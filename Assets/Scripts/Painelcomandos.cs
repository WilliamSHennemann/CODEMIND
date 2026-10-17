using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Botão ao lado da barra de comandos que abre uma lista ("caderno") dos comandos que o jogador
// já usou COM SUCESSO. Clicar num comando o coloca na caixa de texto (não executa).
//
// ── Montagem na cena ─────────────────────────────────────────────────────────────
//  1. O botão ao lado da barra                         -> campo "Botao Abrir"
//  2. Um painel (deixe DESATIVADO no início)           -> campo "Painel"
//  3. Dentro do painel, um objeto vazio com VerticalLayoutGroup
//       Child Control Width ✓  |  Child Control Height ✓
//       Child Force Expand Width ✓  |  Child Force Expand Height ✗
//     (se a lista puder ficar grande, coloque esse objeto dentro de um Scroll View
//      e dê a ele um ContentSizeFitter com Vertical Fit = Preferred Size)
//                                                       -> campo "Conteudo"
//  4. Este script em um objeto que fique SEMPRE ATIVO (pode ser o próprio botão).
//
// Os itens da lista são criados por código; use "Fonte" para usar a mesma fonte pixel do jogo.
public class PainelComandos : MonoBehaviour
{
    [Header("Ligações")]
    [SerializeField] private Button botaoAbrir;
    [SerializeField] private GameObject painel;
    [SerializeField] private RectTransform conteudo;

    [Tooltip("OPCIONAL: o seu próprio modelo de item (ex.: o Panel com o texto dentro). O script copia esse modelo para cada comando.\n" +
             "Precisa ter um TextMeshPro dentro. Se tiver um botão filho chamado 'Apagar', ele vira o X; senão o X é criado por código.\n" +
             "Pode ficar dentro do Conteudo: ele é desativado sozinho. Deixe vazio para usar o item criado por código.")]
    [SerializeField] private GameObject modeloDoItem;

    [Tooltip("OPCIONAL: um objeto (ex.: um texto 'nenhum comando ainda') que aparece enquanto a lista está vazia.")]
    [SerializeField] private GameObject avisoVazio;

    [Header("Comportamento")]
    [Tooltip("Fecha a lista depois que o jogador escolhe um comando.")]
    [SerializeField] private bool fecharAoEscolher = true;

    [Tooltip("Mostra um X em cada comando para apagá-lo da lista.")]
    [SerializeField] private bool permitirApagar = true;

    [Tooltip("OPCIONAL: botão 'limpar tudo', que apaga todos os comandos da lista.")]
    [SerializeField] private Button botaoLimparTudo;

    [Header("Aparência dos itens")]
    [SerializeField] private TMP_FontAsset fonte;
    [Tooltip("Todos os tamanhos abaixo estão na MESMA unidade do seu Canvas. Se o painel for pequeno (ex.: 21 de largura), use valores pequenos (altura 3, fonte 2).")]
    [SerializeField, Min(0.01f)] private float tamanhoDaFonte = 18f;
    [SerializeField] private Color corDoTexto = Color.white;
    [SerializeField] private Color corDoItem = new Color(0f, 0f, 0f, 0.35f);
    [Tooltip("Cor do fundo do botão X. Deixe o A (alfa) em 0 para o fundo ficar transparente: só o X aparece, e o clique continua funcionando.")]
    [SerializeField] private Color corDoBotaoApagar = new Color(0.78f, 0.33f, 0.27f, 0f);
    [Tooltip("Cor da letra X.")]
    [SerializeField] private Color corDoX = new Color(1f, 0.45f, 0.4f);
    [Tooltip("Se o comando for comprido demais para caber ao lado do X, o X vai para FORA do item, na lateral direita, em vez de ficar em cima do texto.")]
    [SerializeField] private bool xParaOLadoSeNaoCouber = true;
    [SerializeField, Min(0.01f)] private float alturaDoItem = 28f;
    [Tooltip("Largura do quadradinho X. 0 = igual à altura do item.")]
    [SerializeField, Min(0f)] private float larguraDoApagar = 0f;
    [SerializeField, Min(0f)] private float margemLateral = 8f;

    [Header("Largura da lista")]
    [Tooltip("Cada item informa quanto precisa de largura (texto + X + margens). Com o Content Size Fitter do Panel em Horizontal Fit = Preferred Size, o Panel cresce para a direita acompanhando o comando mais comprido.")]
    [SerializeField] private bool painelCresceNaLargura = true;
    [Tooltip("Largura mínima de cada item (e portanto do Panel). 0 = sem mínimo.")]
    [SerializeField, Min(0f)] private float larguraMinimaDoItem = 0f;

