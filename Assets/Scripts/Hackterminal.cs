using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

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

        [Tooltip("OPCIONAL: comandos que precisam estar ATIVOS antes deste funcionar.\n" +
                 "Vírgula = E (todos).   Barra '|' = OU (qualquer um).\n" +
                 "Ex: 'Folder | Documentos'  -> vale se Folder OU Documentos estiver ativo.\n" +
                 "Ex: 'Folder, Senha | Admin' -> (Folder E Senha) OU Admin.")]
        public string contextoNecessario;

        [Tooltip("OPCIONAL: valor exato exigido (ex: '1', 'root', '5'). Use '|' para aceitar mais de um (ex: 'true|1'). Deixe vazio para aceitar qualquer valor.")]
        public string valorEsperado = "1";

        [Tooltip("Se marcado, a câmera foca no objeto ao ativar. Desmarque para comandos 'invisíveis' (flags).")]
        public bool moverCamera = true;

        [Tooltip("Se marcado, ao ativar esta entrada o resultado ANTERIOR do mesmo comando é escondido.\n" +
                 "Use quando há várias entradas com o mesmo comando e só uma deve aparecer por vez\n" +
                 "(ex.: dois 'Inspection' com resultados diferentes).")]
        public bool substituiResultadoAnterior = false;

        [Header("Atraso para mostrar (opcional)")]
        [Tooltip("Se marcado, os objetos desta entrada só aparecem DEPOIS que a varredura do TextoInspecionavel terminar.\n" +
                 "Use no Inspection: a mensagem de erro / o diálogo do Jerry esperam a busca acabar.")]
        public bool esperarVarredura = false;

        [Tooltip("Segundos extras de espera antes de mostrar os objetos (soma com a espera da varredura, se estiver marcada).")]
        [Min(0f)] public float atrasoParaMostrar = 0f;

        [Header("Cadeado (opcional)")]
        [Tooltip("Cadeado corrompido com a animação de 'fechado'. Some assim que o jogador hackeia.")]
        public GameObject cadeadoFechado;

        [Tooltip("Cadeado abrindo (precisa de um Animator). Deixe DESATIVADO no início. Aparece ao hackear, toca a animação UMA vez, congela no último frame e fica até o jogador sair da pasta.")]
        public GameObject cadeadoAbrindo;

        [Tooltip("Só é usada se o cadeado abrindo NÃO tiver Animator. Com Animator, o tempo é automático (a pasta abre quando a animação termina).")]
        public float duracaoAbertura = 1f;

        [NonSerialized] public bool destrancado;

        public bool PrecisaDestrancar => !destrancado && (cadeadoFechado != null || cadeadoAbrindo != null);

        public void EsconderCadeados()
        {
            if (cadeadoFechado != null) cadeadoFechado.SetActive(false);
            if (cadeadoAbrindo != null) cadeadoAbrindo.SetActive(false);
        }

        [NonSerialized] string[][] _grupos;

        public bool TemObjetos => objetos != null && objetos.Count > 0;

        // Cada grupo é uma ALTERNATIVA (separadas por '|'). Dentro do grupo, ',' = todos precisam estar ativos.
        // Vazio = sem exigência. Calculado uma vez só.
        public string[][] GruposDeContexto
        {
            get
            {
                if (_grupos == null)
                {
                    var lista = new List<string[]>();
                    foreach (string alternativa in (contextoNecessario ?? "").Split('|'))
                    {
                        var reqs = new List<string>();
                        foreach (string r in alternativa.Split(','))
                        {
                            string t = r.Trim();
                            if (t.Length > 0) reqs.Add(t);
                        }
                        if (reqs.Count > 0) lista.Add(reqs.ToArray());
                    }
                    _grupos = lista.ToArray();
                }
                return _grupos;
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

    [Header("Histórico de comandos")]
    [Tooltip("Ctrl + ↑ / ↓ na caixa de comando percorre os códigos já enviados NESTA fase. As setas sozinhas só movem o cursor entre as linhas.")]
    public bool usarHistorico = true;

    [Tooltip("Quantos códigos guardar por fase (os mais antigos são descartados).")]
    [Min(1)] public int maxHistorico = 100;

    [Tooltip("Também grava cada código enviado em um arquivo de texto (historico_NomeDaFase.txt em Application.persistentDataPath). Útil para o professor revisar.")]
    public bool salvarEmArquivo = false;

    [Header("Alvos Hackeáveis")]
    public List<AlvoHackeavel> alvos = new List<AlvoHackeavel>();

    // ── Para UI/feedback (destaque de linha, mensagem de erro, fim de fase...) ──
    // Lembre de se desinscrever no OnDisable do seu script de UI.
    public static HackTerminal Instancia { get; private set; }
    public bool Rodando { get; private set; }
    public event Action<int> AoExecutarLinha;      // número da linha (1 = primeira) que está rodando agora
    public event Action<int, string> AoErro;       // linha + mensagem pronta para mostrar ao jogador
    public event Action<bool> AoTerminar;          // true = terminou sem erros
    public event Action<string, string> AoComandoExecutado;   // nome e valor de um comando que FUNCIONOU (ex.: "Inspection", "1")

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
    // "quando" cada comando foi ativado (número crescente). Serve para saber qual contexto é o MAIS RECENTE.
    readonly Dictionary<string, int> _quando = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    int _carimbo;

    // ── Arquivos abertos NA TELA (avisados pelo componente ArquivoAberto, ou por DefinirArquivoAtual) ──
    // O último da lista é o "arquivo atual". Um contexto pode ser o nome de um arquivo aberto, além de um comando ativo.
    readonly List<(string nome, int quando)> _arquivos = new List<(string nome, int quando)>();

    public string ArquivoAtual => _arquivos.Count > 0 ? _arquivos[_arquivos.Count - 1].nome : null;

    // Chame quando um arquivo aparecer (o componente ArquivoAberto já faz isso sozinho).
    public void DefinirArquivoAtual(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;
        nome = nome.Trim();

        _arquivos.RemoveAll(a => Igual(a.nome, nome));   // reabrir = vira o mais recente
        _arquivos.Add((nome, ++_carimbo));
    }

    // Chame quando o arquivo sumir. Se havia outro aberto por baixo, ele volta a ser o atual.
    public void FecharArquivo(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;
        nome = nome.Trim();
        _arquivos.RemoveAll(a => Igual(a.nome, nome));
    }

    readonly Dictionary<string, List<Linha>> _funcoes = new Dictionary<string, List<Linha>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, int> _vars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // histórico: uma lista por fase (nome da cena). Sobrevive enquanto o GameManager existir.
    readonly Dictionary<string, List<string>> _historicoPorFase = new Dictionary<string, List<string>>();
    List<string> _historico = new List<string>();
    int _posHistorico = -1;      // -1 = não está navegando
    string _rascunho = "";       // o que o jogador já tinha digitado antes de apertar ↑

    // Códigos enviados nesta fase (só leitura) — dá para usar numa tela de revisão.
    public IReadOnlyList<string> Historico => _historico;

    // ───────────────────────── Ciclo de vida / conexão com a cena ─────────────────────────

    void Awake() => Instancia = this;

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void Start()
    {
        TrocarHistorico(SceneManager.GetActiveScene().name);
        ConectarNaCenaAtual();
    }

    void OnSceneLoaded(Scene cena, LoadSceneMode modo)
    {
        Parar();             // não deixa um programa da cena anterior continuar rodando
        TrocarHistorico(cena.name);
        _funcoes.Clear();    // funções valem só dentro da fase
        foreach (AlvoHackeavel a in alvos) a.destrancado = false;
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

        RegistrarHistorico(codigo);

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

    // ───────────────────────── Histórico de comandos (seta ↑ / ↓) ─────────────────────────

    void Update()
    {
        if (!usarHistorico || commandInput == null || !commandInput.isFocused || _historico.Count == 0) return;

        // Ctrl + ↑ / ↓ = histórico. As setas sozinhas continuam movendo o cursor entre as linhas do código.
        if (!CtrlSegurado()) return;

        if (SetaApertada(true)) Navegar(-1);
        else if (SetaApertada(false)) Navegar(+1);
    }

    void Navegar(int direcao)
    {
        if (direcao < 0)
        {
            if (_posHistorico < 0)
            {
                _rascunho = commandInput.text;                 // guarda o que estava sendo digitado
                _posHistorico = _historico.Count - 1;
            }
            else
            {
                _posHistorico = Mathf.Max(0, _posHistorico - 1);
            }
        }
        else
        {
            if (_posHistorico < 0) return;
            _posHistorico++;
            if (_posHistorico >= _historico.Count) _posHistorico = -1;   // passou do mais recente: volta ao rascunho
        }

        commandInput.text = _posHistorico < 0 ? _rascunho : _historico[_posHistorico];
        StartCoroutine(CursorNoFim());
    }

    // Espera 1 frame para o campo terminar de tratar a própria tecla, e então põe o cursor no fim do texto.
    IEnumerator CursorNoFim()
    {
        yield return null;
        if (commandInput == null) yield break;
        commandInput.caretPosition = commandInput.text.Length;
    }

    void RegistrarHistorico(string codigo)
    {
        _posHistorico = -1;
        _rascunho = "";

        string limpo = codigo.TrimEnd();
        if (limpo.Length == 0) return;

        // Não repete se for igual ao último enviado
        if (_historico.Count == 0 || _historico[_historico.Count - 1] != limpo)
        {
            _historico.Add(limpo);
            if (_historico.Count > maxHistorico) _historico.RemoveAt(0);
        }

        if (salvarEmArquivo) GravarEmArquivo(limpo);
    }

    void TrocarHistorico(string fase)
    {
        if (!_historicoPorFase.TryGetValue(fase, out _historico))
        {
            _historico = new List<string>();
            _historicoPorFase[fase] = _historico;
        }
        _posHistorico = -1;
        _rascunho = "";
    }

    public void LimparHistorico()
    {
        _historico.Clear();
        _posHistorico = -1;
        _rascunho = "";
    }

    void GravarEmArquivo(string codigo)
    {
        try
        {
            string caminho = Path.Combine(Application.persistentDataPath, $"historico_{SceneManager.GetActiveScene().name}.txt");
            File.AppendAllText(caminho, $"[{DateTime.Now:HH:mm:ss}]\n{codigo}\n\n");
        }
        catch (Exception e)
        {
            Debug.LogWarning("HackTerminal: não consegui salvar o histórico em arquivo: " + e.Message);
        }
    }

    // Funcionam tanto com o Input System novo quanto com o antigo
    static bool CtrlSegurado()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        return kb != null && kb.ctrlKey.isPressed;
#else
        return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
#endif
    }

    static bool SetaApertada(bool cima)
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        Keyboard kb = Keyboard.current;
        if (kb == null) return false;
        return cima ? kb.upArrowKey.wasPressedThisFrame : kb.downArrowKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(cima ? KeyCode.UpArrow : KeyCode.DownArrow);
#endif
    }

    // ── "Ocupado": outros scripts (ex.: TextoInspecionavel) seguram o programa enquanto fazem algo.
    // Entradas com 'Esperar Varredura' só mostram os objetos quando ninguém mais estiver segurando.
    int _ocupado;
    public void Segurar() => _ocupado++;
    public void Soltar() => _ocupado = Mathf.Max(0, _ocupado - 1);

    public void Parar()
    {
        StopAllCoroutines();
        Rodando = false;
    }

    public void LimparFuncoes() => _funcoes.Clear();

    // Toca a animação do cadeado abrindo UMA vez só (mesmo que o clip esteja com Loop Time ligado)
    // e congela no último frame. Sem Animator, apenas espera 'duracaoAbertura'.
    IEnumerator TocarCadeadoUmaVez(AlvoHackeavel alvo)
    {
        Animator anim = alvo.cadeadoAbrindo != null ? alvo.cadeadoAbrindo.GetComponentInChildren<Animator>() : null;

        if (anim == null)
        {
            if (alvo.duracaoAbertura > 0f) yield return new WaitForSeconds(alvo.duracaoAbertura);
            yield break;
        }

        anim.speed = 1f;
        yield return null;                         // deixa o Animator entrar no primeiro estado

        AnimatorStateInfo estado = anim.GetCurrentAnimatorStateInfo(0);
        float tempo = 0f;
        float teto = Mathf.Max(5f, alvo.duracaoAbertura);   // segurança: nunca trava o jogo

        while (tempo < teto)
        {
            estado = anim.GetCurrentAnimatorStateInfo(0);
            if (estado.normalizedTime >= 1f) break;         // completou a 1ª volta
            tempo += Time.deltaTime;
            yield return null;
        }

        // Volta ao final do clip e congela (assim não recomeça mesmo com Loop Time ligado)
        anim.Play(estado.fullPathHash, 0, 0.999f);
        anim.Update(0f);
        anim.speed = 0f;
    }

    IEnumerator Rodar(List<Passo> passos)
    {
        Rodando = true;
        bool ok = true;
        WaitForSeconds espera = atrasoEntreComandos > 0f ? new WaitForSeconds(atrasoEntreComandos) : null;

        for (int i = 0; i < passos.Count; i++)
        {
            Passo p = passos[i];
            AoExecutarLinha?.Invoke(p.linha);

            bool ativar = p.valor != "0";
            string erro;

            if (!Resolver(p.nome, p.valor, ativar, out AlvoHackeavel alvo, out erro))
            {
                Falhar(p.linha, erro);
                ok = false;
                break;
            }

            // Entrada com atraso: avisa os ouvintes AGORA (a varredura do Inspection começa) e só depois mostra os objetos.
            bool avisou = false;
            if (ativar && alvo != null && (alvo.esperarVarredura || alvo.atrasoParaMostrar > 0f))
            {
                AoComandoExecutado?.Invoke(p.nome, p.valor);
                avisou = true;

                if (alvo.esperarVarredura)
                {
                    float esperado = 0f;
                    while (_ocupado > 0 && esperado < 10f)      // 10 s = trava de segurança, nunca congela o jogo
                    {
                        esperado += Time.deltaTime;
                        yield return null;
                    }
                }

                if (alvo.atrasoParaMostrar > 0f) yield return new WaitForSeconds(alvo.atrasoParaMostrar);
            }

            // Hackeou uma pasta trancada: troca o cadeado corrompido pelo cadeado abrindo
            // e espera a animação terminar ANTES de abrir a pasta.
            if (ativar && alvo != null && alvo.PrecisaDestrancar)
            {
                if (alvo.cadeadoFechado != null) alvo.cadeadoFechado.SetActive(false);
                if (alvo.cadeadoAbrindo != null) alvo.cadeadoAbrindo.SetActive(true);
                yield return StartCoroutine(TocarCadeadoUmaVez(alvo));
            }

            bool estavaAberto = _ativos.ContainsKey(p.nome);

            if (!Efetivar(p.nome, alvo, ativar, out erro))
            {
                Falhar(p.linha, erro);
                ok = false;
                break;
            }

            if (alvo != null)
            {
                if (ativar) alvo.destrancado = true;                 // não repete a animação se abrir de novo
                else if (estavaAberto) alvo.EsconderCadeados();      // saiu da pasta: o cadeado some
            }

            // Avisa quem estiver ouvindo (ex.: TextoInspecionavel) que este comando acabou de funcionar
            if (!avisou) AoComandoExecutado?.Invoke(p.nome, p.valor);

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

    // Descobre QUAL alvo da lista vale para este comando (contexto, valor esperado, etc.)
    bool Resolver(string nome, string valor, bool ativar, out AlvoHackeavel alvo, out string erro)
    {
        erro = null;
        alvo = null;
        List<AlvoHackeavel> candidatos = alvos.FindAll(a => Igual(a.comando, nome));

        if (candidatos.Count > 0)
        {
            if (ativar)
            {
                alvo = MelhorCandidato(candidatos, valor);
                if (alvo == null)
                {
                    LogDiagnostico(nome, valor, candidatos);
                    // Junta o que falta de TODAS as entradas (e não só da primeira)
                    var faltando = new List<string>();
                    foreach (AlvoHackeavel c in candidatos)
                        if (!ContextoOk(c)) faltando.Add(DescreverContexto(c));

                    erro = faltando.Count > 0
                        ? $"Você precisa ativar '{string.Join(" ou ", faltando)}' antes de usar '{nome}'."
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

        return true;
    }

    // Liga/desliga os objetos do alvo, atualiza o estado e foca a câmera
    bool Efetivar(string nome, AlvoHackeavel alvo, bool ativar, out string erro)
    {
        erro = null;
        GameObject primeiro = null;

        // Entrada "exclusiva": esconde o resultado anterior do mesmo comando ANTES de mostrar o novo
        if (ativar && alvo != null && alvo.substituiResultadoAnterior
            && _ativos.TryGetValue(nome, out AlvoHackeavel anterior)
            && anterior != null && anterior != alvo && anterior.TemObjetos)
        {
            foreach (GameObject o in anterior.objetos)
                if (o != null) o.SetActive(false);
        }

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
            // A entrada existe e foi aceita, mas os slots de Objetos estão vazios (None): erro de configuração, não de digitação.
            erro = (alvo != null && alvo.TemObjetos)
                ? $"A entrada '{nome}' foi aceita, mas os slots de Objetos dela estão vazios (None). Arraste os objetos no Inspector."
                : $"Não achei nada chamado '{nome}'. Confira se digitou igualzinho.";
            return false;
        }

        if (ativar)
        {
            _ativos[nome] = alvo;
            _quando[nome] = ++_carimbo;
        }
        else
        {
            _ativos.Remove(nome);
            _quando.Remove(nome);
        }

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

    // Um requisito de contexto vale se for o ARQUIVO ATUAL (o que está na frente, na tela)
    // ou um COMANDO que está ativo. 'quando' diz o quão recente isso aconteceu (maior = mais novo).
    bool RequisitoAtivo(string req, out int quando)
    {
        quando = 0;

        if (_arquivos.Count > 0)
        {
            var atual = _arquivos[_arquivos.Count - 1];
            if (Igual(atual.nome, req))
            {
                quando = atual.quando;
                return true;
            }
        }

        if (_ativos.ContainsKey(req))
        {
            quando = _quando.TryGetValue(req, out int q) ? q : 1;
            return true;
        }

        return false;
    }

    // OK se não há exigência, ou se PELO MENOS UM grupo ('|') tem todos os seus requisitos ativos (',').
    bool ContextoOk(AlvoHackeavel a)
    {
        string[][] grupos = a.GruposDeContexto;
        if (grupos.Length == 0) return true;

        foreach (string[] grupo in grupos)
        {
            bool todos = true;
            foreach (string req in grupo)
            {
                if (!RequisitoAtivo(req, out _)) { todos = false; break; }
            }
            if (todos) return true;
        }
        return false;
    }

    // Só para você (Console): mostra por que cada entrada do comando foi recusada.
    void LogDiagnostico(string nome, string valor, List<AlvoHackeavel> candidatos)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"HackTerminal: '{nome}:{valor}' não bateu com nenhuma entrada.");
        sb.AppendLine($"Arquivo aberto agora: '{ArquivoAtual ?? "(nenhum)"}'   |   todos os abertos: [{string.Join(" | ", _arquivos.ConvertAll(x => x.nome))}]");
        sb.AppendLine($"Comandos ativos agora: [{string.Join(" | ", _ativos.Keys)}]");

        for (int i = 0; i < candidatos.Count; i++)
        {
            AlvoHackeavel a = candidatos[i];
            sb.AppendLine($"  Entrada {i + 1}: contexto = '{a.contextoNecessario}'  (satisfeito: {ContextoOk(a)})  |  valor esperado = '{a.valorEsperado}'  (ok: {ValorOk(a, valor)})");
        }
        Debug.Log(sb.ToString());
    }

    // Entre as entradas que servem (valor + contexto), escolhe a de contexto MAIS RECENTE:
    // o último arquivo aberto ganha. Empate: a primeira da lista.
    AlvoHackeavel MelhorCandidato(List<AlvoHackeavel> candidatos, string valor)
    {
        AlvoHackeavel melhor = null;
        int melhorNota = -1;

        foreach (AlvoHackeavel a in candidatos)
        {
            if (!ValorOk(a, valor)) continue;

            int nota = NotaDeContexto(a);
            if (nota > melhorNota)
            {
                melhor = a;
                melhorNota = nota;
            }
        }
        return melhor;
    }

    // -1 = contexto NÃO satisfeito | 0 = sem exigência | >0 = quão recente é o requisito mais novo que foi satisfeito
    int NotaDeContexto(AlvoHackeavel a)
    {
        string[][] grupos = a.GruposDeContexto;
        if (grupos.Length == 0) return 0;

        int nota = -1;
        foreach (string[] grupo in grupos)
        {
            bool todos = true;
            int recente = 0;

            foreach (string req in grupo)
            {
                if (!RequisitoAtivo(req, out int q)) { todos = false; break; }
                recente = Mathf.Max(recente, q);
            }

            if (todos && recente > nota) nota = recente;
        }
        return nota;
    }

    // Texto amigável para a mensagem de erro: "Folder | Documentos" -> "Folder ou Documentos"
    static string DescreverContexto(AlvoHackeavel a) =>
        a.contextoNecessario.Replace("|", " ou ").Replace(",", " e ");

    static bool ValorOk(AlvoHackeavel a, string valor)
    {
        if (string.IsNullOrEmpty(a.valorEsperado)) return true;

        foreach (string v in a.valorEsperado.Split('|'))
            if (valor.Equals(v.Trim(), StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}