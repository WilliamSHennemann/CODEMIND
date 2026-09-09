using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class MinigameDecodificar : MinigameBase
{
    [System.Serializable]
    public class LetraClicavel
    {
        public Button botao;
        public TMP_Text texto;
    }

    [Header("Configuração da decodificação")]
    [SerializeField] private List<LetraClicavel> letras = new List<LetraClicavel>();
    [SerializeField] private string palavraCorreta = "root";
    [SerializeField] private TMP_Text textoConstruido;

    private string construindo = "";

    private void OnEnable()
    {
        construindo = "";
        AtualizarTexto();

        foreach (var l in letras)
        {
            l.botao.onClick.RemoveAllListeners();
            TMP_Text letraRef = l.texto;
            l.botao.onClick.AddListener(() => OnClicarLetra(letraRef.text));
        }
    }

    private void OnClicarLetra(string letra)
    {
        string tentativa = construindo + letra.ToLower();

        if (palavraCorreta.ToLower().StartsWith(tentativa))
        {
            construindo = tentativa;
            AtualizarTexto();
            if (construindo.Length == palavraCorreta.Length) Vencer();
        }
        else
        {
            Debug.Log("Letra errada — reiniciando palavra.");
            construindo = "";
            AtualizarTexto();
        }
    }

    private void AtualizarTexto()
    {
        if (textoConstruido != null) textoConstruido.text = construindo;
    }
}