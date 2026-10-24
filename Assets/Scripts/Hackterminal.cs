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

        public enum ModoCaderno { Normal, NaoGuardar, UsarExemplo }

        [Header("Caderno de comandos (opcional)")]
        [Tooltip("O que o botão do caderno guarda quando este comando funciona:\n" +
                 "Normal = o comando que o jogador digitou.\n" +
                 "Nao Guardar = este comando nunca entra no caderno.\n" +
                 "Usar Exemplo = guarda o texto de 'Exemplo No Caderno' no lugar. Várias entradas com o mesmo exemplo viram UMA linha só.")]
        public ModoCaderno caderno = ModoCaderno.Normal;

        [Tooltip("Só vale em 'Usar Exemplo'. Ex.: NomeDoArquivo:1\n" +
                 "Quando o jogador usa :0 nesta entrada, o final do exemplo vira :0 sozinho (NomeDoArquivo:0).")]
        public string exemploNoCaderno;

        [Header("Falas em sequência (opcional)")]
        [Tooltip("Nome do grupo. Todas as entradas com o MESMO nome contam juntas.\n" +
                 "Ex.: 'arquivo inutil' em todos os arquivos que não têm a senha.\n" +
                 "Deixe vazio para não usar.")]
        public string grupoDeFalas;

        [Tooltip("Uma fala (GameObject, ex.: o texto do Jerry) por ordem de uso: o 1º arquivo diferente do grupo que o jogador abre mostra a fala 1, o 2º mostra a 2...\n" +
                 "A última fala se repete dali em diante. Reabrir o MESMO arquivo repete a fala que ele já deu.\n" +
                 "Deixe a lista vazia numa entrada com Grupo De Falas preenchido para ela só ESCONDER a fala atual do grupo (use no arquivo certo).\n" +
                 "Deixe as falas DESATIVADAS no Inspector.")]
        public List<GameObject> falasEmSequencia = new List<GameObject>();

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

    [Header("Caderno de comandos (botão ao lado da barra)")]
    [Tooltip("Quantos comandos DIFERENTES o caderno guarda (os mais antigos saem primeiro).")]
    [Min(1)] public int maxComandosUsados = 40;

    [Header("Falas dos arquivos errados (opcional) — configura UMA vez, vale para todos os arquivos")]
    [Tooltip("As falas do Jerry (GameObjects DESATIVADOS), em ordem: o 1º arquivo errado que o jogador abre mostra a fala 1, o 2º mostra a 2...\n" +
             "A última se repete. Reabrir o mesmo arquivo repete a fala que ele já deu.\n" +
             "Vale para todo arquivo que tenha o componente ArquivoAberto (selecione os 12 janelas de uma vez e use Add Component).")]
    public List<GameObject> falasDeArquivoErrado = new List<GameObject>();

    [Tooltip("Os arquivos que NÃO são errados (os backups). Escreva cada nome igual ao do GameObject da janela (ou ao campo 'Nome Do Arquivo' do ArquivoAberto).\n" +
             "Abrir um deles esconde a fala de 'não é aqui' e mostra a 'Fala De Arquivo Certo' (se houver).")]
    public List<string> arquivosCertos = new List<string>();

    [Tooltip("OPCIONAL: fala (GameObject DESATIVADO) que aparece sempre que o jogador abre um dos Arquivos Certos.\n" +
             "Ex.: o aviso de que o texto está corrompido e de que deve usar Inspection.\n" +
             "Se forem vários balões em sequência, coloque todos dentro de um objeto pai e arraste o pai aqui.")]
    public GameObject falaDeArquivoCerto;

    [Tooltip("As falas só valem enquanto este contexto estiver ativo. Ex.: Documentos (mesma regra do Contexto Necessario: ',' = E, '|' = OU).\n" +
             "Evita que arquivos de OUTRA pasta (como o cofre) também gerem a fala de 'arquivo errado'. Vazio = sempre vale.")]
    public string contextoDasFalas = "Documentos";

    [Header("Texto dos comandos (normalização)")]
    [Tooltip("Ignora acentos ao comparar o que o jogador digitou com o que está configurado. 'Inspeção' = 'inspecao'.")]
    public bool ignorarAcentos = true;

    [Tooltip("Ignora espaços, hífens e underscores. 'Hack Folder' = 'hackfolder' = 'hack-folder', e 'LUA-472' = 'lua 472' = 'lua472'.\n" +
             "Símbolos como # @ % * continuam valendo (os nomes corrompidos dos arquivos ainda precisam deles).\n" +
             "Maiúsculas/minúsculas e espaços nas pontas já eram ignorados.")]
    public bool ignorarEspacosEHifens = true;

    // Uma linha do menu de ajuda (vale para TODAS as cenas: o HackTerminal sobrevive à troca de fase)
    [Serializable]
    public class AjudaDeComando
    {
        [Tooltip("Como o jogador digita. Ex.: Hack Folder:1")]
        public string comando;
        [TextArea, Tooltip("O que ele faz, em uma frase curta.")]
        public string descricao;
    }

    [Header("Menu de ajuda (aparece em todas as telas pelo script MenuAjuda)")]
    public List<AjudaDeComando> ajuda = new List<AjudaDeComando>();

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
    public event Action AoMudarComandosUsados;                // o caderno de comandos ganhou ou perdeu uma linha

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
    readonly Dictionary<string, AlvoHackeavel> _ativos = new Dictionary<string, AlvoHackeavel>(ComparadorNormalizado.Instancia);
    // "quando" cada comando foi ativado (número crescente). Serve para saber qual contexto é o MAIS RECENTE.
    readonly Dictionary<string, int> _quando = new Dictionary<string, int>(ComparadorNormalizado.Instancia);
    int _carimbo;

    // ── Falas em sequência: por grupo, a ordem em que cada entrada foi usada pela 1ª vez e a fala que está na tela ──
    readonly Dictionary<string, Dictionary<AlvoHackeavel, int>> _ordemDaFala = new Dictionary<string, Dictionary<AlvoHackeavel, int>>(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, GameObject> _falaAtual = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

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

        FalaDoArquivo(nome);
    }

    // ── Falas dos arquivos errados ──
    readonly Dictionary<string, int> _ordemDosArquivos = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    GameObject _falaDeArquivo;
    string _arquivoDaFala;
    AlvoHackeavel _exigenciaDasFalas;

    void FalaDoArquivo(string nome)
    {
        bool temErrado = falasDeArquivoErrado != null && falasDeArquivoErrado.Count > 0;
        if (!temErrado && falaDeArquivoCerto == null) return;

        // só vale dentro do contexto configurado (ex.: a pasta Documentos)
        if (!string.IsNullOrWhiteSpace(contextoDasFalas))
        {
            if (_exigenciaDasFalas == null || _exigenciaDasFalas.contextoNecessario != contextoDasFalas)
                _exigenciaDasFalas = new AlvoHackeavel { contextoNecessario = contextoDasFalas };

            if (!ContextoOk(_exigenciaDasFalas)) return;
        }

        EsconderFalaDeArquivo();                                   // a fala anterior some
        if (arquivosCertos != null && arquivosCertos.Exists(c => Igual(c, nome)))
        {
            // arquivo certo: nada de "não é aqui"; mostra a fala própria dele, se houver
            if (falaDeArquivoCerto != null)
            {
                falaDeArquivoCerto.SetActive(true);
                _falaDeArquivo = falaDeArquivoCerto;
                _arquivoDaFala = nome;
            }
            return;
        }

        if (!temErrado) return;

        if (!_ordemDosArquivos.TryGetValue(nome, out int posicao))
        {
            posicao = _ordemDosArquivos.Count;                     // 0 = primeiro arquivo errado, 1 = segundo...
            _ordemDosArquivos[nome] = posicao;
        }

        GameObject fala = falasDeArquivoErrado[Mathf.Min(posicao, falasDeArquivoErrado.Count - 1)];
        if (fala == null) return;

        fala.SetActive(true);
        _falaDeArquivo = fala;
        _arquivoDaFala = nome;
    }

    void EsconderFalaDeArquivo()
    {
        if (_falaDeArquivo != null) _falaDeArquivo.SetActive(false);
        _falaDeArquivo = null;
        _arquivoDaFala = null;
    }

    // Chame quando o arquivo sumir. Se havia outro aberto por baixo, ele volta a ser o atual.
    public void FecharArquivo(string nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return;
        nome = nome.Trim();
        _arquivos.RemoveAll(a => Igual(a.nome, nome));

        if (_arquivoDaFala != null && Igual(_arquivoDaFala, nome)) EsconderFalaDeArquivo();   // fechou o arquivo da fala
    }

    readonly Dictionary<string, List<Linha>> _funcoes = new Dictionary<string, List<Linha>>(ComparadorNormalizado.Instancia);
    readonly Dictionary<string, int> _vars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // histórico: uma lista por fase (nome da cena). Sobrevive enquanto o GameManager existir.
    readonly Dictionary<string, List<string>> _historicoPorFase = new Dictionary<string, List<string>>();
    List<string> _historico = new List<string>();
    int _posHistorico = -1;      // -1 = não está navegando
    string _rascunho = "";       // o que o jogador já tinha digitado antes de apertar ↑

    // Códigos enviados nesta fase (só leitura) — dá para usar numa tela de revisão.
    public IReadOnlyList<string> Historico => _historico;

    // ───────────────────────── Ciclo de vida / conexão com a cena ─────────────────────────

    void Awake()
    {
        Instancia = this;
        _acentos = ignorarAcentos;
        _separadores = ignorarEspacosEHifens;
    }

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
        _ordemDaFala.Clear();   // a contagem das falas em sequência recomeça a cada fase
        _falaAtual.Clear();
        _ordemDosArquivos.Clear();
        _falaDeArquivo = null;
        _arquivoDaFala = null;
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

    // ── Caderno: os comandos que FUNCIONARAM, sem repetir, na ordem em que apareceram pela 1ª vez.
    // Erros de digitação nunca entram, então a lista só mostra exemplos certos.
    readonly List<string> _comandosUsados = new List<string>();
    public IReadOnlyList<string> ComandosUsados => _comandosUsados;

    // Avisa os ouvintes e anota o comando no caderno
    void NotificarComando(string nome, string valor, AlvoHackeavel alvo)
    {
        // Avisa com a grafia da lista (ex.: "Inspection"), mesmo que o jogador tenha digitado "inspection " ou "Inspec-tion"
        string nomeDaLista = alvo != null && !string.IsNullOrWhiteSpace(alvo.comando) ? alvo.comando.Trim() : nome;
        AoComandoExecutado?.Invoke(nomeDaLista, valor);

        string comando = TextoParaCaderno(nome, valor, alvo);
        if (comando == null) return;                       // esta entrada está como "Nao Guardar"

        if (_comandosUsados.Exists(c => string.Equals(c, comando, StringComparison.OrdinalIgnoreCase))) return;

        _comandosUsados.Add(comando);
        if (_comandosUsados.Count > maxComandosUsados) _comandosUsados.RemoveAt(0);
        AoMudarComandosUsados?.Invoke();
    }

    // Decide o que vai para o caderno. Devolve null quando o comando não deve aparecer lá.
    string TextoParaCaderno(string nome, string valor, AlvoHackeavel alvo)
    {
        if (alvo != null && alvo.caderno == AlvoHackeavel.ModoCaderno.NaoGuardar) return null;

        if (alvo != null && alvo.caderno == AlvoHackeavel.ModoCaderno.UsarExemplo
            && !string.IsNullOrWhiteSpace(alvo.exemploNoCaderno))
        {
            string exemplo = alvo.exemploNoCaderno.Trim();
            if (valor != "0") return exemplo;

            // ":0" (sair/fechar): troca só o final do exemplo por :0
            int pos = exemplo.LastIndexOf(':');
            return (pos < 0 ? exemplo : exemplo.Substring(0, pos)) + ":0";
        }

        // Normal: o comando como o jogador usou, mas com a grafia da lista (não a que foi digitada)
        AlvoHackeavel conhecido = alvo ?? alvos.Find(a => Igual(a.comando, nome));
        string nomeCerto = conhecido != null && !string.IsNullOrWhiteSpace(conhecido.comando) ? conhecido.comando.Trim() : nome;
        return $"{nomeCerto}:{valor}";
    }

    public void LimparComandosUsados()
    {
        _comandosUsados.Clear();
        AoMudarComandosUsados?.Invoke();
    }

    // Apaga UM comando do caderno. Se o jogador usar o comando de novo, ele volta para a lista.
    public void RemoverComandoUsado(string comando)
    {
        int removidos = _comandosUsados.RemoveAll(c => string.Equals(c, comando, StringComparison.OrdinalIgnoreCase));
        if (removidos > 0) AoMudarComandosUsados?.Invoke();
    }

    // Coloca um comando na caixa de texto (NÃO executa). Se a caixa já tem texto, o comando entra numa linha nova.
    public void UsarNaCaixa(string comando)
    {
        if (commandInput == null || string.IsNullOrEmpty(comando)) return;

        commandInput.text = string.IsNullOrWhiteSpace(commandInput.text)
            ? comando
            : commandInput.text.TrimEnd() + "\n" + comando;

        commandInput.ActivateInputField();
        StartCoroutine(CursorNoFim());
    }

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
                NotificarComando(p.nome, p.valor, alvo);
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
            if (!avisou) NotificarComando(p.nome, p.valor, alvo);

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
            string kw = pal.Length > 0 ? Normalizar(pal[0]) : "";
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

            string valor = Substituir(l.texto.Substring(pos + 1).Trim().Trim('"', '“', '”'));
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
            if (!Igual(w, "vezes")) p.Add(w);

        string variavel = null;
        int ini = 1, fim = 0;

        if (p.Count == 2 && Numero(p[1], out fim))
        {
            // laço simples: roda 'fim' vezes
        }
        else if (p.Count == 6 && Igual(p[2], "de")
                 && Igual(p[4], "ate")        // "ate" e "até" valem (a comparação ignora acentos)
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
            GameObject achado = AcharObjeto(nome);
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

        MostrarFalaEmSequencia(alvo, ativar);

        if (ativar && cameraFocus != null && (alvo == null || alvo.moverCamera))
            cameraFocus.Focar(primeiro);

        return true;
    }

    // Acha um objeto da cena pelo nome que o jogador digitou, ignorando maiúsculas, acentos e espaços/hífens
    // (mesma regra dos comandos). Procura também entre os objetos DESATIVADOS, para dar para reabrir o que foi escondido.
    GameObject AcharObjeto(string nome)
    {
        GameObject exato = GameObject.Find(nome);      // grafia exata e ativo: rápido e preciso
        if (exato != null) return exato;

        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (Igual(t.name, nome)) return t.gameObject;

        return null;
    }

    // Escolhe a fala pela ordem em que os arquivos DIFERENTES do grupo foram abertos.
    // Sem lista de falas: só esconde a fala que estava na tela (serve para o arquivo certo).
    void MostrarFalaEmSequencia(AlvoHackeavel alvo, bool ativar)
    {
        if (alvo == null || string.IsNullOrWhiteSpace(alvo.grupoDeFalas)) return;
        string grupo = alvo.grupoDeFalas.Trim();

        // esconde a fala que estava aparecendo deste grupo
        if (_falaAtual.TryGetValue(grupo, out GameObject atual) && atual != null) atual.SetActive(false);
        _falaAtual.Remove(grupo);

        if (!ativar || alvo.falasEmSequencia == null || alvo.falasEmSequencia.Count == 0) return;

        if (!_ordemDaFala.TryGetValue(grupo, out Dictionary<AlvoHackeavel, int> ordem))
        {
            ordem = new Dictionary<AlvoHackeavel, int>();
            _ordemDaFala[grupo] = ordem;
        }
        if (!ordem.TryGetValue(alvo, out int posicao))
        {
            posicao = ordem.Count;        // 0 = primeiro arquivo diferente aberto, 1 = segundo...
            ordem[alvo] = posicao;
        }

        GameObject fala = alvo.falasEmSequencia[Mathf.Min(posicao, alvo.falasEmSequencia.Count - 1)];
        if (fala == null) return;

        fala.SetActive(true);
        _falaAtual[grupo] = fala;
    }

    // ───────────────────────── Utilidades ─────────────────────────

    bool Falhar(int linha, string mensagem)
    {
        Debug.LogWarning($"[Linha {linha}] {mensagem}");
        AoErro?.Invoke(linha, mensagem);
        return false;
    }

    // ── Normalização: o que o jogador digita é comparado com o configurado sem ligar para maiúsculas,
    // acentos (opcional) e espaços/hífens/underscores (opcional). Símbolos como # @ % * continuam valendo. ──
    static bool _acentos = true, _separadores = true;

    static string Normalizar(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        s = s.Trim();

        if (_acentos)
        {
            string d = s.Normalize(System.Text.NormalizationForm.FormD);
            var sem = new System.Text.StringBuilder(d.Length);
            foreach (char c in d)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    sem.Append(c);
            s = sem.ToString().Normalize(System.Text.NormalizationForm.FormC);
        }

        var r = new System.Text.StringBuilder(s.Length);
        bool espaco = false;
        foreach (char c in s.ToLowerInvariant())
        {
            if (char.IsWhiteSpace(c) || (_separadores && (c == '-' || c == '_')))
            {
                if (!_separadores) espaco = true;       // sem ignorar separadores: vários espaços viram um só
                continue;
            }

            if (espaco && r.Length > 0) r.Append(' ');
            espaco = false;
            r.Append(c);
        }
        return r.ToString();
    }

    sealed class ComparadorNormalizado : IEqualityComparer<string>
    {
        public static readonly ComparadorNormalizado Instancia = new ComparadorNormalizado();
        public bool Equals(string a, string b) => Normalizar(a) == Normalizar(b);
        public int GetHashCode(string s) => Normalizar(s).GetHashCode();
    }

    static bool Igual(string a, string b) =>
        !string.IsNullOrEmpty(a) && Normalizar(a) == Normalizar(b);

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
            if (Normalizar(valor) == Normalizar(v)) return true;

        return false;
    }
}