    // O que precisa ser medido depois que o layout calcula a largura dos itens (posição do X)
    class AjusteDoX
    {
        public RectTransform raiz, apagar;
        public TMP_Text texto;
        public float largura;
    }

    readonly List<GameObject> _itens = new List<GameObject>();
    readonly List<AjusteDoX> _ajustes = new List<AjusteDoX>();
    bool _assinado;

    float LarguraApagar => larguraDoApagar > 0f ? larguraDoApagar : alturaDoItem;

    // ───────────────────────── Ciclo de vida ─────────────────────────

    void OnEnable()
    {
        if (botaoAbrir != null) botaoAbrir.onClick.AddListener(Alternar);
        if (botaoLimparTudo != null) botaoLimparTudo.onClick.AddListener(LimparTudo);
        Assinar();
    }

    // Se o HackTerminal ainda não existia quando este objeto ligou (ordem de Awake), tenta de novo aqui.
    void Start()
    {
        if (!_assinado) Assinar();
    }

    void OnDisable()
    {
        if (botaoAbrir != null) botaoAbrir.onClick.RemoveListener(Alternar);
        if (botaoLimparTudo != null) botaoLimparTudo.onClick.RemoveListener(LimparTudo);
        Desassinar();
    }

    void Assinar()
    {
        if (_assinado || HackTerminal.Instancia == null) return;

        HackTerminal.Instancia.AoMudarComandosUsados += Atualizar;
        _assinado = true;
        Atualizar();
    }

    void Desassinar()
    {
        if (_assinado && HackTerminal.Instancia != null)
            HackTerminal.Instancia.AoMudarComandosUsados -= Atualizar;

        _assinado = false;
    }

    // ───────────────────────── Abrir / fechar ─────────────────────────

    public void Alternar()
    {
        if (painel != null) Abrir(!painel.activeSelf);
    }

    public void Abrir(bool abrir)
    {
        if (painel == null) return;

        painel.SetActive(abrir);
        if (abrir) Atualizar();
    }

    // ───────────────────────── Lista ─────────────────────────

    void Atualizar()
    {
        if (conteudo == null) return;

        // O modelo que está na cena é só molde: nunca aparece sozinho
        if (modeloDoItem != null && modeloDoItem.scene.IsValid() && modeloDoItem.activeSelf)
            modeloDoItem.SetActive(false);

        foreach (GameObject antigo in _itens)
        {
            if (antigo == null) continue;
            antigo.SetActive(false);       // some do layout já neste frame (o Destroy só vale no fim dele)
            Destroy(antigo);
        }
        _itens.Clear();
        _ajustes.Clear();

        IReadOnlyList<string> comandos = HackTerminal.Instancia != null ? HackTerminal.Instancia.ComandosUsados : null;
        int total = comandos != null ? comandos.Count : 0;

        if (avisoVazio != null) avisoVazio.SetActive(total == 0);

        for (int i = 0; i < total; i++)
            _itens.Add(CriarItem(comandos[i]));

        // Itens novos + painel que cresce: força o layout a recalcular agora, de dentro para fora
        if (painel != null && painel.activeInHierarchy)
        {
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(conteudo);
            if (painel.transform is RectTransform areaPainel) LayoutRebuilder.ForceRebuildLayoutImmediate(areaPainel);
        }

        // Agora os itens têm largura: decide se o X fica ao lado do texto ou sai para a lateral
        foreach (AjusteDoX ajuste in _ajustes) PosicionarX(ajuste);
    }

    // Diz ao layout quanta largura este item precisa: margens + texto inteiro + X.
    void DefinirLarguraPreferida(GameObject raiz, TMP_Text texto, float larguraX)
    {
        if (!painelCresceNaLargura || texto == null) return;

        var medida = raiz.GetComponent<LayoutElement>();
        if (medida == null) medida = raiz.AddComponent<LayoutElement>();

        float larguraDoTexto = texto.GetPreferredValues(texto.text).x;
        float precisa = margemLateral * 2f + larguraDoTexto + (permitirApagar ? larguraX : 0f);

        medida.preferredWidth = Mathf.Max(precisa, larguraMinimaDoItem);
        if (larguraMinimaDoItem > 0f) medida.minWidth = larguraMinimaDoItem;
    }

