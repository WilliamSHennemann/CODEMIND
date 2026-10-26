using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Botão de ajuda que abre uma lista com TODOS os comandos do jogo, em qualquer fase.
//
// ── Onde fica a lista ────────────────────────────────────────────────────────────
// No HackTerminal (o GameManager que sobrevive à troca de fase), campo "Ajuda": cada linha tem o
// comando (ex.: Hack Folder:1) e uma frase curta dizendo o que ele faz. Como o HackTerminal não é
// destruído, a MESMA lista aparece em todas as telas. Basta repetir este botão em cada cena
// (ou deixá-lo em um Canvas que também sobreviva à troca de cena).
//
// Se uma cena não tiver HackTerminal (ex.: o menu inicial), a lista "Ajuda Local" deste componente é usada.
//
// ── Montagem na cena ─────────────────────────────────────────────────────────────
//  1. Um botão (o "?")                                  -> campo "Botao Abrir"
//  2. Um painel (deixe DESATIVADO no início)            -> campo "Painel"
//  3. Dentro do painel, um objeto vazio com VerticalLayoutGroup
//       Child Control Width ✓  |  Child Control Height ✓
//       Child Force Expand Width ✓  |  Child Force Expand Height ✗
//     (se a lista for grande, coloque dentro de um Scroll View)   -> campo "Conteudo"
//  4. OPCIONAL: um botão de fechar dentro do painel     -> campo "Botao Fechar"
//  5. Este script em um objeto que fique SEMPRE ATIVO (pode ser o próprio botão).
//
// Os itens são criados por código: cada comando fica colorido, com a explicação logo abaixo.
public class MenuAjuda : MonoBehaviour
{
    [Header("Ligações")]
    [SerializeField] private Button botaoAbrir;
    [SerializeField] private GameObject painel;
    [SerializeField] private RectTransform conteudo;
    [Tooltip("OPCIONAL: botão que fecha o painel.")]
    [SerializeField] private Button botaoFechar;

    [Header("Lista (só usada se a cena não tiver HackTerminal)")]
    [SerializeField] private List<HackTerminal.AjudaDeComando> ajudaLocal = new List<HackTerminal.AjudaDeComando>();

    [Header("Aparência")]
    [SerializeField] private TMP_FontAsset fonte;
    [Tooltip("Mesma unidade do seu Canvas. Se o painel for pequeno, use valores pequenos.")]
    [SerializeField, Min(0.01f)] private float tamanhoDaFonte = 18f;
    [SerializeField] private Color corDoComando = new Color(0.4f, 1f, 0.9f);
    [SerializeField] private Color corDaDescricao = Color.white;
    [Tooltip("Espaço nas laterais do texto de cada item.")]
    [SerializeField, Min(0f)] private float margemLateral = 4f;

    readonly List<GameObject> _itens = new List<GameObject>();

    void OnEnable()
    {
        if (botaoAbrir != null) botaoAbrir.onClick.AddListener(Alternar);
        if (botaoFechar != null) botaoFechar.onClick.AddListener(Fechar);
    }

    void OnDisable()
    {
        if (botaoAbrir != null) botaoAbrir.onClick.RemoveListener(Alternar);
        if (botaoFechar != null) botaoFechar.onClick.RemoveListener(Fechar);
    }

    public void Alternar()
    {
        if (painel != null) Abrir(!painel.activeSelf);
    }

    public void Fechar() => Abrir(false);

    public void Abrir(bool abrir)
    {
        if (painel == null) return;

        painel.SetActive(abrir);
        if (abrir) Montar();
    }

    // A lista é refeita a cada abertura: se você mudar o texto no Inspector, ele aparece na hora.
    void Montar()
    {
        if (conteudo == null) return;

        foreach (GameObject antigo in _itens)
        {
            if (antigo == null) continue;
            antigo.SetActive(false);        // some do layout já neste frame (o Destroy só vale no fim dele)
            Destroy(antigo);
        }
        _itens.Clear();

        List<HackTerminal.AjudaDeComando> lista = ListaAtual();
        string corCmd = ColorUtility.ToHtmlStringRGB(corDoComando);
        string corDesc = ColorUtility.ToHtmlStringRGB(corDaDescricao);

        foreach (HackTerminal.AjudaDeComando linha in lista)
        {
            if (linha == null || string.IsNullOrWhiteSpace(linha.comando)) continue;

            var obj = new GameObject(linha.comando, typeof(RectTransform), typeof(TextMeshProUGUI));
            obj.transform.SetParent(conteudo, false);

            var texto = obj.GetComponent<TextMeshProUGUI>();
            if (fonte != null) texto.font = fonte;
            texto.richText = true;
            texto.fontSize = tamanhoDaFonte;
            texto.alignment = TextAlignmentOptions.TopLeft;
            texto.margin = new Vector4(margemLateral, 0f, margemLateral, 0f);
            texto.raycastTarget = false;
#pragma warning disable CS0618
            texto.enableWordWrapping = true;              // explicação comprida quebra em várias linhas
#pragma warning restore CS0618
            texto.overflowMode = TextOverflowModes.Overflow;

            texto.text = string.IsNullOrWhiteSpace(linha.descricao)
                ? $"<color=#{corCmd}>{linha.comando.Trim()}</color>"
                : $"<color=#{corCmd}>{linha.comando.Trim()}</color>\n<color=#{corDesc}>{linha.descricao.Trim()}</color>";

            _itens.Add(obj);
        }

        // O painel pode crescer com a lista: recalcula já, de dentro para fora
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(conteudo);
        if (painel != null && painel.transform is RectTransform areaPainel) LayoutRebuilder.ForceRebuildLayoutImmediate(areaPainel);
    }

    List<HackTerminal.AjudaDeComando> ListaAtual()
    {
        if (HackTerminal.Instancia != null && HackTerminal.Instancia.ajuda != null && HackTerminal.Instancia.ajuda.Count > 0)
            return HackTerminal.Instancia.ajuda;

        return ajudaLocal;
    }
}