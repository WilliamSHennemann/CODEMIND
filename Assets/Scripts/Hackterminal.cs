using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

// Coloque este script SÓ no GameManager (o objeto com DontDestroyOnLoad).
// O nome do arquivo precisa ser exatamente HackTerminal.cs (igual ao da classe).
//
// No Input Field da cena: Line Type = "Multi Line Submit"
//   Enter = executa | Shift+Enter = nova linha
//
// ───────────── O QUE O JOGADOR PODE DIGITAR ─────────────
//
//   Documentos:1            -> um comando (como antes)
//   Hack Folder:1
//
//   Documentos:1; Hack Folder:1   -> vários comandos na mesma linha (separados por ;)
//
//   Para 3:                 -> laço: repete o bloco indentado 3 vezes
//     Mover Direita:1
//
//   Para col de 1 ate 3:    -> laço com contador (use o contador no VALOR do comando)
//     Arquivo:1,col
//
//   Definir Varrer:         -> função: guarda um bloco com um nome
//     Para 3:
//       Mover Direita:1
//   Varrer                  -> chama a função (também vale "Varrer:1")
//
// Blocos são marcados por INDENTAÇÃO (espaços), como em Python.
public class HackTerminal : MonoBehaviour
{
    [Serializable]
    public class AlvoHackeavel
    {
        [Tooltip("O nome que o jogador vai digitar no terminal (ex: 'Hack Folder', 'StartButton')")]
        public string comando;

        [Tooltip("Os GameObjects que vão aparecer/desaparecer juntos. Podem começar DESATIVADOS no Inspector.")]
        public List<GameObject> objetos = new List<GameObject>();

        [Tooltip("OPCIONAL: comandos (separados por vírgula) que precisam estar ATIVOS antes deste funcionar.")]
        public string contextoNecessario;

        [Tooltip("OPCIONAL: valor exato exigido (ex: '1', 'root', '5'). Deixe vazio para aceitar qualquer valor.")]
        public string valorEsperado = "1";

        [Tooltip("Se marcado, a câmera foca no objeto ao ativar. Desmarque para comandos 'invisíveis' (flags).")]
        public bool moverCamera = true;

        [NonSerialized] string[] _requisitos;

        public bool TemObjetos => objetos != null && objetos.Count > 0;

        // Lista de requisitos já separada (calculada uma vez só)
        public string[] Requisitos
        {
            get
            {
                if (_requisitos == null)
                {
                    _requisitos = (contextoNecessario ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < _requisitos.Length; i++) _requisitos[i] = _requisitos[i].Trim();
                }
                return _requisitos;
            }
        }
    }

    [Header("Nome exato do Input Field na cena")]
    [Tooltip("Se não achar, ele usa o primeiro TMP_InputField da cena")]
    public string nomeDoInputField = "InputField (TMP)";

    [Header("Execução")]
    [Tooltip("Pausa (segundos) entre um comando e o próximo quando há várias linhas ou laços.")]
    [Min(0f)] public float atrasoEntreComandos = 0.4f;

    [Tooltip("Limite de passos por execução. Protege contra laços gigantes e funções que chamam a si mesmas.")]
    public int maxPassos = 500;

    [Tooltip("Limpa a caixa depois de executar. Desmarque para o jogador poder corrigir o próprio código.")]
    public bool limparAposExecutar = true;

    [Header("Alvos Hackeáveis")]
    public List<AlvoHackeavel> alvos = new List<AlvoHackeavel>();

    // ── Para UI/feedback (destaque de linha, mensagem de erro, fim de fase...) ──
    // Lembre de se desinscrever no OnDisable do seu script de UI.
    public static HackTerminal Instancia { get; private set; }
    public bool Rodando { get; private set; }
    public event Action<int> AoExecutarLinha;      // número da linha (1 = primeira) que está rodando agora
    public event Action<int, string> AoErro;       // linha + mensagem pronta para mostrar ao jogador
    public event Action<bool> AoTerminar;          // true = terminou sem erros

    class Linha
    {
        public string texto;
        public int indent, numero;
        public List<Linha> filhos = new List<Linha>();
    }

    struct Passo
    {
        public int linha;
        public string nome, valor;
    }

