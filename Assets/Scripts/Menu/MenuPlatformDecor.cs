
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Efeito decorativo do menu principal:
/// - Rotaciona a plataforma através de um Pivot independente.
/// - Altera as cores dos blocos em intervalos definidos.
/// - Permite transições suaves entre as cores.
/// </summary>
public class MenuPlatformDecor : MonoBehaviour
{
    [Header("Referências")]

    [Tooltip("Objeto pai que contém os GridBlocks.")]
    public Transform blocksParent;

    [Tooltip("Objeto responsável pela rotação da plataforma.")]
    public Transform rotationPivot;


    [Header("Rotação")]

    [Tooltip("Velocidade de rotação em graus por segundo.")]
    public float velocidadeRotacao = 20f;


    [Header("Cores")]

    [Tooltip("Intervalo entre cada troca de cores, em segundos.")]
    public float intervaloTroca = 1f;

    [Range(0f, 1f)]
    public float minBrightness = 0.3f;

    [Tooltip("Se ativado, as cores mudam suavemente.")]
    public bool transicaoSuave = false;

    [Tooltip("Duração da transição suave em segundos.")]
    public float duracaoTransicao = 0.35f;


    private readonly List<GridBlock> blocks = new List<GridBlock>();

    private Color[] coresOrigem;
    private Color[] coresDestino;

    private float timerTroca;
    private float timerTransicao;

    private bool transicionando;


    private void Awake()
    {
        if (blocksParent == null)
            blocksParent = transform;

        if (rotationPivot == null)
        {
            Debug.LogError(
                "MenuPlatformDecor: Rotation Pivot não foi configurado.",
                this
            );

            enabled = false;
            return;
        }

        blocks.AddRange(
            blocksParent.GetComponentsInChildren<GridBlock>()
        );

        if (blocks.Count == 0)
        {
            Debug.LogError(
                "MenuPlatformDecor: nenhum GridBlock encontrado.",
                this
            );

            enabled = false;
            return;
        }

        coresOrigem = new Color[blocks.Count];
        coresDestino = new Color[blocks.Count];
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


    private void Update()
    {
        // ROTACAO INDEPENDENTE PELO PIVOT.
        rotationPivot.Rotate(
            Vector3.up,
            velocidadeRotacao * Time.deltaTime,
            Space.Self
        );


        // CONTROLE DA TROCA DE CORES.
        timerTroca += Time.deltaTime;

        if (timerTroca >= intervaloTroca)
        {
            timerTroca -= intervaloTroca;
            SortearNovasCores();
        }


        if (transicionando)
            AtualizarTransicao();
    }


    private void SortearNovasCores()
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            coresOrigem[i] = coresDestino[i];

            coresDestino[i] =
                ColorUtils.RandomColorNoBlack(minBrightness);
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

        float t = Mathf.Clamp01(
            timerTransicao / Mathf.Max(0.01f, duracaoTransicao)
        );


        for (int i = 0; i < blocks.Count; i++)
        {
            Color corAtual = Color.Lerp(
                coresOrigem[i],
                coresDestino[i],
                t
            );

            blocks[i].SetColor(corAtual);
        }


        if (t >= 1f)
            transicionando = false;
    }
}