    void PosicionarX(AjusteDoX a)
    {
        if (a.raiz == null || a.apagar == null || a.texto == null) return;

        float larguraDoTexto = a.texto.GetPreferredValues(a.texto.text).x;
        bool cabe = margemLateral + larguraDoTexto + a.largura + margemLateral <= a.raiz.rect.width;

        RectTransform areaTexto = a.texto.rectTransform;
        areaTexto.anchorMin = new Vector2(0f, 0f);
        areaTexto.anchorMax = new Vector2(1f, 1f);
        areaTexto.offsetMin = new Vector2(margemLateral, 0f);

        if (cabe || !xParaOLadoSeNaoCouber)
        {
            // dentro do item, colado na direita; o texto termina onde o X começa
            a.apagar.pivot = new Vector2(1f, 0.5f);
            a.apagar.anchoredPosition = Vector2.zero;
            areaTexto.offsetMax = new Vector2(-(margemLateral + a.largura), 0f);
        }
        else
        {
            // não cabe: o X vai para fora do item, na lateral direita, e o texto usa a largura toda
            a.apagar.pivot = new Vector2(0f, 0.5f);
            a.apagar.anchoredPosition = new Vector2(margemLateral * 0.5f, 0f);
            areaTexto.offsetMax = new Vector2(-margemLateral, 0f);
        }
    }

    // Cria o item a partir do modelo do jogador (visual dele, tamanho dele).
    GameObject CriarItemDoModelo(string comando)
    {
        GameObject raiz = Instantiate(modeloDoItem, conteudo, false);
        raiz.name = comando;
        raiz.SetActive(true);

        // Sem Layout Element nem Layout Group no modelo, o item teria altura 0 e os itens ficariam
        // empilhados no mesmo lugar. Então usa a altura que você desenhou no modelo.
        if (raiz.GetComponent<LayoutElement>() == null && raiz.GetComponent<LayoutGroup>() == null)
        {
            float altura = ((RectTransform)modeloDoItem.transform).rect.height;
            if (altura <= 0f) altura = alturaDoItem;

            var medida = raiz.AddComponent<LayoutElement>();
            medida.minHeight = altura;
            medida.preferredHeight = altura;
        }

        // clicar no item inteiro coloca o comando na caixa
        Button principal = raiz.GetComponent<Button>();
        if (principal == null)
        {
            principal = raiz.AddComponent<Button>();
            Graphic fundo = raiz.GetComponent<Graphic>();
            if (fundo != null) principal.targetGraphic = fundo;
        }
        principal.onClick.RemoveAllListeners();
        string escolhido = comando;
        principal.onClick.AddListener(() => Escolher(escolhido));

        // X: um botão filho cujo nome contém "apagar"
        Button apagar = null;
        foreach (Button b in raiz.GetComponentsInChildren<Button>(true))
        {
            if (b.gameObject != raiz && b.name.ToLowerInvariant().Contains("apagar")) { apagar = b; break; }
        }

        // texto do comando: o primeiro TextMeshPro que NÃO está dentro do botão Apagar (senão o "X" receberia o comando)
        TMP_Text texto = null;
        foreach (TMP_Text t in raiz.GetComponentsInChildren<TMP_Text>(true))
        {
            if (apagar != null && t.transform.IsChildOf(apagar.transform)) continue;
            texto = t;
            break;
        }

        if (texto != null)
        {
            texto.text = comando;
#pragma warning disable CS0618
            texto.enableWordWrapping = false;             // uma linha só
#pragma warning restore CS0618
            texto.overflowMode = TextOverflowModes.Truncate;
        }
        else Debug.LogWarning("PainelComandos: o 'Modelo Do Item' não tem nenhum TextMeshPro dentro.", this);

        if (apagar != null)
        {
            apagar.gameObject.SetActive(permitirApagar);
            apagar.onClick.RemoveAllListeners();
            string paraApagar = comando;
            apagar.onClick.AddListener(() => Apagar(paraApagar));

            // X do próprio modelo: usa a largura que ele já tem
            RectTransform areaDoSeuX = (RectTransform)apagar.transform;
            DefinirLarguraPreferida(raiz, texto, permitirApagar ? Mathf.Max(areaDoSeuX.rect.width, areaDoSeuX.sizeDelta.x) : 0f);
        }
        else if (permitirApagar)
        {
            // X criado por código, do tamanho do SEU item (a não ser que "Largura Do Apagar" diga outra coisa)
            float alturaDoModelo = ((RectTransform)modeloDoItem.transform).rect.height;
            if (alturaDoModelo <= 0f) alturaDoModelo = alturaDoItem;

            float larguraX = larguraDoApagar > 0f ? larguraDoApagar : alturaDoModelo;
            float fonteX = texto != null ? texto.fontSize : tamanhoDaFonte;
            RectTransform areaX = CriarBotaoApagar(raiz.transform, comando, larguraX, fonteX, texto != null ? texto.font : null);

            // A posição final do X e o fim do texto são decididos em PosicionarX, quando a largura do item já existe
            if (texto != null)
                _ajustes.Add(new AjusteDoX { raiz = (RectTransform)raiz.transform, apagar = areaX, texto = texto, largura = larguraX });

            DefinirLarguraPreferida(raiz, texto, larguraX);
        }
        else
        {
            DefinirLarguraPreferida(raiz, texto, 0f);     // sem X
        }

        return raiz;
    }

