using UnityEngine;
using TMPro;

public class NomeDoObjeto2D : MonoBehaviour
{
    [Tooltip("OPCIONAL: arraste aqui uma fonte (TMP_FontAsset) para usar em vez da fonte padrão do texto.")]
    [SerializeField] private TMP_FontAsset fonte;

    void Start()
    {
        // Busca o componente de texto que está dentro do Canvas filho
        TMP_Text texto = GetComponentInChildren<TMP_Text>();

        if (texto != null)
        {
            // Aplica o nome do objeto principal no texto
            texto.text = gameObject.name;

            // Se uma fonte foi escolhida no Inspector, aplica ela também
            if (fonte != null)
                texto.font = fonte;
        }
        else
        {
            Debug.LogWarning("Nenhum componente TextMeshPro foi encontrado nos filhos de " + gameObject.name);
        }
    }
}