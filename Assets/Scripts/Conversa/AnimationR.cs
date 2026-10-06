using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class AnimationR : MonoBehaviour
{
    [SerializeField] private SpriteRenderer Radio;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float velocidadeAnimacao = 0.08f;

    private void Awake()
    {
        Radio.sprite = frames[0]; // fica parado no frame 1 até ser chamado
    }

    private Coroutine loopAtual;

    public void ComecarLoop()
    {
        if (loopAtual != null)
            StopCoroutine(loopAtual);

        loopAtual = StartCoroutine(LoopPingPong());
    }

    public void PararLoop()
    {
        if (loopAtual != null)
        {
            StopCoroutine(loopAtual);
            loopAtual = null;
        }

        Radio.sprite = frames[0]; // volta pro frame parado
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