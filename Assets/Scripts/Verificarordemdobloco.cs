using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// Confere se o jogador escreveu os comandos NA ORDEM CERTA.
//
// ── Como usar ────────────────────────────────────────────────────────────────────
//  1. Coloque este script no objeto "Flag" que a entrada do backup (backup_v3) liga na lista de Objetos
//     do HackTerminal. Esse objeto precisa começar DESATIVADO: o script só vigia enquanto ele está ativo,
//     ou seja, depois que o jogador abriu o backup.
//  2. Em "Sequencia Correta" escreva os comandos na ordem certa (um por linha da lista).
//  3. Arraste a mensagem de falha em "Mensagem De Falha" (ela é ativada quando a ordem estiver errada).
//  4. Se a "forma de voltar" for um evento, ligue em "Ao Falhar". Se quiser que o script mesmo recarregue a
//     fase, marque "Recarregar Cena Ao Falhar".
//
// ── O que conta como erro de ordem ───────────────────────────────────────────────
//  Só as linhas que pertencem à sequência correta são olhadas; o resto (Inspection:1, outros comandos...) é ignorado.
//  Se elas aparecerem fora da ordem da lista, é falha. Linhas repetidas não atrapalham.
//  Maiúsculas, acentos, espaços, hífens e underscores não importam ("senha: onda-305" = "Senha:ONDA 305").
//  O bloco que ABRIU o backup nunca é conferido (ele foi enviado antes de este script ligar).
//
// ── Bloqueio ─────────────────────────────────────────────────────────────────────
//  A conferência acontece ANTES de o código rodar (evento HackTerminal.AntesDeExecutar). Se a ordem estiver errada,
//  NADA do bloco é executado: aparece a mensagem de falha e o jogador recomeça. Enquanto a mensagem de falha estiver
//  na tela, qualquer outro código também fica bloqueado.
//  Isso exige a versão do HackTerminal que tem o evento AntesDeExecutar.
public class VerificarOrdemDoBloco : MonoBehaviour
{
    [Header("Sequência correta (na ordem, um comando por item)")]
    [Tooltip("Escreva como o jogador digita, ex.: Cofre:1")]
    [SerializeField] private string[] sequenciaCorreta =
    {
        "Cofre:1",
        "Hack Folder:1",
        "Senha: ONDA 305",
        "Hack Document: log_codemind",
    };

    [Tooltip("Desmarcado: só é falha se as linhas da sequência aparecerem FORA DE ORDEM (um bloco incompleto, mas na ordem, não é falha).\n" +
             "Marcado: qualquer bloco que não tenha TODA a sequência, na ordem, e sem nada fora dela, é falha.")]
    [SerializeField] private bool exigirSequenciaCompleta = false;

    [Header("Quando a ordem está errada")]
    [Tooltip("Objeto (DESATIVADO no início) que mostra a mensagem de falha.")]
    [SerializeField] private GameObject mensagemDeFalha;

    [Tooltip("Roda quando falha. Ligue aqui a sua forma de voltar ao início da fase, se for por evento.")]
    [SerializeField] private UnityEvent aoFalhar;

    [Tooltip("Marque para ESTE script recarregar a cena atual depois de alguns segundos (você já tem sua forma de voltar? deixe desmarcado).")]
    [SerializeField] private bool recarregarCenaAoFalhar = false;

    [SerializeField, Min(0f)] private float segundosAntesDeRecarregar = 5f;

    [Header("Quando acerta (opcional)")]
    [Tooltip("Roda quando o jogador manda a sequência COMPLETA, na ordem certa, e o programa termina sem erro.")]
    [SerializeField] private UnityEvent aoAcertar;

    [Header("Depuração")]
    [Tooltip("Escreve no Console o que foi lido e a decisão tomada.")]
    [SerializeField] private bool mostrarLogs = true;

    HackTerminal _terminal;
    bool _falhou, _acertou, _pularProximo;

    sealed class Analise
    {
        public List<int> posicoes;
        public bool foraDeOrdem, completa, falha;
    }

    void OnEnable()
    {
        _falhou = false;
        _acertou = false;
        _pularProximo = false;
        Assinar();
    }

    void OnDisable()
    {
        // Não dá para enviar código novo enquanto um programa roda: o bloqueio só precisa valer com o objeto ligado.
        if (_terminal != null) _terminal.AntesDeExecutar -= Bloquear;

        // Se este objeto foi desligado POR UM COMANDO do programa que ainda está rodando (ex.: a primeira linha
        // "Hack Document: 0" fecha o backup e desliga o Flag junto), continua ouvindo o FIM desse programa.
        if (_terminal != null && _terminal.Rodando) return;

        Desassinar();
    }

    void OnDestroy() => Desassinar();

    void Desassinar()
    {
        if (_terminal != null)
        {
            _terminal.AoTerminar -= Verificar;
            _terminal.AntesDeExecutar -= Bloquear;
        }
        _terminal = null;
    }

    void Assinar()
    {
        HackTerminal t = HackTerminal.Instancia;
        if (t == null)
        {
            StartCoroutine(AssinarQuandoExistir());
            return;
        }

        if (_terminal != null)                                         // nunca ouve duas vezes
        {
            _terminal.AoTerminar -= Verificar;
            _terminal.AntesDeExecutar -= Bloquear;
        }

        _terminal = t;
        t.AoTerminar += Verificar;
        t.AntesDeExecutar += Bloquear;

        // Este objeto foi ligado por um comando que AINDA está rodando (o que abriu o backup):
        // o fim desse programa não conta para o "acertou".
        _pularProximo = t.Rodando;
    }