    GameObject CriarItem(string comando)
    {
        if (modeloDoItem != null) return CriarItemDoModelo(comando);

        var raiz = new GameObject(comando, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        raiz.transform.SetParent(conteudo, false);

        var fundo = raiz.GetComponent<Image>();
        fundo.color = corDoItem;

        var medida = raiz.GetComponent<LayoutElement>();
        medida.minHeight = alturaDoItem;
        medida.preferredHeight = alturaDoItem;

        var botao = raiz.GetComponent<Button>();
        botao.targetGraphic = fundo;
        string escolhido = comando;                       // cópia local: cada botão guarda o seu comando
        botao.onClick.AddListener(() => Escolher(escolhido));

        var textoObj = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI));
        textoObj.transform.SetParent(raiz.transform, false);

        var area = (RectTransform)textoObj.transform;
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(margemLateral, 0f);
        area.offsetMax = new Vector2(-(margemLateral + (permitirApagar ? LarguraApagar : 0f)), 0f);   // deixa espaço para o X

        var texto = textoObj.GetComponent<TextMeshProUGUI>();
        if (fonte != null) texto.font = fonte;
        texto.text = comando;
        texto.fontSize = tamanhoDaFonte;
        texto.color = corDoTexto;
        texto.alignment = TextAlignmentOptions.MidlineLeft;
#pragma warning disable CS0618
        texto.enableWordWrapping = false;                 // uma linha só: comando comprido não quebra em duas
#pragma warning restore CS0618
        texto.overflowMode = TextOverflowModes.Truncate;  // comando comprido é cortado, sem precisar do glifo de reticências
        texto.raycastTarget = false;                      // o clique vale para o botão, não para o texto

        if (permitirApagar)
        {
            RectTransform areaX = CriarBotaoApagar(raiz.transform, comando, LarguraApagar, tamanhoDaFonte, null);
            _ajustes.Add(new AjusteDoX { raiz = (RectTransform)raiz.transform, apagar = areaX, texto = texto, largura = LarguraApagar });
        }

        DefinirLarguraPreferida(raiz, texto, LarguraApagar);

        return raiz;
    }

    // Um quadradinho com "X" na ponta direita do item. Por ser filho do item e ter o próprio Button,
    // o clique nele apaga e NÃO conta como "escolher o comando".
    RectTransform CriarBotaoApagar(Transform item, string comando, float largura, float tamanhoFonte, TMP_FontAsset fonteDoX)
    {
        var obj = new GameObject("Apagar", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(item, false);

        var area = (RectTransform)obj.transform;
        area.anchorMin = new Vector2(1f, 0f);
        area.anchorMax = new Vector2(1f, 1f);
        area.pivot = new Vector2(1f, 0.5f);
        area.sizeDelta = new Vector2(largura, 0f);
        area.anchoredPosition = Vector2.zero;

        var fundo = obj.GetComponent<Image>();
        fundo.color = corDoBotaoApagar;

        var botao = obj.GetComponent<Button>();
        botao.targetGraphic = fundo;
        string apagar = comando;
        botao.onClick.AddListener(() => Apagar(apagar));

        var rotuloObj = new GameObject("X", typeof(RectTransform), typeof(TextMeshProUGUI));
        rotuloObj.transform.SetParent(obj.transform, false);

        var rotuloArea = (RectTransform)rotuloObj.transform;
        rotuloArea.anchorMin = Vector2.zero;
        rotuloArea.anchorMax = Vector2.one;
        rotuloArea.offsetMin = Vector2.zero;
        rotuloArea.offsetMax = Vector2.zero;

        var rotulo = rotuloObj.GetComponent<TextMeshProUGUI>();
        if (fonteDoX != null) rotulo.font = fonteDoX;
        else if (fonte != null) rotulo.font = fonte;
        rotulo.text = "X";
        rotulo.fontSize = tamanhoFonte;
        rotulo.color = corDoX;
        rotulo.alignment = TextAlignmentOptions.Center;
        rotulo.raycastTarget = false;

        return area;
    }

    void Apagar(string comando)
    {
        if (HackTerminal.Instancia != null) HackTerminal.Instancia.RemoverComandoUsado(comando);
    }

    public void LimparTudo()
    {
        if (HackTerminal.Instancia != null) HackTerminal.Instancia.LimparComandosUsados();
    }

    void Escolher(string comando)
    {
        if (HackTerminal.Instancia != null) HackTerminal.Instancia.UsarNaCaixa(comando);
        if (fecharAoEscolher) Abrir(false);
    }
}