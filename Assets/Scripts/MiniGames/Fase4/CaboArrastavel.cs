using UnityEngine;
using UnityEngine.EventSystems;

public class CaboArrastavel : MonoBehaviour, IDragHandler, IEndDragHandler
{
    [HideInInspector] public string idOrigem;
    private RectTransform rect;
    private Canvas canvas;
    private Vector2 posicaoInicial;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
    }

    private void OnEnable() => posicaoInicial = rect.anchoredPosition;

    public void OnDrag(PointerEventData eventData)
    {
        rect.anchoredPosition += eventData.delta / canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        SlotDestino slot = eventData.pointerCurrentRaycast.gameObject?.GetComponent<SlotDestino>();

        if (slot != null && slot.idEsperado == idOrigem)
        {
            rect.anchoredPosition = slot.GetComponent<RectTransform>().anchoredPosition;
            slot.Conectado();
        }
        else
        {
            rect.anchoredPosition = posicaoInicial;
        }
    }
}