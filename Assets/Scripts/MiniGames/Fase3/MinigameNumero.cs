using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MinigameNumero : MinigameBase
{
    [Header("Configuração do dial")]
    [SerializeField] private Button botaoMais;
    [SerializeField] private Button botaoMenos;
    [SerializeField] private Button botaoConfirmar;
    [SerializeField] private TMP_Text textoValorAtual;
    [SerializeField] private int valorMinimo = 0;
    [SerializeField] private int valorMaximo = 20;
    [SerializeField] private int valorCorreto = 5;

    private int valorAtual;

    private void OnEnable()
    {
        valorAtual = valorMinimo;
        AtualizarTexto();

        botaoMais.onClick.RemoveAllListeners();
        botaoMenos.onClick.RemoveAllListeners();
        botaoConfirmar.onClick.RemoveAllListeners();

        botaoMais.onClick.AddListener(() => MudarValor(1));
        botaoMenos.onClick.AddListener(() => MudarValor(-1));
        botaoConfirmar.onClick.AddListener(Confirmar);
    }

    private void MudarValor(int delta)
    {
        valorAtual = Mathf.Clamp(valorAtual + delta, valorMinimo, valorMaximo);
        AtualizarTexto();
    }

    private void AtualizarTexto() => textoValorAtual.text = valorAtual.ToString();

    private void Confirmar()
    {
        if (valorAtual == valorCorreto) Vencer();
        else Debug.Log("Valor incorreto, tenta de novo.");
    }
}