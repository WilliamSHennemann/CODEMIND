using UnityEngine;

// Coloque este componente na RAIZ da janela de cada arquivo (ex.: o objeto "Notes-back%p_v3").
//
// Enquanto a janela estiver ATIVA na tela, o HackTerminal sabe que esse é o "arquivo atual".
// Não importa COMO ela abriu (comando, clique, animação, outro script): se aparece, avisa; se some, avisa.
//
// Nas entradas do HackTerminal (ex.: "Inspection"), o campo "Contexto Necessario" pode ser
// o nome do arquivo (o mesmo nome deste componente) em vez de um comando.
public class ArquivoAberto : MonoBehaviour
{
    [Tooltip("OPCIONAL: nome que vale como contexto. Se vazio, usa o nome do próprio GameObject.")]
    [SerializeField] private string nomeDoArquivo;

    bool _avisou;

    public string Nome => string.IsNullOrWhiteSpace(nomeDoArquivo) ? gameObject.name : nomeDoArquivo.Trim();

    void OnEnable() => Avisar();

    // Se o HackTerminal ainda não existia quando a janela ligou (ordem de Awake), tenta de novo aqui.
    void Start()
    {
        if (!_avisou) Avisar();
    }

    void OnDisable()
    {
        if (_avisou && HackTerminal.Instancia != null)
            HackTerminal.Instancia.FecharArquivo(Nome);

        _avisou = false;
    }

    void Avisar()
    {
        if (HackTerminal.Instancia == null) return;

        HackTerminal.Instancia.DefinirArquivoAtual(Nome);
        _avisou = true;
    }
}
