using System.Collections;
using UnityEngine;
using TMPro;

public enum TipoDeGatilho { Clique, AoAtivar }

public class DialogueBox : MonoBehaviour
{
    [SerializeField] private TipoDeGatilho gatilho = TipoDeGatilho.Clique;
    [SerializeField] private GameObject caixaDeTexto;
    [SerializeField] private TMP_Text textoExibido;
    [SerializeField] private string[] linhas;
    [SerializeField] private float atrasoInicial = 5f;
    [SerializeField] private float atrasoPorCaractere = 0.03f;
    [SerializeField] private float atrasoParaSumir = 5f;

    [Header("Avanço manual")]
    [Tooltip("Se marcado, também aceita clique do mouse em qualquer lugar pra avançar a linha, além da tecla.")]
    [SerializeField] private bool avancarComClique = true;
    [SerializeField] private KeyCode teclaAvancar = KeyCode.Space;

    [Tooltip("Só usado se o gatilho for Clique e precisar ligar uma animação junto")]
    [SerializeField] private AnimationR Radio;

    private int linhaAtual = 0;
    private bool radioLigado = false;
    private bool dialogoAtivado = false;

    private bool digitando = false;
    private bool podeAvancar = false;
    private Coroutine digitacaoCoroutine;

    private void Start()
    {
        if (gatilho == TipoDeGatilho.Clique)
        {
            caixaDeTexto.SetActive(false);

            if (Radio != null)
                StartCoroutine(LigarRadioAposDelay());
            else
                radioLigado = true;
        }
    }

    private void OnEnable()
    {
        if (gatilho == TipoDeGatilho.AoAtivar && !dialogoAtivado)
        {
            dialogoAtivado = true;

            if (Radio != null)
                Radio.ComecarLoop();

            IniciarDialogo();
        }
    }

    private void Update()
    {
        if (!caixaDeTexto.activeSelf || !podeAvancar) return;

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

    private IEnumerator LigarRadioAposDelay()
    {
        yield return new WaitForSeconds(atrasoInicial);
        Radio.ComecarLoop();
        radioLigado = true;
    }

    private void OnMouseDown()
    {
        if (gatilho != TipoDeGatilho.Clique) return;
        if (!radioLigado || dialogoAtivado) return;
        dialogoAtivado = true;
        IniciarDialogo();
    }

    private void IniciarDialogo()
    {
        caixaDeTexto.SetActive(true);
        linhaAtual = 0;
        podeAvancar = false;
        MostrarLinha(linhaAtual);
        StartCoroutine(LiberarInputProximoFrame());
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

        if (Radio != null)
            Radio.PararLoop();
    }
}