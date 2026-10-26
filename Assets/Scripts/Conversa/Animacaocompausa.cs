using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Animação independente (não precisa do DialogueBox).
//
// Funcionamento:
//   1. Toca os frames do 0 até o "Frame De Pausa".
//   2. Congela nesse frame ENQUANTO o "Texto Observado" estiver ativo na tela.
//   3. Quando o texto some, continua do frame seguinte até o último.
//   4. Termina e (opcionalmente) some.
//
// Funciona com SpriteRenderer (mundo) ou Image (UI). Coloque no mesmo objeto da animação
// ou arraste o componente desejado nos campos.
//
// Sem Texto Observado, a animação toca direto do começo ao fim, sem pausar.
public class AnimacaoComPausa : MonoBehaviour
{
    [Header("Onde desenhar (use UM)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Image imagemUI;

    [Header("Animação")]
    [SerializeField] private Sprite[] frames;
    [Tooltip("Segundos por frame.")]
    [SerializeField, Min(0.001f)] private float velocidade = 0.08f;

    [Header("Pegar os frames automaticamente (opcional)")]
    [Tooltip("Arraste AQUI o AnimationClip OU a imagem (sprite sheet) já fatiada. O campo 'Frames' é preenchido sozinho, na ordem certa.\n" +
             "Para voltar a editar os frames na mão, esvazie este campo.")]
    [SerializeField] private Object fonteDosFrames;
    [Tooltip("Só vale com AnimationClip: usa o FPS do clip como velocidade.")]
    [SerializeField] private bool usarVelocidadeDoClip = true;

    [Header("Pausa")]
    [Tooltip("O texto (GameObject) que segura a animação. Enquanto ele estiver ativo, a animação fica parada no frame de pausa.")]
    [SerializeField] private GameObject textoObservado;
    [Tooltip("Índice do frame onde congela (0 = primeiro frame).")]
    [SerializeField, Min(0)] private int frameDePausa = 3;
    [Tooltip("Se o texto ainda não apareceu quando a animação chega no frame de pausa, espera até este tempo por ele. 0 = não espera (continua direto).")]
    [SerializeField, Min(0f)] private float esperaMaxPeloTexto = 3f;
    [Tooltip("Se marcado, congela no frame de pausa ATÉ o texto aparecer (não importa quanto demore) e depois até ele sumir. Ignora o tempo máximo acima.")]
    [SerializeField] private bool esperarTextoParaSempre = true;
    [Tooltip("Escreve no Console o que a animação está fazendo (ajuda a descobrir por que ela não pausa).")]
    [SerializeField] private bool mostrarLogs = true;

    [Header("Conflitos")]
    [Tooltip("Desliga o Animator deste objeto ao começar. Ele troca o sprite sozinho e impede a pausa de funcionar.")]
    [SerializeField] private bool desligarAnimator = true;

    [Header("Início / fim")]
    [SerializeField] private bool tocarAoAtivar = true;
    [SerializeField, Min(0f)] private float atrasoInicial = 0f;
    [Tooltip("Some (desativa este objeto) depois do último frame.")]
    [SerializeField] private bool esconderAoTerminar = true;
    [SerializeField, Min(0f)] private float atrasoParaEsconder = 0f;

    Coroutine _rotina;
    bool _liberadoManual;

    public bool Tocando => _rotina != null;

    /// <summary>True quando a animação chegou ao frame de pausa (ou terminou, se não tem pausa). O DialogueBox usa isso para começar.</summary>
    public bool ChegouNaPausa { get; private set; }

#if UNITY_EDITOR
    // Só no Editor: lê os sprites e grava no array 'frames' (que fica salvo, então funciona no build).
    void OnValidate()
    {
        if (fonteDosFrames == null) return;

        var lista = new System.Collections.Generic.List<Sprite>();
        float fps = 0f;

        if (fonteDosFrames is AnimationClip clip)
        {
            foreach (var bind in UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                foreach (var key in UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, bind))
                    if (key.value is Sprite s) lista.Add(s);

                if (lista.Count > 0) break;   // usa a primeira curva de sprites
            }
            fps = clip.frameRate;
        }
        else
        {
            string caminho = UnityEditor.AssetDatabase.GetAssetPath(fonteDosFrames);
            foreach (Object o in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(caminho))
                if (o is Sprite s) lista.Add(s);

            lista.Sort((a, b) =>
            {
                int na = NumeroFinal(a.name), nb = NumeroFinal(b.name);
                return na != nb ? na.CompareTo(nb) : string.CompareOrdinal(a.name, b.name);
            });
        }

        if (lista.Count == 0)
        {
            Debug.LogWarning("AnimacaoComPausa: não achei sprites em '" + fonteDosFrames.name + "'. Use um AnimationClip de sprites ou uma imagem com Sprite Mode = Multiple (já fatiada).", this);
            return;
        }

        frames = lista.ToArray();
        if (usarVelocidadeDoClip && fps > 0f) velocidade = 1f / fps;
    }

    // "run_12" -> 12 (para ordenar 2 antes de 10)
    static int NumeroFinal(string nome)
    {
        int i = nome.Length;
        while (i > 0 && char.IsDigit(nome[i - 1])) i--;
        return i < nome.Length && int.TryParse(nome.Substring(i), out int v) ? v : -1;
    }
#endif

    void Reset()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        imagemUI = GetComponent<Image>();
    }