    TMP_InputField commandInput;
    CameraFocus cameraFocus;
    int _contador;

    // alvo ativo por comando (o valor pode ser null se o objeto foi achado por nome)
    readonly Dictionary<string, AlvoHackeavel> _ativos = new Dictionary<string, AlvoHackeavel>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, List<Linha>> _funcoes = new Dictionary<string, List<Linha>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, int> _vars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // ───────────────────────── Ciclo de vida / conexão com a cena ─────────────────────────

    void Awake() => Instancia = this;

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start() => ConectarNaCenaAtual();

    void OnSceneLoaded(Scene cena, LoadSceneMode modo)
    {
        Parar();             // não deixa um programa da cena anterior continuar rodando
        _funcoes.Clear();    // funções valem só dentro da fase
        ConectarNaCenaAtual();
    }

    void ConectarNaCenaAtual()
    {
        if (commandInput != null) commandInput.onSubmit.RemoveListener(AoEnviar);

        GameObject campo = GameObject.Find(nomeDoInputField);
        commandInput = campo != null ? campo.GetComponent<TMP_InputField>() : FindFirstObjectByType<TMP_InputField>();
        cameraFocus = FindFirstObjectByType<CameraFocus>();

        if (commandInput != null) commandInput.onSubmit.AddListener(AoEnviar);
        else Debug.LogWarning("HackTerminal: não achei nenhum TMP_InputField nesta cena.");

        if (cameraFocus == null) Debug.LogWarning("HackTerminal: não achei nenhum CameraFocus nesta cena.");

        PopularAlvosPadrao();
    }

    // Cria 'Hack Folder' -> StartButton (exige 'conceito') se ainda não existir na lista.
    void PopularAlvosPadrao()
    {
        if (alvos.Exists(a => Igual(a.comando, "Hack Folder"))) return;

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "StartButton") continue;

