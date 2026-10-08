using UnityEngine;


public class CameraFocus : MonoBehaviour
{
    [Header("Movimento da Câmera")]
    public float smoothSpeed = 5f;
    public float ajusteSubida = 0f;

    private Vector3 targetPosition;
    private bool isMoving = false;

    void Start()
    {
        targetPosition = transform.position;
    }

    public void Focar(GameObject alvo)
    {
        if (alvo == null) return;

        Vector3 centroVisual;

        if (alvo.TryGetComponent<Renderer>(out Renderer rend))
        {
            centroVisual = rend.bounds.center;
        }
        else if (alvo.TryGetComponent<RectTransform>(out RectTransform rect))
        {
            centroVisual = rect.position;
        }
        else
        {
            centroVisual = alvo.transform.position;
        }

        targetPosition = new Vector3(centroVisual.x, centroVisual.y + ajusteSubida, transform.position.z);
        isMoving = true;
    }

    void LateUpdate()
    {
        if (!isMoving) return;

        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, targetPosition) < 0.02f)
        {
            transform.position = targetPosition;
            isMoving = false;
        }
    }
}