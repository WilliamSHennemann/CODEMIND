using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class MinigameSequencia : MinigameBase
{
    [System.Serializable]
    public class BotaoSequencia
    {
        public Button botao;
        public Image imagem;
        public Color corNormal = Color.white;
        public Color corAcesa = Color.yellow;
    }

    [Header("Configuração da sequência")]
    [SerializeField] private List<BotaoSequencia> botoes = new List<BotaoSequencia>();
    [SerializeField] private List<int> sequenciaCorreta = new List<int>();
    [SerializeField] private float tempoEntreFlashes = 0.5f;

    private List<int> sequenciaJogador = new List<int>();
    private bool aceitandoInput = false;

    private void OnEnable()
    {
        sequenciaJogador.Clear();
        aceitandoInput = false;

        for (int i = 0; i < botoes.Count; i++)
        {
            botoes[i].imagem.color = botoes[i].corNormal;
            int index = i;
            botoes[i].botao.onClick.RemoveAllListeners();
            botoes[i].botao.onClick.AddListener(() => OnClicarBotao(index));
        }

        StartCoroutine(MostrarSequencia());
    }

    private IEnumerator MostrarSequencia()
    {
        yield return new WaitForSeconds(0.5f);

        foreach (int i in sequenciaCorreta)
        {
            botoes[i].imagem.color = botoes[i].corAcesa;
            yield return new WaitForSeconds(tempoEntreFlashes);
            botoes[i].imagem.color = botoes[i].corNormal;
            yield return new WaitForSeconds(0.15f);
        }

        aceitandoInput = true;
    }

    private void OnClicarBotao(int index)
    {
        if (!aceitandoInput) return;

        int posicaoEsperada = sequenciaJogador.Count;
        if (sequenciaCorreta[posicaoEsperada] == index)
        {
            sequenciaJogador.Add(index);
            if (sequenciaJogador.Count == sequenciaCorreta.Count) Vencer();
        }
        else
        {
            Debug.Log("Sequência errada — repetindo.");
            sequenciaJogador.Clear();
            aceitandoInput = false;
            StartCoroutine(MostrarSequencia());
        }
    }
}