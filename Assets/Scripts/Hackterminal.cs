using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using TMPro;

// Coloque este script SÓ no GameManager (o objeto com DontDestroyOnLoad).
// Ele NÃO mexe na câmera diretamente -- só chama CameraFocus.Focar() na câmera
// da cena atual. Como este objeto sobrevive entre cenas, ele se reconecta
// automaticamente ao Input Field e à câmera toda vez que uma cena carrega.
public class HackTerminal : MonoBehaviour
{
    [System.Serializable]
    public class AlvoHackeavel
    {
        [Tooltip("O nome que o jogador vai digitar no terminal (ex: 'Hack Folder', 'StartButton')")]
        public string comando;

        [Tooltip("Os GameObjects (sprite/popup/trigger de diálogo etc.) que vão aparecer/desaparecer juntos. Podem começar DESATIVADOS no Inspector.")]
        public List<GameObject> objetos = new List<GameObject>();

        [Tooltip("OPCIONAL: nome de outro comando que precisa ter sido o ÚLTIMO digitado antes deste funcionar.")]
        public string contextoNecessario;

         [Tooltip("OPCIONAL: valor exato exigido (ex: '1', '0', 'root', '5'). Deixe vazio para aceitar qualquer valor digitado.")]
        public string valorEsperado = "1";

        [Tooltip("Se marcado, a câmera foca no objeto ao ativar. Desmarque para comandos 'invisíveis' (flags) que não devem mover a câmera.")]
        public bool moverCamera = true;
    }

    [Header("Nome exato do Input Field na cena")]
    [Tooltip("Se não souber, deixe como está -- ele tenta achar o primeiro TMP_InputField da cena")]
    public string nomeDoInputField = "InputField (TMP)";

    [Header("Alvos Hackeáveis")]
    public List<AlvoHackeavel> alvos = new List<AlvoHackeavel>();

