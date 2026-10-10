using System;
using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

// Coloque este componente NO OBJETO DO TEXTO (o TextMeshPro) do arquivo.
//
// No texto, marque o que escapou da corrupção com [[ ]]:
//
//     #@%&# restauracao #&%@#
//     #@%& Codigo: [[LUA-472]] &%#
//
// - Antes do Inspection, o jogador vê o texto SEM os colchetes e SEM cor (o trecho se mistura ao lixo).
// - Inspection:1  -> uma varredura percorre o texto linha por linha e, no fim, o trecho marcado ganha cor.
// - Inspection:0  (ou fechar a janela) -> volta ao texto normal.
//
// Arquivos SEM [[ ]] fazem a varredura e terminam sem destacar nada (é o "só lixo").
//
// Se o texto estiver dentro de uma janela com o componente ArquivoAberto, o destaque só acontece
// quando essa janela é o arquivo atual (vários arquivos abertos não se misturam).
public class TextoInspecionavel : MonoBehaviour
{
    [Tooltip("O texto que será destacado. Se vazio, usa o TextMeshPro deste mesmo objeto.")]
    [SerializeField] private TMP_Text texto;

    [Tooltip("Nome do comando que ativa o destaque (o mesmo que o jogador digita).")]
    [SerializeField] private string comandoDeInspecao = "Inspection";

    [Header("Cores")]
    [Tooltip("Cor do trecho que escapou da corrupção.")]
    [SerializeField] private Color corDestaque = new Color(1f, 0.82f, 0.2f);

    [Tooltip("Cor da linha que está sendo varrida no momento.")]
    [SerializeField] private Color corVarredura = new Color(0.4f, 1f, 0.9f);

    [Header("Varredura")]
    [Tooltip("Segundos em cada linha durante a varredura. 0 = destaca na hora, sem animação.")]
    [SerializeField, Min(0f)] private float tempoPorLinha = 0.25f;

    static readonly Regex Marcado = new Regex(@"\[\[(.*?)\]\]", RegexOptions.Singleline);

    string _original = "";        // texto como foi escrito, com [[ ]]
    bool _pronto, _assinado, _segurando;
    Coroutine _varredura;
    ArquivoAberto _arquivo;

    // ───────────────────────── Ciclo de vida ─────────────────────────

    void Awake() => Preparar();

    void OnEnable()
    {
        Preparar();
        MostrarNormal();       // reabriu a janela: começa sem destaque
        Assinar();
    }

    // Se o HackTerminal ainda não existia quando o texto ligou (ordem de Awake), tenta de novo aqui.
    void Start()
    {
        if (!_assinado) Assinar();
    }

    void OnDisable()
    {
        _varredura = null;     // (as corrotinas já param sozinhas ao desativar)
        SoltarPrograma();
        MostrarNormal();
        Desassinar();
    }

    // Enquanto a varredura roda, o HackTerminal "espera" (entradas com Esperar Varredura só mostram os objetos depois).
    void SegurarPrograma()
    {
        if (_segurando || HackTerminal.Instancia == null) return;

        HackTerminal.Instancia.Segurar();
        _segurando = true;
    }

    void SoltarPrograma()
    {
        if (!_segurando) return;

        _segurando = false;
        if (HackTerminal.Instancia != null) HackTerminal.Instancia.Soltar();
    }

    void Preparar()
    {
        if (_pronto) return;

        if (texto == null) texto = GetComponent<TMP_Text>();
        if (texto == null)
        {
            Debug.LogWarning($"TextoInspecionavel em '{name}': não achei nenhum TextMeshPro. Arraste o texto no campo 'Texto'.", this);
            return;
        }

        texto.richText = true;                                   // precisa ser true para as cores aparecerem
        _original = texto.text.Replace("\r", "");
        _arquivo = GetComponentInParent<ArquivoAberto>();
        _pronto = true;
    }

    void Assinar()
    {
        if (_assinado || HackTerminal.Instancia == null) return;

        HackTerminal.Instancia.AoComandoExecutado += AoComando;
        _assinado = true;
    }

    void Desassinar()
    {
        if (_assinado && HackTerminal.Instancia != null)
            HackTerminal.Instancia.AoComandoExecutado -= AoComando;

        _assinado = false;
    }

    // ───────────────────────── Reação ao comando ─────────────────────────

    void AoComando(string nome, string valor)
    {
        if (!_pronto || !string.Equals(nome?.Trim(), comandoDeInspecao?.Trim(), StringComparison.OrdinalIgnoreCase)) return;

        if (valor == "0") Restaurar();
        else if (EhOArquivoAtual()) Inspecionar();
    }

    bool EhOArquivoAtual()
    {
        if (_arquivo == null) return true;       // sem ArquivoAberto por perto: não filtra

        string atual = HackTerminal.Instancia != null ? HackTerminal.Instancia.ArquivoAtual : null;
        return atual != null && string.Equals(atual, _arquivo.Nome, StringComparison.OrdinalIgnoreCase);
    }

    // ───────────────────────── Destaque ─────────────────────────

    // Também pode ser chamado por outro script ou por um UnityEvent.
    public void Inspecionar()
    {
        if (!_pronto) return;

        if (_varredura != null) StopCoroutine(_varredura);
        SegurarPrograma();                       // antes de começar: o HackTerminal já vê "ocupado"
        _varredura = StartCoroutine(Varrer());
    }

    public void Restaurar()
    {
        if (!_pronto) return;

        if (_varredura != null) StopCoroutine(_varredura);
        _varredura = null;
        SoltarPrograma();
        MostrarNormal();
    }

    // Troca o conteúdo (ex.: o arquivo mostra outro texto). Use [[ ]] para marcar o trecho intacto.
    public void DefinirTexto(string comMarcadores)
    {
        Preparar();
        if (!_pronto) return;

        if (_varredura != null) StopCoroutine(_varredura);
        _varredura = null;
        SoltarPrograma();
        _original = (comMarcadores ?? "").Replace("\r", "");
        MostrarNormal();
    }

    IEnumerator Varrer()
    {
        if (tempoPorLinha > 0f)
        {
            string[] linhas = SemMarcadores(_original).Split('\n');
            string tintaVarredura = ColorUtility.ToHtmlStringRGB(corVarredura);
            var espera = new WaitForSeconds(tempoPorLinha);

            for (int atual = 0; atual < linhas.Length; atual++)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < linhas.Length; i++)
                {
                    if (i > 0) sb.Append('\n');

                    if (i == atual) sb.Append("<color=#").Append(tintaVarredura).Append('>').Append(linhas[i]).Append("</color>");
                    else sb.Append(linhas[i]);
                }
                texto.text = sb.ToString();
                yield return espera;
            }
        }

        texto.text = ComDestaque(_original);     // achou algo? o trecho ganha cor. Não achou? fica igual ao normal.
        _varredura = null;
        SoltarPrograma();                        // varredura acabou: quem estava esperando pode mostrar os objetos
    }

    void MostrarNormal()
    {
        if (_pronto && texto != null) texto.text = SemMarcadores(_original);
    }

    static string SemMarcadores(string s) => Marcado.Replace(s, "$1");

    string ComDestaque(string s)
    {
        string hex = ColorUtility.ToHtmlStringRGB(corDestaque);
        return Marcado.Replace(s, m => $"<color=#{hex}>{m.Groups[1].Value}</color>");
    }
}