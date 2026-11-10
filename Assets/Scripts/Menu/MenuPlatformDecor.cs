using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Efeito decorativo do menu principal, sincronizado com a música:
/// - Gira o pivot em sincronia com o loop da música.
/// - Troca as cores dos blocos a cada N batidas (0,8 s a 75 BPM).
/// - Se não houver AudioManager, usa o timer interno (intervaloTroca).
/// </summary>
public class MenuPlatformDecor : MonoBehaviour
{
    [Header("Referências")]
    public Transform blocksParent;
    public Transform rotationPivot;

    [Header("Rotação")]
    [Tooltip("Voltas completas por loop da música (76,8 s). Usado quando a música está tocando.")]
    public float voltasPorLoop = 1f;
    [Tooltip("Graus por segundo. Usado só se não houver AudioManager.")]
    public float velocidadeRotacao = 20f;

    [Header("Cores")]
    [Min(1)] public int batidasPorTroca = 1;
    [Tooltip("Intervalo de reserva (sem AudioManager), em segundos.")]
    public float intervaloTroca = 0.8f;
    [Range(0f, 1f)] public float minBrightness = 0.3f;
    public bool transicaoSuave = false;
    public float duracaoTransicao = 0.35f;

    private readonly List<GridBlock> blocks = new List<GridBlock>();
    private Color[] coresOrigem;
    private Color[] coresDestino;

    private Quaternion rotacaoBase;
    private float timerTroca;
    private float timerTransicao;
    private bool transicionando;
    private bool inscrito;

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

        if (am != null && am.IsPlaying)
        {
            // rotação e cores seguem a música
            float angulo = am.Normalized * 360f * voltasPorLoop;
            rotationPivot.localRotation = rotacaoBase * Quaternion.AngleAxis(angulo, Vector3.up);
        }
        else
        {
            // reserva: comportamento antigo, com timer
            rotationPivot.Rotate(Vector3.up, velocidadeRotacao * Time.deltaTime, Space.Self);

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