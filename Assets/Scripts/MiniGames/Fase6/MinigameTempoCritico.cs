using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MinigameTempoCritico : MinigameBase
{
    [Header("Configuração")]
    [SerializeField] private List<RectTransform> posicoesPossiveis = new List<RectTransform>();
    [SerializeField] private Button botaoAlvo; // um único botão que se reposiciona
    [SerializeField] private Image barraDetecção;
    [SerializeField] private int acertosNecessarios = 6;
    [SerializeField] private float velocidadeDetecção = 0.15f; // quanto a barra enche por segundo
    [SerializeField] private float reducaoPorAcerto = 0.1f; // quanto reduz a barra ao acertar

    private int acertosAtuais;
    private float detecçãoAtual;
    private bool ativo;

    private void OnEnable()
    {
        acertosAtuais = 0;
        detecçãoAtual = 0f;
        ativo = true;

        botaoAlvo.onClick.RemoveAllListeners();
        botaoAlvo.onClick.AddListener(OnAcertou);

        ReposicionarBotao();
        StartCoroutine(EnchendoDetecção());
    }

    private IEnumerator EnchendoDetecção()
    {
        while (ativo)
        {
            detecçãoAtual += velocidadeDetecção * Time.deltaTime;
            barraDetecção.fillAmount = detecçãoAtual;

            if (detecçãoAtual >= 1f)
            {
                ativo = false;
                Debug.Log("Detectado! Reiniciando tentativa.");
                OnEnable(); // reinicia a fase do minigame
                yield break;
            }

            yield return null;
        }
    }

    private void OnAcertou()
    {
        if (!ativo) return;

        acertosAtuais++;
        detecçãoAtual = Mathf.Max(0f, detecçãoAtual - reducaoPorAcerto);
        barraDetecção.fillAmount = detecçãoAtual;

        if (acertosAtuais >= acertosNecessarios)
        {
            ativo = false;
            Vencer();
        }
        else
        {
            ReposicionarBotao();
        }
    }

    private void ReposicionarBotao()
    {
        if (posicoesPossiveis.Count == 0) return;
        var pos = posicoesPossiveis[Random.Range(0, posicoesPossiveis.Count)];
        botaoAlvo.GetComponent<RectTransform>().anchoredPosition = pos.anchoredPosition;
    }
}