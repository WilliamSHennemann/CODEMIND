using UnityEngine;
using TMPro;

public class CommandInterpreter : MonoBehaviour
{
    public TMP_InputField commandInput;
    public Transform targetObject;
    public float cameraSpeed = 20f;

    [Header("Offset para câmera 2D")]
    public Vector2 cameraOffset = new Vector2(0f, 8f); // (X, Y) - ajuste o Y para cima

    private Camera mainCamera;
    private Vector3 lastLoggedPosition;

    void Start()
    {
        mainCamera = Camera.main;
        commandInput.onSubmit.AddListener(OnSubmitCommand);
    }

    void OnSubmitCommand(string text)
    {
        string[] parts = text.Split(':');

        if (parts.Length == 2)
        {
            string objectName = parts[0].Trim();
            string valueStr = parts[1].Trim();

            if (int.TryParse(valueStr, out int intValue))
            {
                bool state = intValue == 1;
                GameObject foundObject = GameObject.Find(objectName);

                if (foundObject != null)
                {
                    foundObject.SetActive(state);
                    targetObject = foundObject.transform;
                    Debug.Log($"Objeto '{objectName}' foi {(state ? "ativado" : "desativado")}.");
                }
                else
                {
                    Debug.LogWarning($"Objeto '{objectName}' não encontrado.");
                }
            }
            else
            {
                Debug.LogWarning("O valor deve ser 0 ou 1.");
            }
        }
        else
        {
            Debug.LogWarning("Formato inválido. Use 'Nome: 1' ou 'Nome: 0'.");
        }

        commandInput.text = "";
    }

    void Update()
{
    if (targetObject != null)
    {
        Vector3 targetPos = new Vector3(
            targetObject.position.x + cameraOffset.x,
            targetObject.position.y + cameraOffset.y,
            mainCamera.transform.position.z
        );

        // LOG DE DEPURAÇÃO
        Debug.Log($"Alvo calculado: {targetPos} | Câmera atual: {mainCamera.transform.position}");

        mainCamera.transform.position = Vector3.MoveTowards(
            mainCamera.transform.position,
            targetPos,
            cameraSpeed * Time.deltaTime
        );
    }
}
}