    private TMP_InputField commandInput;
    private CameraFocus cameraFocus;
    private HashSet<string> comandosAtivos = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, AlvoHackeavel> alvoAtivoPorComando = new Dictionary<string, AlvoHackeavel>(System.StringComparer.OrdinalIgnoreCase);

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        ConectarNaCenaAtual();
    }

    void OnSceneLoaded(Scene cena, LoadSceneMode modo)
    {
        ConectarNaCenaAtual();
    }

    void ConectarNaCenaAtual()
    {
        // Remove o listener antigo antes de reconectar, pra não duplicar
        if (commandInput != null)
        {
            commandInput.onEndEdit.RemoveListener(OnCommandSubmitted);
        }

        GameObject campoObj = GameObject.Find(nomeDoInputField);
        commandInput = campoObj != null ? campoObj.GetComponent<TMP_InputField>() : FindObjectOfType<TMP_InputField>();

        cameraFocus = FindObjectOfType<CameraFocus>();

        if (commandInput != null)
        {
            commandInput.onEndEdit.AddListener(OnCommandSubmitted);
        }
        else
        {
            Debug.LogWarning("HackTerminal: não achei nenhum TMP_InputField nesta cena.");
        }

        if (cameraFocus == null)
        {
            Debug.LogWarning("HackTerminal: não achei nenhum CameraFocus nesta cena.");
        }

        PopularAlvosPadrao();
    }

    // Preenche automaticamente 'Hack Folder' -> StartButton (exige 'conceito')
    // caso ainda não exista essa entrada na lista.
    void PopularAlvosPadrao()
    {
        bool jaTem = alvos.Exists(a =>
            !string.IsNullOrEmpty(a.comando) &&
            a.comando.Trim().Equals("Hack Folder", System.StringComparison.OrdinalIgnoreCase));

        if (jaTem) return;

        GameObject startButton = null;
        foreach (Transform t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "StartButton")
            {
                startButton = t.gameObject;
                break;
            }
        }

        if (startButton != null)
        {
            alvos.Add(new AlvoHackeavel
            {
                comando = "Hack Folder",
                objetos = new List<GameObject> { startButton },
                contextoNecessario = "conceito"
            });
            Debug.Log("HackTerminal: 'Hack Folder' configurado automaticamente para abrir StartButton.");
        }
        else
        {
            Debug.LogWarning("HackTerminal: não encontrei nenhum objeto chamado 'StartButton' nesta cena.");
        }
    }

    void OnCommandSubmitted(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        string[] parts = text.Split(':');
        if (parts.Length != 2)
        {
            Debug.LogWarning("Formato inválido. Use 'Nome: 1' ou 'Nome: 0'.");
            LimparCampo();
            return;
        }

        string nomeComando = parts[0].Trim();
        string valorStr = parts[1].Trim().Trim('"'); // aceita "root" ou root, tanto faz

        // Pega TODAS as entradas que usam esse mesmo nome de comando
        List<AlvoHackeavel> candidatos = alvos.FindAll(a =>
            !string.IsNullOrEmpty(a.comando) &&
            a.comando.Trim().Equals(nomeComando, System.StringComparison.OrdinalIgnoreCase));

        AlvoHackeavel alvo = null;

        if (candidatos.Count > 0)
        {
            if (valorStr == "0")
            {
                // Fecha exatamente a entrada que foi ativada por esse comando (se soubermos qual foi).
                if (!alvoAtivoPorComando.TryGetValue(nomeComando, out alvo))
                {
                    // Não sabemos qual foi -- provavelmente nunca abriu por aqui. Usa a primeira com objetos como fallback.
                    alvo = candidatos.Find(a => a.objetos != null && a.objetos.Count > 0);
                    if (alvo == null)
                        alvo = candidatos[0];
                }
            }
            else
            {
                // Para ABRIR, sim, olha contexto e valor esperado
                alvo = candidatos.Find(a => ContextoSatisfeito(a) && ValorCombina(a, valorStr));

                if (alvo == null)
                {
                    // Nenhuma bateu -- damos um aviso mais específico se o motivo for contexto faltando
                    AlvoHackeavel semContexto = candidatos.Find(a => !ContextoSatisfeito(a));
                    if (semContexto != null)
                    {
                        Debug.LogWarning($"Você precisa ativar '{semContexto.contextoNecessario}' antes de usar '{nomeComando}' desse jeito.");
                    }
                    else
                    {
                        Debug.LogWarning($"Valor incorreto para '{nomeComando}'. Esperado algo diferente.");
                    }

                    LimparCampo();
                    return;
                }
            }
        }

    // Continua tratando "0" como desativar, qualquer outra coisa como ativar
    bool estado = valorStr != "0";

    List<GameObject> objetosAlvo = (alvo != null && alvo.objetos != null && alvo.objetos.Count > 0)
        ? alvo.objetos
        : null;

    if (objetosAlvo == null)
    {
        GameObject encontrado = GameObject.Find(nomeComando);
        if (encontrado != null)
            objetosAlvo = new List<GameObject> { encontrado };
    }

    if (objetosAlvo == null || objetosAlvo.Count == 0)
    {
        Debug.LogWarning($"NÃO ACHEI nenhum objeto para o comando: {nomeComando}");
        LimparCampo();
        return;
    }

    GameObject primeiroValido = null;

    foreach (GameObject obj in objetosAlvo)
    {
        if (obj == null) continue;

        obj.SetActive(estado);
        Debug.Log($"'{obj.name}' foi {(estado ? "ativado" : "desativado")} pelo comando '{nomeComando}'.");

        if (primeiroValido == null)
            primeiroValido = obj;
    }

    if (estado)
    {
        comandosAtivos.Add(nomeComando);
        if (alvo != null)
            alvoAtivoPorComando[nomeComando] = alvo;
    }
    else
    {
        comandosAtivos.Remove(nomeComando);
        alvoAtivoPorComando.Remove(nomeComando);
    }

    if (estado && cameraFocus != null && (alvo == null || alvo.moverCamera) && primeiroValido != null)
    {
        cameraFocus.Focar(primeiroValido);
    }

    LimparCampo();
}

    void LimparCampo()
    {
        if (commandInput == null) return;
        commandInput.text = "";
        commandInput.ActivateInputField();
    }

    // Confere se todos os requisitos de contexto desse alvo específico já foram ativados
    private bool ContextoSatisfeito(AlvoHackeavel a)
    {
        if (string.IsNullOrEmpty(a.contextoNecessario)) return true;

        string[] requisitos = a.contextoNecessario.Split(',');
        foreach (string req in requisitos)
        {
            string requisito = req.Trim();
            if (string.IsNullOrEmpty(requisito)) continue;

            if (!comandosAtivos.Contains(requisito))
                return false;
        }

        return true;
    }

    // Confere se o valor digitado bate com o valor esperado desse alvo específico
    private bool ValorCombina(AlvoHackeavel a, string valorStr)
    {
        // "0" sempre serve para FECHAR, mesmo que o valor exigido pra abrir seja outro (ex: "root")
        if (valorStr == "0") return true;

        if (string.IsNullOrEmpty(a.valorEsperado)) return true;
        return valorStr.Equals(a.valorEsperado, System.StringComparison.OrdinalIgnoreCase);
    }
}