            alvos.Add(new AlvoHackeavel
            {
                comando = "Hack Folder",
                objetos = new List<GameObject> { t.gameObject },
                contextoNecessario = "conceito"
            });
            Debug.Log("HackTerminal: 'Hack Folder' configurado automaticamente para abrir StartButton.");
            return;
        }

        Debug.LogWarning("HackTerminal: não encontrei nenhum objeto chamado 'StartButton' nesta cena.");
    }

    // ───────────────────────── Entrada do jogador ─────────────────────────

    void AoEnviar(string texto)
    {
        if (!string.IsNullOrWhiteSpace(texto)) Executar(texto);

        if (commandInput == null) return;
        if (limparAposExecutar) commandInput.text = "";
        commandInput.ActivateInputField();
    }

    // Pode ser chamado também por um botão "Executar" na UI.
    public void Executar(string codigo)
    {
        if (Rodando)
        {
            AoErro?.Invoke(0, "Aguarde o programa terminar.");
            return;
        }

        _contador = 0;
        _vars.Clear();

        // 1) Lê e "desenrola" o código inteiro ANTES de rodar nada:
        //    erros de escrita são avisados sem executar metade do programa.
        var passos = new List<Passo>();
        if (!Expandir(Ler(codigo), passos))
        {
            AoTerminar?.Invoke(false);
            return;
        }

        // 2) Executa os passos um a um.
        StartCoroutine(Rodar(passos));
    }

    public void Parar()
    {
        StopAllCoroutines();
        Rodando = false;
    }

    public void LimparFuncoes() => _funcoes.Clear();

    IEnumerator Rodar(List<Passo> passos)
    {
        Rodando = true;
        bool ok = true;
        WaitForSeconds espera = atrasoEntreComandos > 0f ? new WaitForSeconds(atrasoEntreComandos) : null;

        for (int i = 0; i < passos.Count; i++)
        {
            Passo p = passos[i];
            AoExecutarLinha?.Invoke(p.linha);

            if (!Aplicar(p.nome, p.valor, out string erro))
            {
                Falhar(p.linha, erro);
                ok = false;
                break;
            }

            if (espera != null && i < passos.Count - 1) yield return espera;
        }

        Rodando = false;
        AoTerminar?.Invoke(ok);
    }

    // ───────────────────────── Leitura do texto (linhas e indentação) ─────────────────────────

    List<Linha> Ler(string codigo)
    {
        var raiz = new List<Linha>();
        var pilha = new Stack<Linha>();
        string[] brutas = codigo.Replace("\r", "").Replace("\u200B", "").Split('\n');

        for (int i = 0; i < brutas.Length; i++)
        {
            string bruta = brutas[i].Replace("\t", "    ");
            int indent = bruta.Length - bruta.TrimStart().Length;

            // ';' separa vários comandos na mesma linha (todos com a mesma indentação)
            foreach (string parte in bruta.Split(';'))
            {
                string texto = parte.Trim();
                if (texto.Length == 0) continue;

                var linha = new Linha { texto = texto, indent = indent, numero = i + 1 };
                while (pilha.Count > 0 && pilha.Peek().indent >= indent) pilha.Pop();

                if (pilha.Count == 0) raiz.Add(linha);
                else pilha.Peek().filhos.Add(linha);

                pilha.Push(linha);
            }
        }
        return raiz;
    }

    // ───────────────────────── Desenrola laços e funções em uma lista simples de passos ─────────────────────────

    // linhaForcada > 0: passos vindos de dentro de uma função ficam "presos" à linha da chamada.
    bool Expandir(List<Linha> bloco, List<Passo> saida, int linhaForcada = 0)
    {
        foreach (Linha l in bloco)
        {
            int num = linhaForcada > 0 ? linhaForcada : l.numero;
            if (++_contador > maxPassos)
                return Falhar(num, $"Passos demais (limite {maxPassos}). Tem um laço ou função sem fim?");

            string[] pal = l.texto.TrimEnd(':').Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string kw = pal.Length > 0 ? pal[0].ToLowerInvariant() : "";
            bool ehBloco = kw == "para" || kw == "repetir" || kw == "definir";

            if (ehBloco)
            {
                if (!l.texto.EndsWith(":")) return Falhar(num, $"Faltou o ':' no fim da linha '{pal[0]}'.");
                if (l.filhos.Count == 0) return Falhar(num, "O bloco está vazio. Indente (com espaços) as linhas que fazem parte dele.");

                if (kw == "definir")
                {
                    if (pal.Length != 2) return Falhar(num, "Use assim: Definir NomeDaFuncao:");
                    _funcoes[pal[1]] = l.filhos;
                }
                else if (!ExpandirLaco(l, pal, saida, linhaForcada, num))
                {
                    return false;
                }
                continue;
            }

            if (l.filhos.Count > 0)
                return Falhar(l.filhos[0].numero, "Esta linha está indentada, mas a de cima não abre um bloco (Para / Repetir / Definir).");

            // Comando comum ou chamada de função
            int pos = l.texto.IndexOf(':');
            string nome = (pos < 0 ? l.texto : l.texto.Substring(0, pos)).Trim();

            if (_funcoes.TryGetValue(nome, out List<Linha> corpo))
            {
                if (!Expandir(corpo, saida, num)) return false;
                continue;
            }

            if (pos < 0) return Falhar(num, "Faltou o ':' entre o nome e o valor. Exemplo: Hack Folder:1");

            string valor = Substituir(l.texto.Substring(pos + 1).Trim().Trim('"'));
            if (nome.Length == 0 || valor.Length == 0) return Falhar(num, "Comando incompleto. Use Nome:valor");

            saida.Add(new Passo { linha = num, nome = nome, valor = valor });
        }
        return true;
    }

    // "Para 3:"  /  "Repetir 3 vezes:"  /  "Para col de 1 ate 3:"
    bool ExpandirLaco(Linha l, string[] pal, List<Passo> saida, int linhaForcada, int num)
    {
        var p = new List<string>();
        foreach (string w in pal)
            if (!w.Equals("vezes", StringComparison.OrdinalIgnoreCase)) p.Add(w);

        string variavel = null;
        int ini = 1, fim = 0;

        if (p.Count == 2 && Numero(p[1], out fim))
        {
            // laço simples: roda 'fim' vezes
        }
        else if (p.Count == 6 && p[2].Equals("de", StringComparison.OrdinalIgnoreCase)
                 && (p[4].Equals("ate", StringComparison.OrdinalIgnoreCase) || p[4].Equals("até", StringComparison.OrdinalIgnoreCase))
                 && Numero(p[3], out ini) && Numero(p[5], out fim))
        {
            variavel = p[1];
        }
        else
        {
            return Falhar(num, "Use 'Para 3:' ou 'Para col de 1 ate 3:'.");
        }

        bool tinha = false;
        int antigo = 0;
        if (variavel != null) tinha = _vars.TryGetValue(variavel, out antigo);

        for (int i = ini; i <= fim; i++)
        {
            if (variavel != null) _vars[variavel] = i;
            if (!Expandir(l.filhos, saida, linhaForcada)) return false;
        }

        if (variavel != null)
        {
            if (tinha) _vars[variavel] = antigo;
            else _vars.Remove(variavel);
        }
        return true;
    }

    bool Numero(string t, out int n) => int.TryParse(t, out n) || _vars.TryGetValue(t, out n);

    // Troca o nome das variáveis dos laços pelo valor atual (só na parte do valor do comando)
    string Substituir(string valor)
    {
        foreach (var kv in _vars)
            valor = Regex.Replace(valor, $@"\b{Regex.Escape(kv.Key)}\b", kv.Value.ToString(), RegexOptions.IgnoreCase);
        return valor;
    }

    // ───────────────────────── Executa UM comando (mesma lógica de antes, mais enxuta) ─────────────────────────

    bool Aplicar(string nome, string valor, out string erro)
    {
        erro = null;
        bool ativar = valor != "0";
        List<AlvoHackeavel> candidatos = alvos.FindAll(a => Igual(a.comando, nome));
        AlvoHackeavel alvo = null;

        if (candidatos.Count > 0)
        {
            if (ativar)
            {
                alvo = candidatos.Find(a => ContextoOk(a) && ValorOk(a, valor));
                if (alvo == null)
                {
                    AlvoHackeavel semContexto = candidatos.Find(a => !ContextoOk(a));
                    erro = semContexto != null
                        ? $"Você precisa ativar '{semContexto.contextoNecessario}' antes de usar '{nome}'."
                        : $"Valor incorreto para '{nome}'.";
                    return false;
                }
            }
            else if (!_ativos.TryGetValue(nome, out alvo))
            {
                // Nunca foi aberto por aqui: usa a primeira entrada com objetos como alternativa
                alvo = candidatos.Find(a => a.TemObjetos) ?? candidatos[0];
            }
        }

        GameObject primeiro = null;

        if (alvo != null && alvo.TemObjetos)
        {
            foreach (GameObject obj in alvo.objetos)
            {
                if (obj == null) continue;
                obj.SetActive(ativar);
                if (primeiro == null) primeiro = obj;
            }
        }
        else
        {
            GameObject achado = GameObject.Find(nome);
            if (achado != null)
            {
                achado.SetActive(ativar);
                primeiro = achado;
            }
        }

        if (primeiro == null)
        {
            erro = $"Não achei nada chamado '{nome}'. Confira se digitou igualzinho.";
            return false;
        }

        if (ativar) _ativos[nome] = alvo;
        else _ativos.Remove(nome);

        if (ativar && cameraFocus != null && (alvo == null || alvo.moverCamera))
            cameraFocus.Focar(primeiro);

        return true;
    }

    // ───────────────────────── Utilidades ─────────────────────────

    bool Falhar(int linha, string mensagem)
    {
        Debug.LogWarning($"[Linha {linha}] {mensagem}");
        AoErro?.Invoke(linha, mensagem);
        return false;
    }

    static bool Igual(string a, string b) =>
        !string.IsNullOrEmpty(a) && a.Trim().Equals(b, StringComparison.OrdinalIgnoreCase);

    bool ContextoOk(AlvoHackeavel a)
    {
        foreach (string req in a.Requisitos)
            if (req.Length > 0 && !_ativos.ContainsKey(req)) return false;
        return true;
    }

    static bool ValorOk(AlvoHackeavel a, string valor) =>
        string.IsNullOrEmpty(a.valorEsperado) || valor.Equals(a.valorEsperado, StringComparison.OrdinalIgnoreCase);
}