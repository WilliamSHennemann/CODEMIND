using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LineRenderer))]
public class ConectarMultiplosObjetos : MonoBehaviour
{
    // Lista onde você pode arrastar quantos objetos quiser no Inspector
    public List<Transform> objetosParaConectar = new List<Transform>(); 

    private LineRenderer lineRenderer;

    void Start()
    {
        lineRenderer = GetComponent<LineRenderer>();
    }

    // LateUpdate evita que a linha fique "atrasada" em relação à animação
    void LateUpdate()
    {
        if (objetosParaConectar == null || objetosParaConectar.Count < 2)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        // Define a quantidade de pontos da linha com base no número de objetos
        lineRenderer.positionCount = objetosParaConectar.Count;

        // Atualiza a posição de cada ponto em tempo real
        for (int i = 0; i < objetosParaConectar.Count; i++)
{
    if (objetosParaConectar[i] != null)
    {
        // Tenta pegar o centro visual do objeto. Se não tiver, usa a posição padrão.
        if (objetosParaConectar[i].TryGetComponent<Renderer>(out Renderer renderer))
        {
            lineRenderer.SetPosition(i, renderer.bounds.center);
        }
        else
        {
            lineRenderer.SetPosition(i, objetosParaConectar[i].position);
        }
    }
}
    }
}