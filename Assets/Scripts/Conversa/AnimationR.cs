using System.Collections;
using UnityEngine;

public class AnimationR : MonoBehaviour
{
    [SerializeField] private SpriteRenderer Radio;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float velocidadeAnimacao = 0.08f;

    [Header("Início automático (opcional)")]
    [Tooltip("Se marcado, o rádio começa a animar sozinho quando a fase começa, sem precisar de clique.")]
    [SerializeField] private bool comecarAoIniciarFase = false;
    [SerializeField] private float atrasoInicial = 0f;

    private Coroutine loopAtual;
    private Coroutine esperaInicial;

    /// <summary>True depois que o rádio foi ligado pelo menos uma vez (mesmo que depois tenha sido congelado).</summary>
    public bool JaLigou { get; private set; }

    /// <summary>True enquanto a animação está rodando (false quando congelado).</summary>
    public bool EstaTocando => loopAtual != null;

    private void Awake()
    {
        // fica parado no frame 1 até ser chamado
        if (Radio != null && frames != null && frames.Length > 0)
            Radio.sprite = frames[0];
    }

    private void Start()
    {
        if (comecarAoIniciarFase)
            esperaInicial = StartCoroutine(ComecarDepoisDoAtraso());
    }

    private void OnDisable()
    {
        // as coroutines morrem junto com o objeto, então limpa as referências
        loopAtual = null;
        esperaInicial = null;
    }

    private IEnumerator ComecarDepoisDoAtraso()
    {
        if (atrasoInicial > 0f)
            yield return new WaitForSeconds(atrasoInicial);

        esperaInicial = null;
        ComecarLoop();
    }

    public void ComecarLoop()
    {
        CancelarEspera();

        if (loopAtual != null)
            StopCoroutine(loopAtual);

        JaLigou = true;
        loopAtual = StartCoroutine(LoopPingPong());
    }

    /// <summary>Para a animação e deixa o rádio congelado no sprite em que estava.</summary>
    public void PararLoop()
    {
        CancelarEspera();

        if (loopAtual != null)
        {
            StopCoroutine(loopAtual);
            loopAtual = null;
        }
        // não mexe no sprite: ele fica exatamente no frame atual
    }

    /// <summary>Para a animação e volta pro primeiro frame (use só se quiser mesmo resetar).</summary>
    public void ResetarParaPrimeiroFrame()
    {
        PararLoop();

        if (Radio != null && frames != null && frames.Length > 0)
            Radio.sprite = frames[0];
    }

    private void CancelarEspera()
    {
        if (esperaInicial != null)
        {
            StopCoroutine(esperaInicial);
            esperaInicial = null;
        }
    }

    private IEnumerator LoopPingPong()
    {
        if (Radio == null || frames == null || frames.Length == 0)
        {
            loopAtual = null;
            yield break;
        }

        while (true)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                Radio.sprite = frames[i];
                yield return new WaitForSeconds(velocidadeAnimacao);
            }
            for (int i = frames.Length - 2; i >= 0; i--)
            {
                Radio.sprite = frames[i];
                yield return new WaitForSeconds(velocidadeAnimacao);
            }
        }
    }
}