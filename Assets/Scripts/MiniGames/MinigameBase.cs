using UnityEngine;

public abstract class MinigameBase : MonoBehaviour
{
    [Header("O que acontece ao vencer")]
    [SerializeField] protected GameObject pastaAberta; // objeto que mostra os arquivos/resposta
    [SerializeField] protected float atrasoAoVencer = 0.5f;

    protected virtual void Vencer()
    {
        Invoke(nameof(FecharEAbrirPasta), atrasoAoVencer);
    }

    protected virtual void FecharEAbrirPasta()
    {
        gameObject.SetActive(false);
        if (pastaAberta != null)
            pastaAberta.SetActive(true);
    }
}