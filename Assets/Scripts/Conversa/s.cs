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
    [Tooltip("Se marcado, também aceita clique do mouse pra avançar a linha, além da tecla.")]
    [SerializeField] private bool avancarComClique = true;
    [Tooltip("Se marcado, o clique só avança a fala quando for em cima do RÁDIO. Clicar no campo de comandos ou em outro lugar não passa a fala.\nSe nenhum rádio estiver ligado ao diálogo (campo Radio vazio em todos), o clique volta a valer em qualquer lugar.")]
    [SerializeField] private bool avancarSomenteClicandoNoRadio = true;
    [Tooltip("Escreve no Console quando um clique é recusado por ter sido fora do rádio (posição do mouse e objetos testados). Desmarque quando estiver tudo certo.")]
    [SerializeField] private bool depurarClique = true;
    [Tooltip("A tecla NÃO avança a fala enquanto o jogador está digitando num campo de texto (senão o Espaço de 'Hack Folder' passaria a fala).")]
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

    // true entre o começo do diálogo e o momento em que ele some da tela (ou é interrompido)
    private bool tocando = false;

    // O rádio da cena (qualquer DialogueBox que tenha o campo Radio preenchido o registra).
    // Assim as falas que não têm o rádio ligado também sabem onde o clique vale.
    private static AnimationR _radioDaCena;

    private void Start()
    {
        // se o diálogo já começou no OnEnable, não esconde a caixa
        if (!dialogoAtivado && caixaDeTexto != null)
            caixaDeTexto.SetActive(false);
    }

    // Todos os DialogueBox ativos agora. Serve para um novo diálogo interromper o que ainda está
    // escrevendo na MESMA caixa de texto (senão as duas falas se misturam letra por letra).
    private static readonly System.Collections.Generic.List<DialogueBox> _todos = new System.Collections.Generic.List<DialogueBox>();

    private void OnEnable()
    {
        if (!_todos.Contains(this)) _todos.Add(this);
        if (Radio != null) _radioDaCena = Radio;

        if (iniciarAoAtivar)
            IniciarDialogo();
    }

    private void OnDisable()
    {
        _todos.Remove(this);

        // As corrotinas morrem com o objeto desativado: limpa o estado para a fala poder tocar de novo.
        digitando = false;
        podeAvancar = false;
        fechando = false;
        tocando = false;
        esperandoAnimacao = false;
        digitacaoCoroutine = null;
        sumirSozinhoCoroutine = null;
        if (iniciarAoAtivar) dialogoAtivado = false;
    }

    // Para na hora este diálogo (sem apagar o texto: quem chamou vai escrever por cima).
    // 'dialogoAtivado' continua true de propósito: a fala interrompida conta como já usada e NÃO recomeça
    // sozinha (antes, um clique no rádio religava a primeira fala por cima da segunda).
    private void Interromper()
    {
        StopAllCoroutines();   // digitação, "sumir sozinho" e o atraso de fechar: nenhum pode mexer na caixa depois
        digitacaoCoroutine = null;
        sumirSozinhoCoroutine = null;
        digitando = false;
        podeAvancar = false;
        fechando = false;
        tocando = false;
        esperandoAnimacao = false;
    }

    private bool AlgumOutroTocando()
    {
        foreach (DialogueBox d in _todos)
            if (d != null && d != this && d.tocando) return true;
        return false;
    }

    // O clique foi em cima do rádio? Vale se acertar QUALQUER uma destas coisas, em qualquer objeto do rádio:
    // um Collider2D, um Collider 3D, o retângulo de um objeto de UI ou o sprite.
    // Os objetos testados são o do campo Radio e todo objeto que tenha um DialogueBox "Iniciar Com Clique"
    // (é nele que você colocou o collider para clicar no rádio).
    private bool ClicouNoRadio(AnimationR r)
    {
        Vector3 mouse = Input.mousePosition;
        Camera cam = Camera.main;

        var raizes = new System.Collections.Generic.List<Transform> { r.transform };
        foreach (DialogueBox d in _todos)
            if (d != null && d.iniciarComClique && !raizes.Contains(d.transform)) raizes.Add(d.transform);

        bool mediu = false;
        foreach (Transform raiz in raizes)
            if (ClicouEm(raiz, mouse, cam, ref mediu)) return true;

        if (!mediu) return true;     // nada mensurável (sem collider, sem sprite, sem UI): não bloqueia

        if (depurarClique)
        {
            string onde = cam != null ? cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, Mathf.Abs(cam.transform.position.z))).ToString() : "(sem Camera.main)";
            Debug.Log($"DialogueBox: clique fora do rádio. Tela={mouse}  Mundo={onde}  Objetos testados: {string.Join(", ", raizes.ConvertAll(t => t.name))}. " +
                      "Se o clique estava em cima do rádio, o collider/retângulo dele não cobre essa área.");
        }
        return false;
    }

    private static bool ClicouEm(Transform raiz, Vector3 mouse, Camera cam, ref bool mediu)
    {
        // 1) Objeto de UI (Canvas)
        var rt = raiz as RectTransform;
        Canvas canvas = raiz.GetComponentInParent<Canvas>();
        if (rt != null && canvas != null)
        {
            mediu = true;
            Camera camUI = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : (canvas.worldCamera != null ? canvas.worldCamera : cam);
            if (RectTransformUtility.RectangleContainsScreenPoint(rt, mouse, camUI)) return true;
        }

        if (cam == null) return false;

        // Z do clique no plano do jogo (z = 0): faz diferença só em câmera de perspectiva
        Vector3 m = new Vector3(mouse.x, mouse.y, Mathf.Abs(cam.transform.position.z));
        Vector3 mundo = cam.ScreenToWorldPoint(m);

        // 2) Collider2D (no objeto, nos filhos ou no pai)
        var c2s = new System.Collections.Generic.List<Collider2D>(raiz.GetComponentsInChildren<Collider2D>());
        Collider2D c2pai = raiz.GetComponentInParent<Collider2D>();
        if (c2pai != null && !c2s.Contains(c2pai)) c2s.Add(c2pai);
        foreach (Collider2D c2 in c2s)
        {
            mediu = true;
            if (c2.OverlapPoint(new Vector2(mundo.x, mundo.y))) return true;
        }

        // 3) Collider 3D
        foreach (Collider c3 in raiz.GetComponentsInChildren<Collider>())
        {
            mediu = true;
            if (c3.Raycast(cam.ScreenPointToRay(mouse), out _, 1000f)) return true;
        }

        // 4) Só o sprite
        foreach (SpriteRenderer sr in raiz.GetComponentsInChildren<SpriteRenderer>())
        {
            mediu = true;
            Vector3 p = mundo;
            p.z = sr.bounds.center.z;
            if (sr.bounds.Contains(p)) return true;
        }

        return false;
    }

    private bool CliqueValido()
    {
        if (!avancarSomenteClicandoNoRadio) return true;

        AnimationR r = _radioDaCena != null ? _radioDaCena : Radio;
        return r == null || ClicouNoRadio(r);
    }

    private static bool DigitandoNumCampo()
    {
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es == null || es.currentSelectedGameObject == null) return false;

        TMP_InputField campo = es.currentSelectedGameObject.GetComponent<TMP_InputField>();
        return campo != null && campo.isFocused;
    }

    private void InterromperOutrosDaMesmaCaixa()
    {
        for (int i = _todos.Count - 1; i >= 0; i--)
        {
            DialogueBox outro = _todos[i];
            if (outro == null || outro == this || !outro.tocando) continue;

            bool mesmaCaixa = (textoExibido != null && outro.textoExibido == textoExibido) ||
                              (caixaDeTexto != null && outro.caixaDeTexto == caixaDeTexto);
            if (mesmaCaixa) outro.Interromper();
        }
    }

    private void Update()
    {
        if (caixaDeTexto == null || !caixaDeTexto.activeSelf || !podeAvancar) return;

        bool pediuAvanco = (Input.GetKeyDown(teclaAvancar) && !DigitandoNumCampo()) ||
                           (avancarComClique && Input.GetMouseButtonDown(0) && CliqueValido());

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
        if (AlgumOutroTocando()) return;     // já tem uma fala na tela: o clique no rádio só passa a fala dela

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

        InterromperOutrosDaMesmaCaixa();   // a fala nova assume a caixa; a antiga para de escrever
        dialogoAtivado = true;
        tocando = true;

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
        for (int i = 1; i <= linha.Length; i++)
        {
            textoExibido.text = linha.Substring(0, i);   // sempre o começo DESTA linha: nunca mistura com outra
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
        tocando = false;
        caixaDeTexto.SetActive(false);
        textoExibido.text = "";

        AplicarRadioAoTerminar();
    }
}