    IEnumerator AssinarQuandoExistir()
    {
        while (HackTerminal.Instancia == null) yield return null;
        Assinar();
    }

    // O HackTerminal chama isto ANTES de rodar o código. Devolver true BLOQUEIA: nada é executado.
    bool Bloquear(string codigo)
    {
        if (_falhou)
        {
            // enquanto a mensagem de falha estiver na tela, nada roda
            if (mensagemDeFalha == null || mensagemDeFalha.activeInHierarchy) return true;
            _falhou = false;
        }

        if (_acertou) return false;

        Analise a = Analisar(codigo);
        if (a == null) return false;

        if (mostrarLogs)
            Debug.Log($"VerificarOrdemDoBloco: linhas da sequência na ordem digitada = [{string.Join(", ", a.posicoes)}] " +
                      $"(0 = primeira da lista). Fora de ordem: {a.foraDeOrdem}. Completa: {a.completa}. Bloquear: {a.falha}.", this);

        if (!a.falha) return false;

        Falhar();
        return true;
    }

    // Chamado pelo HackTerminal quando um programa termina (ok = false se parou num erro).
    void Verificar(bool ok)
    {
        try { Conferir(ok); }
        finally
        {
            // Se o objeto ficou desligado durante o programa, já cumpriu o papel: para de ouvir.
            if (!isActiveAndEnabled) Desassinar();
        }
    }

    // Só decide o "acertou": a falha já foi tratada antes de o código rodar.
    void Conferir(bool ok)
    {
        if (_pularProximo) { _pularProximo = false; return; }
        if (_falhou || _acertou || _terminal == null || !ok) return;

        IReadOnlyList<string> historico = _terminal.Historico;
        if (historico == null || historico.Count == 0) return;

        Analise a = Analisar(historico[historico.Count - 1]);
        if (a != null && a.completa && !a.falha) Acertar();
    }

    // Compara o código digitado com a sequência certa. Devolve null se não há sequência configurada.
    Analise Analisar(string codigo)
    {
        var sequencia = new List<string>();
        foreach (string s in sequenciaCorreta)
        {
            string k = Chave(s);
            if (k != null) sequencia.Add(k);
        }
        if (sequencia.Count == 0) return null;

        // posição, dentro da sequência certa, de cada linha digitada que pertence a ela
        var posicoes = new List<int>();
        foreach (string k in ChavesDoCodigo(codigo))
        {
            int p = sequencia.IndexOf(k);
            if (p >= 0) posicoes.Add(p);
        }

        bool foraDeOrdem = false;
        for (int i = 1; i < posicoes.Count; i++)
            if (posicoes[i] < posicoes[i - 1]) { foraDeOrdem = true; break; }

        bool completa = posicoes.Count == sequencia.Count;
        for (int i = 0; completa && i < posicoes.Count; i++)
            if (posicoes[i] != i) completa = false;

        return new Analise
        {
            posicoes = posicoes,
            foraDeOrdem = foraDeOrdem,
            completa = completa,
            falha = exigirSequenciaCompleta ? (posicoes.Count > 0 && !completa) : foraDeOrdem,
        };
    }

    void Falhar()
    {
        _falhou = true;
        if (mostrarLogs) Debug.Log("VerificarOrdemDoBloco: ordem errada, mostrando a mensagem de falha.", this);

        if (mensagemDeFalha != null) mensagemDeFalha.SetActive(true);
        aoFalhar?.Invoke();

        // Roda no HackTerminal (que não é desligado), porque este objeto pode estar desativado agora.
        if (recarregarCenaAoFalhar && _terminal != null) _terminal.StartCoroutine(RecarregarDepois());
    }

    void Acertar()
    {
        _acertou = true;
        if (mostrarLogs) Debug.Log("VerificarOrdemDoBloco: sequência completa e na ordem certa.", this);
        aoAcertar?.Invoke();
    }

    IEnumerator RecarregarDepois()
    {
        if (segundosAntesDeRecarregar > 0f) yield return new WaitForSecondsRealtime(segundosAntesDeRecarregar);
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ── Leitura do que foi digitado (mesmas regras do HackTerminal: uma linha por comando, ';' separa comandos) ──

    static IEnumerable<string> ChavesDoCodigo(string codigo)
    {
        foreach (string bruta in codigo.Replace("\r", "").Split('\n'))
        {
            foreach (string parte in bruta.Split(';'))
            {
                string texto = parte.Trim();
                if (texto.Length == 0) continue;

                string k = Chave(texto);
                if (k != null) yield return k;
            }
        }
    }

    // "Senha: ONDA 305" -> "senha:onda305". Devolve null se não for "Nome:valor".
    static string Chave(string linha)
    {
        if (string.IsNullOrWhiteSpace(linha)) return null;

        int pos = linha.IndexOf(':');
        if (pos < 0) return null;

        string nome = Normalizar(linha.Substring(0, pos));
        string valor = Normalizar(linha.Substring(pos + 1).Trim().Trim('"', '“', '”'));
        if (nome.Length == 0 || valor.Length == 0) return null;

        if (valor == "true") valor = "1";      // o HackTerminal aceita "True" e "1" como a mesma coisa
        return nome + ":" + valor;
    }

    // minúsculas, sem acentos, sem espaços/hífens/underscores
    static string Normalizar(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";

        string d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(d.Length);
        foreach (char c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c) || c == '-' || c == '_') continue;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }
}