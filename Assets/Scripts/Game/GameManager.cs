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

    [Header("Entrada calma (sem susto)")]
    [Tooltip("Preparo antes da primeira rodada, em segundos. O jogador já pode andar.")]
    public float tempoPreparo = 3f;

    [Tooltip("Preparo depois do TryAgain, em segundos.")]
    public float tempoPreparoRetry = 1.5f;

    [Tooltip("Cor do fundo do jogo (neutra e escura). A cor-alvo NÃO usa mais o fundo: ela aparece no HUD.")]
    public Color corFundo = new Color(0.10f, 0.10f, 0.12f);

    [Tooltip("Tempo para o fundo mudar de cor ao entrar no preparo, em segundos.")]
    public float duracaoTransicaoFundo = 0.25f;

    [Header("Feedback visual")]
    [Tooltip("Cor dos blocos entre as rodadas. Dica: um cinza suave incomoda menos que branco puro.")]
    public Color corPadraoBlocos = Color.white;

    public Color corVitoria = new Color(0.24f, 0.86f, 0.52f);

    [Tooltip("Duração do pulso suave de vitória, em segundos.")]
    public float duracaoPulsoVitoria = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("Quanto o pulso chega perto da cor de vitória (0 = nada, 1 = cor cheia).")]
    public float forcaPulsoVitoria = 0.55f;

    [Tooltip("Pausa depois da vitória antes da próxima rodada")]
    public float pausaAposVitoria = 0.5f;

    [Header("Revelação da derrota")]
    [Tooltip("Cor para onde os blocos errados vão 'apagando'.")]
    public Color corBlocosApagados = new Color(0.25f, 0.25f, 0.30f);

    [Tooltip("Tempo do apagar suave dos blocos errados, em segundos.")]
    public float duracaoRevelacao = 0.6f;

    [Tooltip("Espera antes de mostrar o painel de Game Over, para o jogador ver o bloco certo.")]
    public float atrasoGameOver = 0.9f;

    [Header("Eventos")]
    public UnityEvent onRoundWon;
    public UnityEvent onRoundLost;
    public UnityEvent onGameOver;

    private readonly List<GridBlock> blocks = new List<GridBlock>();

    // Blocos atualmente tocados pelo Player.
    // Atualizado pelos triggers SEMPRE (inclusive entre rodadas): o trigger só
    // dispara ao ENTRAR, então quem ficou parado num bloco não pode ser "esquecido".
    private readonly List<GridBlock> currentBlocks = new List<GridBlock>();

    private GridBlock blocoCorreto;

    private Color targetColor;
    private float tempoAtual;

    private Coroutine preparoCoroutine;
    private Coroutine timerCoroutine;
    private Coroutine derrotaCoroutine;
    private Coroutine fundoCoroutine;

    private bool roundActive;
    private int rodada;

    private float inicioRodada;
    private int rodadaUid;
    private bool fimDeJogo;

    private bool emPreparo;
    private float preparoFim;
    private float preparoTotal;

    // =========================================================
    // LEITURA PARA O HUD (somente leitura)
    // =========================================================

    /// <summary>Número da rodada atual (0 durante o preparo, depois 1, 2, 3...).</summary>
    public int Rodada => rodada;

    /// <summary>Contador que sobe a cada rodada/preparo e nunca reseta (detecta "algo novo").</summary>
    public int RodadaUid => rodadaUid;

    public bool RodadaAtiva => roundActive;

    /// <summary>Cor que o jogador precisa encontrar (mostrada no HUD, não no fundo).</summary>
    public Color CorAlvo => targetColor;

    /// <summary>False no preparo antes da 1ª rodada (ainda não existe cor-alvo).</summary>
    public bool TemCorAlvo => rodada >= 1;

    /// <summary>True depois que o jogador errou, até o TryAgain.</summary>
    public bool FimDeJogo => fimDeJogo;

    /// <summary>True enquanto o jogo "respira" antes de valer (contagem inicial).</summary>
    public bool EmPreparo => emPreparo;

    public float PreparoRestante =>
        emPreparo ? Mathf.Max(0f, preparoFim - Time.time) : 0f;

    /// <summary>1 = preparo acabou de começar, 0 = prestes a valer.</summary>
    public float Preparo01 =>
        preparoTotal > 0f ? Mathf.Clamp01(PreparoRestante / preparoTotal) : 0f;

    /// <summary>Duração total da rodada atual, em segundos.</summary>
    public float TempoDaRodada => tempoAtual;

    public float TempoRestante =>
        roundActive
            ? Mathf.Max(0f, tempoAtual - (Time.time - inicioRodada))
            : 0f;

    /// <summary>1 = rodada acabou de começar, 0 = tempo esgotado.</summary>
    public float Progresso01 =>
        tempoAtual > 0f
            ? Mathf.Clamp01(TempoRestante / tempoAtual)
            : 0f;

    // =========================================================
    // UNITY
    // =========================================================

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

        // Fundo neutro e fixo durante todo o jogo (a cor-alvo vive no HUD).
        if (Camera.main == null)
        {
            Debug.LogError(
                "GameManager: nenhuma câmera com a tag MainCamera foi encontrada."
            );
        }
        else
        {
            MudarFundo(corFundo, 0f);
        }

        // Em vez de valer já no primeiro frame, o jogo começa com um preparo calmo.
        IniciarPreparo(tempoPreparo);
    }

    // =========================================================
    // PREPARO (contagem antes de valer)
    // =========================================================

    private void IniciarPreparo(float duracao)
    {
        if (preparoCoroutine != null)
        {
            StopCoroutine(preparoCoroutine);
        }

        preparoCoroutine = StartCoroutine(PreparoRoutine(duracao));
    }

    private IEnumerator PreparoRoutine(float duracao)
    {
        emPreparo = true;
        fimDeJogo = false;
        roundActive = false;
        rodadaUid++;

        preparoTotal = Mathf.Max(0.01f, duracao);
        preparoFim = Time.time + preparoTotal;

        // Tabuleiro neutro e fundo calmo: nenhuma cor aleatória até valer.
        foreach (GridBlock block in blocks)
        {
            block.SetColor(corPadraoBlocos);
        }

        MudarFundo(corFundo, duracaoTransicaoFundo);

        yield return new WaitForSeconds(preparoTotal);

        emPreparo = false;
        NovaRodada();
    }

    // =========================================================
    // RODADA
    // =========================================================

    public void NovaRodada()
    {
        rodada++;
        rodadaUid++;
        fimDeJogo = false;
        emPreparo = false;
        roundActive = true;

        // Sorteia a cor que será procurada.
        targetColor =
            ColorUtils.RandomColorNoBlack(minBrightness);

        // A cor-alvo agora aparece no HUD (CorAlvo); o fundo permanece neutro.

        // Sorteia as cores dos blocos.
        foreach (GridBlock block in blocks)
        {
            block.SetColor(
                ColorUtils.RandomColorNoBlack(minBrightness)
            );
        }

        // Garante que exista pelo menos um bloco correto.
        int indiceCorreto = Random.Range(0, blocks.Count);

        blocoCorreto = blocks[indiceCorreto];
        blocoCorreto.SetColor(targetColor);

        Debug.Log(
            $"Rodada {rodada} iniciada | " +
            $"Tempo: {tempoAtual:F2}s | " +
            $"Bloco correto: {blocoCorreto.name}"
        );

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        inicioRodada = Time.time;

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
    // FUNDO (transição suave)
    // =========================================================

    private void MudarFundo(Color alvo, float duracao)
    {
        if (fundoCoroutine != null)
        {
            StopCoroutine(fundoCoroutine);
            fundoCoroutine = null;
        }

        Camera cam = Camera.main;

        if (cam == null)
            return;

        if (duracao <= 0.001f)
        {
            cam.backgroundColor = alvo;
            return;
        }

        fundoCoroutine = StartCoroutine(TransicaoFundo(cam, alvo, duracao));
    }

    private IEnumerator TransicaoFundo(Camera cam, Color alvo, float duracao)
    {
        Color origem = cam.backgroundColor;
        float t = 0f;

        while (t < duracao)
        {
            t += Time.deltaTime;

            cam.backgroundColor = Color.Lerp(
                origem,
                alvo,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracao))
            );

            yield return null;
        }

        cam.backgroundColor = alvo;
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

        // Registra SEMPRE, mesmo fora de rodada (preparo, pausa pós-vitória, game over...).
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

        // Um único pulso suave (sobe e desce), em vez de piscadas secas.
        yield return StartCoroutine(
            PulsoBlocos(corVitoria, duracaoPulsoVitoria, forcaPulsoVitoria)
        );

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

    /// <summary>
    /// Mistura os blocos com uma cor e volta, em curva de seno (0 -> 1 -> 0).
    /// Nunca chega na cor cheia (limitado por "forca") e nunca corta de uma vez.
    /// </summary>
    private IEnumerator PulsoBlocos(Color cor, float duracao, float forca)
    {
        if (duracao > 0.001f)
        {
            float t = 0f;

            while (t < duracao)
            {
                t += Time.deltaTime;

                float sino = Mathf.Sin(Mathf.Clamp01(t / duracao) * Mathf.PI);

                foreach (GridBlock block in blocks)
                {
                    block.SetVisualColor(
                        Color.Lerp(block.BlockColor, cor, forca * sino)
                    );
                }

                yield return null;
            }
        }

        foreach (GridBlock block in blocks)
        {
            block.SetVisualColor(block.BlockColor);
        }
    }

    // =========================================================
    // DERROTA
    // =========================================================

    private IEnumerator DerrotaRoutine()
    {
        roundActive = false;
        fimDeJogo = true;

        onRoundLost?.Invoke();

        // Sem piscar: a tela "apaga" e mostra qual era o bloco certo.
        derrotaCoroutine = StartCoroutine(RevelarDerrota());

        yield break;
    }

    private IEnumerator RevelarDerrota()
    {
        float duracao = Mathf.Max(0.01f, duracaoRevelacao);
        float t = 0f;

        while (t < duracao)
        {
            t += Time.deltaTime;

            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracao));

            foreach (GridBlock block in blocks)
            {
                // O bloco certo mantém a cor verdadeira (igual à cor-alvo do HUD);
                // todos os outros apagam suavemente.
                if (block == blocoCorreto)
                    continue;

                block.SetVisualColor(
                    Color.Lerp(block.BlockColor, corBlocosApagados, 0.75f * k)
                );
            }

            yield return null;
        }

        // Dá um tempo para o jogador entender o que aconteceu antes do painel.
        yield return new WaitForSeconds(atrasoGameOver);

        onGameOver?.Invoke();
    }

    // =========================================================
    // REINICIAR
    // =========================================================

    public void ReiniciarJogo()
    {
        if (derrotaCoroutine != null)
        {
            StopCoroutine(derrotaCoroutine);
            derrotaCoroutine = null;
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
        blocoCorreto = null;

        IniciarPreparo(tempoPreparoRetry);
    }
}