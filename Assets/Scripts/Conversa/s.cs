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
    [Tooltip("Tempo entre o clique que passa da ÚLTIMA linha e o texto sumir.")]
    [SerializeField] private float atrasoParaSumir = 5f;

    [Header("Sumir sozinho (opcional)")]
    [Tooltip("Se marcado, quando a ÚLTIMA linha termina de aparecer o texto some sozinho depois do tempo abaixo, sem precisar clicar.\nDesmarcado = só some depois do clique (como antes).")]
    [SerializeField] private bool sumirSozinhoNaUltimaLinha = false;
    [Tooltip("Segundos que a última linha fica na tela antes de sumir sozinha. Se o jogador clicar antes, o clique vale normalmente.")]
    [SerializeField, Min(0f)] private float tempoParaSumirSozinho = 3f;

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

    [Header("Esperar uma animação (opcional)")]
    [Tooltip("Arraste aqui o AnimacaoComPausa. O diálogo só começa quando a animação chegar no frame de pausa.\nDeixe VAZIO para o diálogo começar normalmente (como antes, com o rádio).")]
    [SerializeField] private AnimacaoComPausa animacaoParaEsperar;
    [Tooltip("Segurança: se a animação não chegar no frame de pausa em X segundos, o diálogo começa mesmo assim. 0 = espera para sempre.")]
    [SerializeField, Min(0f)] private float tempoMaxEsperaAnimacao = 10f;

    private bool esperandoAnimacao = false;
    private bool fechando = false;
    private Coroutine sumirSozinhoCoroutine;
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
            AoTerminarDeDigitar();
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
        if (dialogoAtivado || esperandoAnimacao) return;
        if (linhas == null || linhas.Length == 0) return;

        // Opcional: espera a animação chegar no frame de pausa antes de começar
        if (animacaoParaEsperar != null && !animacaoParaEsperar.ChegouNaPausa)
        {
            StartCoroutine(EsperarAnimacaoEComecar());
            return;
        }

        ComecarDialogo();
    }

    private IEnumerator EsperarAnimacaoEComecar()
    {
        esperandoAnimacao = true;
        float esperado = 0f;

        while (animacaoParaEsperar != null && !animacaoParaEsperar.ChegouNaPausa
               && (tempoMaxEsperaAnimacao <= 0f || esperado < tempoMaxEsperaAnimacao))
        {
            esperado += Time.deltaTime;
            yield return null;
        }

        esperandoAnimacao = false;
        ComecarDialogo();
    }

    private void ComecarDialogo()
    {
        if (dialogoAtivado) return;

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
        AoTerminarDeDigitar();
    }

    // Chamado quando uma linha termina de aparecer (sozinha ou completada com clique).
    private void AoTerminarDeDigitar()
    {
        bool ultima = linhaAtual >= linhas.Length - 1;
        if (!ultima || !sumirSozinhoNaUltimaLinha || fechando || sumirSozinhoCoroutine != null) return;

        sumirSozinhoCoroutine = StartCoroutine(SumirSozinho());
    }

    private IEnumerator SumirSozinho()
    {
        yield return new WaitForSeconds(tempoParaSumirSozinho);

        sumirSozinhoCoroutine = null;
        if (fechando) yield break;      // o jogador já clicou e o fechamento normal está em andamento

        fechando = true;
        podeAvancar = false;
        Fechar();
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
            if (fechando) return;
            fechando = true;
            podeAvancar = false;

            if (sumirSozinhoCoroutine != null)
            {
                StopCoroutine(sumirSozinhoCoroutine);
                sumirSozinhoCoroutine = null;
            }

            StartCoroutine(FecharAposDelay());
        }
    }

    private IEnumerator FecharAposDelay()
    {
        yield return new WaitForSeconds(atrasoParaSumir);
        Fechar();
    }

    private void Fechar()
    {
        caixaDeTexto.SetActive(false);
        textoExibido.text = "";

        AplicarRadioAoTerminar();
    }
}