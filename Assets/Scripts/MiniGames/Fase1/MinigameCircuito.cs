using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinigameCircuito : MinigameBase
{
    [System.Serializable]
    public class NoCircuito
    {
        public Button botao;
        public Image imagem;
    }

    [Header("Configuração do circuito")]
    [SerializeField] private List<NoCircuito> nos = new List<NoCircuito>();
    [SerializeField] private List<int> ordemCorreta = new List<int>();
    [SerializeField] private Color corDesligado = Color.red;
    [SerializeField] private Color corLigado = Color.green;

    private List<int> cliquesAtuais = new List<int>();

    private void OnEnable()
    {
        ResetarCircuito();
    }

    private void ResetarCircuito()
    {
        cliquesAtuais.Clear();
        for (int i = 0; i < nos.Count; i++)
        {
            nos[i].imagem.color = corDesligado;
            int index = i;
            nos[i].botao.onClick.RemoveAllListeners();
            nos[i].botao.onClick.AddListener(() => OnClicarNo(index));
        }
    }

    private void OnClicarNo(int index)
    {
        int posicaoEsperada = cliquesAtuais.Count;
        if (posicaoEsperada >= ordemCorreta.Count) return;

        if (ordemCorreta[posicaoEsperada] == index)
        {
            nos[index].imagem.color = corLigado;
            cliquesAtuais.Add(index);
            if (cliquesAtuais.Count == ordemCorreta.Count) Vencer();
        }
        else
        {
            Debug.Log("Ordem errada — circuito reiniciado.");
            ResetarCircuito();
        }
    }
}