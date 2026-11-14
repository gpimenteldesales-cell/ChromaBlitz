using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuração")]
    [Tooltip("Objeto pai que contém todos os blocos da fase")]
    public Transform blocksParent;

    [Tooltip("Brilho mínimo das cores sorteadas")]
    [Range(0f, 1f)]
    public float minBrightness = 0.3f;

    [Header("Detecção do player")]
    [Tooltip("Player usado para descobrir em que bloco ele está. Se vazio, procura pela tag Player.")]
    public Transform playerTransform;

    [Tooltip("Altura (acima do centro do player) de onde o raio parte para baixo.")]
    public float alturaRaio = 1f;

    [Header("Dificuldade")]
    public float tempoInicial = 3f;
    public float tempoMinimo = 0.6f;

    [Range(0.5f, 0.99f)]
    public float reducaoPorRodada = 0.95f;

    [Header("Feedback visual")]
    public Color corPadraoBlocos = Color.white;

    public int quantidadePiscadas = 3;
    public float intervaloPiscada = 0.15f;

    [Tooltip("Pausa depois da vitória antes da próxima rodada")]
    public float pausaAposVitoria = 0.5f;

    [Header("Eventos")]
    public UnityEvent onRoundWon;
    public UnityEvent onRoundLost;
    public UnityEvent onGameOver;

    private readonly List<GridBlock> blocks = new List<GridBlock>();

    // Blocos atualmente tocados pelo Player.
    // Atualizado pelos triggers SEMPRE (inclusive entre rodadas): o trigger só
    // dispara ao ENTRAR, então quem ficou parado num bloco não pode ser "esquecido".
    private readonly List<GridBlock> currentBlocks = new List<GridBlock>();

    private Color targetColor;
    private float tempoAtual;

    private Coroutine timerCoroutine;
    private Coroutine loopDerrotaCoroutine;

    private bool roundActive;
    private int rodada;

    private void Awake()
    {
        // Evita dois GameManagers na mesma cena.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (blocksParent == null)
        {
            Debug.LogError(
                "GameManager: blocksParent não foi definido no Inspector."
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
                "GameManager: nenhum GridBlock foi encontrado em blocksParent."
            );

            enabled = false;
            return;
        }

        tempoAtual = tempoInicial;
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");

            if (p != null)
            {
                playerTransform = p.transform;
            }
            else
            {
                Debug.LogWarning(
                    "GameManager: Player não encontrado (tag Player). " +
                    "Usando apenas os triggers para detectar o bloco."
                );
            }
        }

        NovaRodada();
    }

    public void NovaRodada()
    {
        rodada++;
        roundActive = true;

        // Sorteia a cor que será procurada.
        if (Camera.main == null)
        {
            Debug.LogError(
                "GameManager: nenhuma câmera com a tag MainCamera foi encontrada."
            );
        }
        else
        {
            targetColor =
                ColorUtils.RandomColorNoBlack(minBrightness);

            Camera.main.backgroundColor = targetColor;
        }

        // Sorteia as cores dos blocos.
        foreach (GridBlock block in blocks)
        {
            block.SetColor(
                ColorUtils.RandomColorNoBlack(minBrightness)
            );
        }

        // Garante que exista pelo menos um bloco correto.
        int indiceCorreto = Random.Range(0, blocks.Count);

        blocks[indiceCorreto].SetColor(targetColor);

        Debug.Log(
            $"Rodada {rodada} iniciada | " +
            $"Tempo: {tempoAtual:F2}s | " +
            $"Bloco correto: {blocks[indiceCorreto].name}"
        );

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        timerCoroutine = StartCoroutine(TimerRodada());
    }

    private IEnumerator TimerRodada()
    {
        yield return new WaitForSeconds(tempoAtual);

        if (!roundActive)
            yield break;

        // Verifica o bloco sob o player somente quando o tempo acaba.
        GridBlock blocoDoPlayer = BlocoSobPlayer();

        bool acertou =
            blocoDoPlayer != null &&
            ColorUtils.CoresIguais(
                blocoDoPlayer.BlockColor,
                targetColor
            );

        Debug.Log(
            $"Fim da rodada {rodada} | " +
            $"Bloco do player: {(blocoDoPlayer != null ? blocoDoPlayer.name : "nenhum")} | " +
            $"Acertou: {acertou}"
        );

        if (acertou)
        {
            // Revelação sonora somente após o fim da rodada.
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCorrect();

                // A nota correspondente à cor pode ser tocada aqui
                // quando tivermos a associação entre cor e índice.
            }

            yield return StartCoroutine(VitoriaRoutine());
        }
        else
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayError();
            }

            yield return StartCoroutine(DerrotaRoutine());
        }
    }

    // =========================================================
    // QUAL BLOCO ESTÁ SOB O PLAYER?
    // =========================================================

    /// <summary>
    /// Descobre o bloco sob o CENTRO do player.
    /// 1) Raio para baixo a partir do player (não depende de ter "entrado" no bloco).
    /// 2) Se o raio não achar nada, usa os blocos registrados pelos triggers
    ///    (o mais próximo do player; ou o último registrado se não houver referência).
    /// </summary>
    private GridBlock BlocoSobPlayer()
    {
        if (playerTransform != null)
        {
            Vector3 origem =
                playerTransform.position + Vector3.up * alturaRaio;

            RaycastHit[] hits = Physics.RaycastAll(
                origem,
                Vector3.down,
                alturaRaio + 5f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            GridBlock melhor = null;
            float menorDistancia = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                GridBlock gb =
                    hits[i].collider.GetComponentInParent<GridBlock>();

                if (gb != null && hits[i].distance < menorDistancia)
                {
                    menorDistancia = hits[i].distance;
                    melhor = gb;
                }
            }

            if (melhor != null)
                return melhor;
        }

        // Fallback: blocos registrados pelos triggers.
        if (currentBlocks.Count == 0)
            return null;

        if (playerTransform == null)
            return currentBlocks[currentBlocks.Count - 1];

        GridBlock maisPerto = null;
        float menorSqr = float.MaxValue;

        foreach (GridBlock b in currentBlocks)
        {
            Vector3 d = b.transform.position - playerTransform.position;
            d.y = 0f;

            if (d.sqrMagnitude < menorSqr)
            {
                menorSqr = d.sqrMagnitude;
                maisPerto = b;
            }
        }

        return maisPerto;
    }

    // =========================================================
    // PLAYER ENTROU
    // =========================================================

    public void OnPlayerEnteredBlock(GridBlock block)
    {
        if (block == null)
            return;

        // Registra SEMPRE, mesmo fora de rodada (pausa pós-vitória, game over...).
        // Quem avalia o resultado é o fim do timer, não esta função.
        if (!currentBlocks.Contains(block))
        {
            currentBlocks.Add(block);
        }

        Debug.Log(
            $"Entrou em {block.name} | " +
            $"Cor: {block.BlockColor}"
        );

        // IMPORTANTE:
        // Nenhum áudio é reproduzido aqui.
        // O jogador precisa descobrir a cor visualmente.
    }

    // =========================================================
    // PLAYER SAIU
    // =========================================================

    public void OnPlayerExitedBlock(GridBlock block)
    {
        if (block == null)
            return;

        currentBlocks.Remove(block);
    }

    // =========================================================
    // VITÓRIA
    // =========================================================

    private IEnumerator VitoriaRoutine()
    {
        roundActive = false;

        yield return StartCoroutine(PiscarBlocos(Color.green));

        foreach (GridBlock block in blocks)
        {
            block.SetColor(corPadraoBlocos);
        }

        onRoundWon?.Invoke();

        // Aumenta a dificuldade.
        tempoAtual = Mathf.Max(
            tempoMinimo,
            tempoAtual * reducaoPorRodada
        );

        yield return new WaitForSeconds(pausaAposVitoria);

        NovaRodada();
    }

    // =========================================================
    // DERROTA
    // =========================================================

    private IEnumerator DerrotaRoutine()
    {
        roundActive = false;

        onRoundLost?.Invoke();
        onGameOver?.Invoke();

        loopDerrotaCoroutine =
            StartCoroutine(
                PiscarBlocosInfinito(Color.red)
            );

        yield break;
    }

    private IEnumerator PiscarBlocosInfinito(Color corFeedback)
    {
        while (true)
        {
            foreach (GridBlock block in blocks)
            {
                block.SetVisualColor(corFeedback);
            }

            yield return new WaitForSeconds(intervaloPiscada);

            foreach (GridBlock block in blocks)
            {
                block.SetVisualColor(block.BlockColor);
            }

            yield return new WaitForSeconds(intervaloPiscada);
        }
    }

    private IEnumerator PiscarBlocos(Color corFeedback)
    {
        for (int i = 0; i < quantidadePiscadas; i++)
        {
            foreach (GridBlock block in blocks)
            {
                block.SetVisualColor(corFeedback);
            }

            yield return new WaitForSeconds(intervaloPiscada);

            foreach (GridBlock block in blocks)
            {
                block.SetVisualColor(block.BlockColor);
            }

            yield return new WaitForSeconds(intervaloPiscada);
        }
    }

    // =========================================================
    // REINICIAR
    // =========================================================

    public void ReiniciarJogo()
    {
        if (loopDerrotaCoroutine != null)
        {
            StopCoroutine(loopDerrotaCoroutine);
            loopDerrotaCoroutine = null;
        }

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        // currentBlocks NÃO é limpo de propósito: ele reflete a posição física do
        // player (triggers). Limpar faria o jogo esquecer quem continua parado
        // em cima de um bloco, pois o trigger não dispara de novo.

        tempoAtual = tempoInicial;
        rodada = 0;
        roundActive = false;

        NovaRodada();
    }
}