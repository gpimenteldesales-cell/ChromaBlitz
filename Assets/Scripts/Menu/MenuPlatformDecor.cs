using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Efeito decorativo do menu principal:
/// - Gira o pivot com um relógio próprio (1 volta por loop da música), sem depender do áudio.
///   No WebGL o navegador só libera o áudio após o primeiro clique/tecla, e o relógio do
///   AudioSource pode ficar parado mesmo com isPlaying = true.
/// - Troca as cores dos blocos a cada N batidas (0,8 s a 75 BPM) quando a música está
///   realmente avançando.
/// - Se não houver AudioManager, ou se o áudio estiver parado/travado, usa o timer
///   interno (intervaloTroca).
/// </summary>
public class MenuPlatformDecor : MonoBehaviour
{
    [Header("Referências")]
    public Transform blocksParent;
    public Transform rotationPivot;

    [Header("Rotação")]
    [Tooltip("Duração de um loop da música, em segundos (96 batidas a 75 BPM = 76,8 s).")]
    public float duracaoLoop = 76.8f;
    [Tooltip("Voltas completas por loop da música.")]
    public float voltasPorLoop = 1f;

    [Header("Cores")]
    [Min(1)] public int batidasPorTroca = 1;
    [Tooltip("Intervalo de reserva (música parada ou sem AudioManager), em segundos.")]
    public float intervaloTroca = 0.8f;
    [Range(0f, 1f)] public float minBrightness = 0.3f;
    public bool transicaoSuave = false;
    public float duracaoTransicao = 0.35f;

    private readonly List<GridBlock> blocks = new List<GridBlock>();
    private Color[] coresOrigem;
    private Color[] coresDestino;

    private Quaternion rotacaoBase;
    private float tempoMenu;
    private float timerTroca;
    private float timerTransicao;
    private bool transicionando;
    private bool inscrito;

    // Detecção de áudio travado (WebGL antes do primeiro clique).
    private bool audioAvancando;
    private float ultimoNormalizado = -1f;
    private float tempoSemAvanco;

    private void Awake()
    {
        if (blocksParent == null)
            blocksParent = transform;

        if (rotationPivot == null)
        {
            Debug.LogError("MenuPlatformDecor: Rotation Pivot não foi configurado.", this);
            enabled = false;
            return;
        }

        blocks.AddRange(blocksParent.GetComponentsInChildren<GridBlock>());

        if (blocks.Count == 0)
        {
            Debug.LogError("MenuPlatformDecor: nenhum GridBlock encontrado.", this);
            enabled = false;
            return;
        }

        coresOrigem = new Color[blocks.Count];
        coresDestino = new Color[blocks.Count];
        rotacaoBase = rotationPivot.localRotation;
    }

    private void Start()
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            Color cor = ColorUtils.RandomColorNoBlack(minBrightness);
            blocks[i].SetColor(cor);
            coresDestino[i] = cor;
        }
    }

    private void OnDisable()
    {
        if (inscrito && AudioManager.Instance != null)
            AudioManager.Instance.OnBeat -= AoBater;
        inscrito = false;
    }

    private void AoBater(int beat)
    {
        // Se o áudio está travado, quem troca as cores é o timer interno.
        if (!audioAvancando)
            return;

        if (beat % Mathf.Max(1, batidasPorTroca) == 0)
            SortearNovasCores();
    }

    private void Update()
    {
        var am = AudioManager.Instance;

        if (!inscrito && am != null)
        {
            am.OnBeat += AoBater;
            inscrito = true;
        }

        // 1) Rotação: relógio próprio. Nunca depende do áudio.
        tempoMenu += Time.deltaTime;
        float fase = Mathf.Repeat(tempoMenu / Mathf.Max(0.01f, duracaoLoop), 1f);
        float angulo = fase * 360f * voltasPorLoop;
        rotationPivot.localRotation = rotacaoBase * Quaternion.AngleAxis(angulo, Vector3.up);

        // 2) Cores: pela batida quando o áudio realmente avança; senão, pelo timer.
        audioAvancando = AudioRealmenteAvancando(am);

        if (audioAvancando)
        {
            timerTroca = 0f;
        }
        else
        {
            timerTroca += Time.deltaTime;
            if (timerTroca >= intervaloTroca)
            {
                timerTroca -= intervaloTroca;
                SortearNovasCores();
            }
        }

        if (transicionando)
            AtualizarTransicao();
    }

    /// <summary>
    /// True só se a música está tocando E o relógio dela está de fato andando.
    /// Cobre o caso do WebGL: isPlaying = true, mas o tempo do áudio parado
    /// até o jogador interagir com a página.
    /// </summary>
    private bool AudioRealmenteAvancando(AudioManager am)
    {
        if (am == null || !am.IsPlaying)
        {
            ultimoNormalizado = -1f;
            tempoSemAvanco = 0f;
            return false;
        }

        float n = am.Normalized;

        if (Mathf.Abs(n - ultimoNormalizado) > 0.00001f)
        {
            ultimoNormalizado = n;
            tempoSemAvanco = 0f;
            return true;
        }

        tempoSemAvanco += Time.unscaledDeltaTime;
        return tempoSemAvanco < 0.5f;
    }

    private void SortearNovasCores()
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            coresOrigem[i] = coresDestino[i];
            coresDestino[i] = ColorUtils.RandomColorNoBlack(minBrightness);
        }

        if (transicaoSuave)
        {
            timerTransicao = 0f;
            transicionando = true;
        }
        else
        {
            for (int i = 0; i < blocks.Count; i++)
                blocks[i].SetColor(coresDestino[i]);
        }
    }

    private void AtualizarTransicao()
    {
        timerTransicao += Time.deltaTime;
        float t = Mathf.Clamp01(timerTransicao / Mathf.Max(0.01f, duracaoTransicao));

        for (int i = 0; i < blocks.Count; i++)
            blocks[i].SetColor(Color.Lerp(coresOrigem[i], coresDestino[i], t));

        if (t >= 1f)
            transicionando = false;
    }
}