    void OnEnable()
    {
        // Um Animator no mesmo objeto troca o sprite a cada frame e passa por cima da pausa.
        if (desligarAnimator)
        {
            Animator anim = GetComponent<Animator>();
            if (anim != null)
            {
                anim.enabled = false;
                if (mostrarLogs) Debug.Log($"AnimacaoComPausa '{name}': desliguei o Animator deste objeto (ele sobrescrevia o sprite).", this);
            }
        }

        if (tocarAoAtivar) Tocar();
        else Mostrar(0);
    }

    void OnDisable() => _rotina = null;   // as corrotinas param sozinhas ao desativar

    // Pode ser chamado por outro script ou UnityEvent.
    public void Tocar()
    {
        if (frames == null || frames.Length == 0) return;

        if (_rotina != null) StopCoroutine(_rotina);
        _liberadoManual = false;
        ChegouNaPausa = false;
        _rotina = StartCoroutine(Rodar());
    }

    // Opcional: força a animação a continuar mesmo com o texto ainda na tela.
    public void Liberar() => _liberadoManual = true;

    IEnumerator Rodar()
    {
        Mostrar(0);
        if (atrasoInicial > 0f) yield return new WaitForSeconds(atrasoInicial);

        if (textoObservado == null && mostrarLogs)
            Debug.LogWarning($"AnimacaoComPausa '{name}': o campo 'Texto Observado' está vazio, então a animação toca direto, sem pausar.", this);

        int pausa = textoObservado != null ? Mathf.Clamp(frameDePausa, 0, frames.Length - 1) : -1;
        var espera = new WaitForSeconds(velocidade);

        for (int i = 0; i < frames.Length; i++)
        {
            Mostrar(i);

            if (i == pausa)
            {
                ChegouNaPausa = true;     // avisa o DialogueBox: pode começar o texto agora
                yield return SegurarAteTextoSumir(i);
            }

            if (i < frames.Length - 1) yield return espera;
        }

        _rotina = null;
        ChegouNaPausa = true;             // sem pausa configurada: libera quem estiver esperando no fim

        if (esconderAoTerminar)
        {
            if (atrasoParaEsconder > 0f) yield return new WaitForSeconds(atrasoParaEsconder);
            gameObject.SetActive(false);
        }
    }

    bool _avisouConflito;

    // Roda no FIM do frame (depois de qualquer Animator/script) e põe o sprite congelado de volta.
    IEnumerator Congelar(int frame)
    {
        yield return new WaitForEndOfFrame();

        Sprite esperado = frames[frame];
        Sprite atual = spriteRenderer != null ? spriteRenderer.sprite : (imagemUI != null ? imagemUI.sprite : null);

        if (atual != esperado && !_avisouConflito)
        {
            _avisouConflito = true;
            Debug.LogWarning($"AnimacaoComPausa '{name}': ALGO trocou o sprite durante a pausa (era '{esperado.name}', virou '{(atual != null ? atual.name : "nada")}'). " +
                             "Procure outro script, Animator ou Animation no objeto, nos pais ou nos filhos. Estou reafirmando o frame congelado.", this);
        }

        Mostrar(frame);
    }

    IEnumerator SegurarAteTextoSumir(int frame)
    {
        if (mostrarLogs)
            Debug.Log($"AnimacaoComPausa '{name}': chegou no frame de pausa. Texto '{textoObservado.name}' ativo agora? {textoObservado.activeInHierarchy}", this);

        // 1) se o texto ainda não apareceu, espera por ele
        float esperado = 0f;
        while (!_liberadoManual && !textoObservado.activeInHierarchy
               && (esperarTextoParaSempre || esperado < esperaMaxPeloTexto))
        {
            esperado += Time.deltaTime;
            yield return Congelar(frame);
        }

        if (!textoObservado.activeInHierarchy && !_liberadoManual)
        {
            if (mostrarLogs) Debug.LogWarning($"AnimacaoComPausa '{name}': o texto não apareceu a tempo, então a animação continuou.", this);
            yield break;
        }

        if (mostrarLogs) Debug.Log($"AnimacaoComPausa '{name}': texto na tela, animação congelada.", this);

        // 2) enquanto o texto estiver na tela, fica congelado
        while (!_liberadoManual && textoObservado != null && textoObservado.activeInHierarchy)
            yield return Congelar(frame);

        if (mostrarLogs) Debug.Log($"AnimacaoComPausa '{name}': texto sumiu, continuando a animação.", this);
    }

    void Mostrar(int i)
    {
        if (frames == null || frames.Length == 0) return;
        Sprite s = frames[Mathf.Clamp(i, 0, frames.Length - 1)];

        if (spriteRenderer != null) spriteRenderer.sprite = s;
        if (imagemUI != null) imagemUI.sprite = s;
    }
}