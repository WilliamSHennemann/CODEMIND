using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class ArquivoClicavel : MonoBehaviour, IPointerClickHandler
{
    [Header("Conteudo revelado ao clicar")]
    [TextArea] [SerializeField] private string conteudoArquivo = "Entrar = false";

    [Header("Onde mostrar o texto")]
    [SerializeField] private GameObject painelDeTexto; // um painel comum na cena, compartilhado
    [SerializeField] private TMP_Text textoDoPainel;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (painelDeTexto != null) painelDeTexto.SetActive(true);
        if (textoDoPainel != null) textoDoPainel.text = conteudoArquivo;
    }
}