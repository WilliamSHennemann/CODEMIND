using System.Collections;
using UnityEngine;
using TMPro;

public enum RadioAoComecar { NaoMexer, CongelarNoFrameAtual, LigarAnimando }
public enum RadioAoTerminar { NaoMexer, CongelarNoFrameAtual, VoltarAoPrimeiroFrame }

public class DialogueBox : MonoBehaviour
{
    [Header("Quando o diálogo começa (cada um é opcional, pode marcar os dois)")]
    [Tooltip("Começa sozinho quando o objeto é ativado (início da fase).")]
    [SerializeField] private bool iniciarAoAtivar = false;
    [Tooltip("Começa quando clica no objeto (precisa de um Collider).")]
    [SerializeField] private bool iniciarComClique = true;

    [Header("Texto")]
    [SerializeField] private GameObject caixaDeTexto;
    [SerializeField] private TMP_Text textoExibido;
    [SerializeField] private string[] linhas;
    [SerializeField] private float atrasoPorCaractere = 0.03f;
    [SerializeField] private float atrasoParaSumir = 5f;

    [Header("Avanço manual")]
    [Tooltip("Se marcado, também aceita clique do mouse em qualquer lugar pra avançar a linha, além da tecla.")]
    [SerializeField] private bool avancarComClique = true;
    [SerializeField] private KeyCode teclaAvancar = KeyCode.Space;

    [Header("Rádio (opcional)")]
    [SerializeField] private AnimationR Radio;
    [Tooltip("Se marcado, o clique só funciona depois que o rádio já ligou (comportamento antigo).")]
    [SerializeField] private bool exigirRadioLigadoParaClicar = true;
    [Tooltip("O que fazer com o rádio quando o diálogo começa. CongelarNoFrameAtual = para no sprite em que estava.")]
    [SerializeField] private RadioAoComecar radioAoComecarDialogo = RadioAoComecar.CongelarNoFrameAtual;
    [Tooltip("O que fazer com o rádio quando o diálogo termina. NaoMexer = ele continua como está (não some).")]
    [SerializeField] private RadioAoTerminar radioAoTerminarDialogo = RadioAoTerminar.NaoMexer;

    private int linhaAtual = 0;
    private bool dialogoAtivado = false;

    private bool digitando = false;
    private bool podeAvancar = false;
    private Coroutine digitacaoCoroutine;

    private void Start()
    {
        // se o diálogo já começou no OnEnable, não esconde a caixa
        if (!dialogoAtivado && caixaDeTexto != null)
            caixaDeTexto.SetActive(false);
    }

    private void OnEnable()
    {
        if (iniciarAoAtivar)
            IniciarDialogo();
    }

    private void Update()
    {
        if (caixaDeTexto == null || !caixaDeTexto.activeSelf || !podeAvancar) return;

        bool pediuAvanco = Input.GetKeyDown(teclaAvancar) ||
                           (avancarComClique && Input.GetMouseButtonDown(0));

        if (!pediuAvanco) return;

        if (digitando)
        {
            // clicou/apertou enquanto ainda tava digitando -> completa a linha na hora
            if (digitacaoCoroutine != null) StopCoroutine(digitacaoCoroutine);
            textoExibido.text = linhas[linhaAtual];
            digitando = false;
        }
        else
        {
            ProximaLinha();
        }
    }

    private void OnMouseDown()
    {
        if (!iniciarComClique || dialogoAtivado) return;

        if (exigirRadioLigadoParaClicar && Radio != null && !Radio.JaLigou) return;

        IniciarDialogo();
    }

    private void IniciarDialogo()
    {
        if (dialogoAtivado) return;
        if (linhas == null || linhas.Length == 0) return;

        dialogoAtivado = true;

        AplicarRadioAoComecar();

        caixaDeTexto.SetActive(true);
        linhaAtual = 0;
        podeAvancar = false;
        MostrarLinha(linhaAtual);
        StartCoroutine(LiberarInputProximoFrame());
    }

    private void AplicarRadioAoComecar()
    {
        if (Radio == null) return;

        switch (radioAoComecarDialogo)
        {
            case RadioAoComecar.CongelarNoFrameAtual:
                Radio.PararLoop();
                break;
            case RadioAoComecar.LigarAnimando:
                if (!Radio.EstaTocando) Radio.ComecarLoop();
                break;
        }
    }

    private void AplicarRadioAoTerminar()
    {
        if (Radio == null) return;

        switch (radioAoTerminarDialogo)
        {
            case RadioAoTerminar.CongelarNoFrameAtual:
                Radio.PararLoop();
                break;
            case RadioAoTerminar.VoltarAoPrimeiroFrame:
                Radio.ResetarParaPrimeiroFrame();
                break;
        }
    }

    private IEnumerator LiberarInputProximoFrame()
    {
        // evita que o mesmo clique que abriu o diálogo já conte como "avançar"
        yield return null;
        podeAvancar = true;
    }

    private void MostrarLinha(int indice)
    {
        if (digitacaoCoroutine != null) StopCoroutine(digitacaoCoroutine);
        digitacaoCoroutine = StartCoroutine(DigitarLinha(linhas[indice]));
    }

    private IEnumerator DigitarLinha(string linha)
    {
        digitando = true;
        textoExibido.text = "";
        foreach (char c in linha)
        {
            textoExibido.text += c;
            yield return new WaitForSeconds(atrasoPorCaractere);
        }
        digitando = false;
    }

    public void ProximaLinha()
    {
        linhaAtual++;
        if (linhaAtual < linhas.Length)
        {
            MostrarLinha(linhaAtual);
        }
        else
        {
            podeAvancar = false;
            StartCoroutine(FecharAposDelay());
        }
    }

    private IEnumerator FecharAposDelay()
    {
        yield return new WaitForSeconds(atrasoParaSumir);
        caixaDeTexto.SetActive(false);
        textoExibido.text = "";

        AplicarRadioAoTerminar();
    }
}