using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AnimationR : MonoBehaviour
{
    [SerializeField] private SpriteRenderer Radio;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float velocidadeAnimacao = 0.08f;

    [Tooltip("Se marcado, o rádio começa invisível e só aparece quando a animação começa (ComecarLoop).")]
    [SerializeField] private bool comecarEscondido = false;

    private Coroutine loopAtual;

    private void Awake()
    {
        Radio.sprite = frames[0]; // fica parado no frame 1 até ser chamado

        if (comecarEscondido)
            Radio.enabled = false;
    }

    // Aparece e começa a tocar
    public void ComecarLoop()
    {
        Radio.enabled = true;

        if (loopAtual != null)
            StopCoroutine(loopAtual);

        loopAtual = StartCoroutine(LoopPingPong());
    }

    // Só para a animação (o rádio continua visível, parado no frame 1)
    public void PararLoop()
    {
        if (loopAtual != null)
        {
            StopCoroutine(loopAtual);
            loopAtual = null;
        }

        Radio.sprite = frames[0]; // volta pro frame parado
    }

    // Para a animação E some com o rádio
    public void Esconder()
    {
        PararLoop();
        Radio.enabled = false;
    }

    public void Mostrar()
    {
        Radio.enabled = true;
    }

    private IEnumerator LoopPingPong()
    {
        while (true)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                Radio.sprite = frames[i];
                yield return new WaitForSeconds(velocidadeAnimacao);
            }
            for (int i = frames.Length - 2; i >= 0; i--)
            {
                Radio.sprite = frames[i];
                yield return new WaitForSeconds(velocidadeAnimacao);
            }
        }
    }
}