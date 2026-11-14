using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD do Chroma Blitz, montado por código (sem prefab): basta colocar este script
/// em um GameObject vazio da cena do jogo (ex.: "HUD").
///
/// Mostra:
/// - Painel RODADA (topo esquerdo), com um "pop" leve a cada rodada nova.
/// - Painel TEMPO (topo centro): barra + segundos. Durante o preparo vira
///   "GET READY" com contagem 3-2-1 em roxo (calmo), e depois vira a barra verde.
/// - Painel ALVO (topo direito): a COR-ALVO que o jogador precisa encontrar.
///   Ela NÃO é mais o fundo do jogo: o fundo agora é neutro e fixo.
/// - Mensagem embaixo: a regra do jogo e os controles (no preparo e nas primeiras
///   rodadas) ou, na derrota, a explicação de qual bloco era o certo.
/// - Entrada suave: a cena sai do preto e o HUD aparece em fade (sem estouro).
///
/// Os painéis são escuros e opacos DE PROPÓSITO, para ficarem legíveis sempre.
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Textos (troque para português se quiser)")]
    public string textoRodada = "ROUND";
    public string textoTempo = "TIME";
    public string textoPreparo = "GET READY";
    public string textoAlvo = "TARGET";
    public string textoRegra = "STAND ON THE TILE THAT MATCHES THE TARGET!";
    public string textoControles = "MOVE: WASD / ARROWS";
    public string textoDerrota = "TIME'S UP!";
    public string textoDerrotaExplicacao = "THE TILE THAT KEPT ITS COLOR WAS THE ANSWER";

    [Header("Dica de início")]
    [Tooltip("A dica fica visível durante o preparo e nas primeiras N rodadas.")]
    public int rodadasComDica = 3;
    [Tooltip("Distância da mensagem até a borda de baixo da tela (pixels, base 1920x1080).")]
    public float margemDica = 30f;

    [Header("Entrada suave")]
    [Tooltip("Tempo do fade-in da cena (sai do preto) e do HUD, em segundos.")]
    public float duracaoFadeIn = 0.8f;

    [Header("Visual")]
    [Tooltip("Opcional: fonte pixel (TMP Font Asset). Vazio = fonte padrão do TMP.")]
    public TMP_FontAsset font;
    [Tooltip("Ordem do Canvas. Aumente se o HUD ficar atrás de outro painel.")]
    public int sortingOrder = 5;

    [Header("Paleta (a mesma do menu)")]
    public Color corBorda = new Color(0.43f, 0.25f, 1.00f);            // roxo dos botões
    public Color corPainel = new Color(0.12f, 0.08f, 0.22f, 0.95f);     // miolo escuro dos botões
    public Color corPainelSombra = new Color(0.07f, 0.04f, 0.14f, 1f);  // linha interna
    public Color corTrilho = new Color(0.04f, 0.02f, 0.09f, 1f);
    public Color corVerde = new Color(0.24f, 0.86f, 0.52f);             // highlight do menu
    public Color corPerigo = new Color(0.95f, 0.25f, 0.25f);            // vermelho do cubo/derrota
    public Color corTextoSuave = new Color(1f, 1f, 1f, 0.7f);

    [Header("Painel da cor-alvo")]
    [Tooltip("Cor do quadro enquanto ainda não existe cor-alvo (preparo).")]
    public Color corAlvoVazia = new Color(0.14f, 0.12f, 0.20f);
    [Tooltip("Moldura clara em volta da cor-alvo, para ela se destacar do painel escuro.")]
    public Color corMolduraAlvo = new Color(0.92f, 0.92f, 0.96f);

    // --- runtime ---
    TextMeshProUGUI txtRodada;
    TextMeshProUGUI lblTempo;
    TextMeshProUGUI txtTempo;
    TextMeshProUGUI txtDica;
    TextMeshProUGUI txtControles;
    RectTransform barraFill;
    Image barraFillImg;
    Image quadroAlvo;
    RectTransform molduraAlvoRt;
    CanvasGroup grupoRaiz;
    CanvasGroup grupoDica;
    Image fadePreto;

    int ultimoUid = -1;
    float alphaDica;
    float tFadeIn;
    Color corAlvoAtual;
    Coroutine popRodadaCo;
    Coroutine popAlvoCo;

    void Awake()
    {
        BuildUI();
        corAlvoAtual = corAlvoVazia;
    }

    void Update()
    {
        AtualizarFadeIn();

        var gm = GameManager.Instance;
        if (gm == null) return;

        // Rodada/preparo novo (detecta pelo Uid, que nunca reseta: funciona também após TryAgain).
        if (gm.RodadaUid != ultimoUid)
        {
            ultimoUid = gm.RodadaUid;
            SetText(txtRodada, gm.Rodada.ToString("00"));

            if (popRodadaCo != null) StopCoroutine(popRodadaCo);
            popRodadaCo = StartCoroutine(Pop(txtRodada.rectTransform));

            if (gm.TemCorAlvo)
            {
                if (popAlvoCo != null) StopCoroutine(popAlvoCo);
                popAlvoCo = StartCoroutine(Pop(molduraAlvoRt));
            }
        }

        AtualizarPainelTempo(gm);
        AtualizarPainelAlvo(gm);
        AtualizarMensagem(gm);
    }

    // ------------------------------------------------------------------
    // ATUALIZAÇÕES
    // ------------------------------------------------------------------
    void AtualizarFadeIn()
    {
        if (tFadeIn >= duracaoFadeIn) return;

        tFadeIn += Time.unscaledDeltaTime;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tFadeIn / Mathf.Max(0.01f, duracaoFadeIn)));

        grupoRaiz.alpha = k;
        fadePreto.color = new Color(0f, 0f, 0f, 1f - k);

        if (tFadeIn >= duracaoFadeIn)
        {
            grupoRaiz.alpha = 1f;
            fadePreto.gameObject.SetActive(false);
        }
    }

    void AtualizarPainelTempo(GameManager gm)
    {
        if (gm.EmPreparo)
        {
            // Contagem calma: barra roxa que esvazia e número grande.
            float pp = Mathf.Clamp01(gm.Preparo01);
            barraFill.anchorMax = new Vector2(pp, 1f);
            barraFillImg.enabled = pp > 0.003f;
            barraFillImg.color = corBorda;

            SetText(lblTempo, textoPreparo);
            SetText(txtTempo, Mathf.CeilToInt(gm.PreparoRestante).ToString());
            return;
        }

        // Rodada valendo: barra verde que vai ficando vermelha perto do fim.
        float p = Mathf.Clamp01(gm.Progresso01);
        barraFill.anchorMax = new Vector2(p, 1f);
        barraFillImg.enabled = p > 0.003f;
        barraFillImg.color = Color.Lerp(corPerigo, corVerde, Mathf.InverseLerp(0.15f, 0.45f, p));

        SetText(lblTempo, textoTempo);
        SetText(txtTempo, gm.TempoRestante.ToString("0.0", CultureInfo.InvariantCulture) + "s");
    }

    void AtualizarPainelAlvo(GameManager gm)
    {
        // Mostra a cor-alvo exata (a última continua visível na pausa e na derrota).
        // Um deslizar bem curto evita o "estouro" de cor, sem atrasar a leitura.
        Color alvo = gm.TemCorAlvo ? gm.CorAlvo : corAlvoVazia;
        alvo.a = 1f;

        float suavizar = 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime);
        corAlvoAtual = Color.Lerp(corAlvoAtual, alvo, suavizar);
        quadroAlvo.color = corAlvoAtual;
    }

    void AtualizarMensagem(GameManager gm)
    {
        bool derrota = gm.FimDeJogo;
        bool dicaInicio = !derrota && (gm.EmPreparo || (gm.Rodada >= 1 && gm.Rodada <= rodadasComDica));
        bool mostrar = dicaInicio || derrota;

        string t1 = derrota ? textoDerrota : textoRegra;
        string t2 = derrota ? textoDerrotaExplicacao : textoControles;
        Color c2 = derrota ? Color.white : corVerde;

        bool textoIgual = txtDica.text == t1;

        // Se o texto precisa mudar, primeiro some, depois troca, depois volta (sem corte seco).
        float alvo = (mostrar && textoIgual) ? 1f : 0f;
        alphaDica = Mathf.MoveTowards(alphaDica, alvo, Time.unscaledDeltaTime * 2.5f);

        if (mostrar && !textoIgual && alphaDica <= 0.01f)
        {
            txtDica.text = t1;
            txtControles.text = t2;
            txtControles.color = c2;
        }

        grupoDica.alpha = alphaDica;
    }

    static void SetText(TMP_Text t, string s)
    {
        if (t.text != s) t.text = s;
    }

    IEnumerator Pop(RectTransform rt)
    {
        const float duracao = 0.25f;
        float t = 0f;

        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duracao);
            rt.localScale = Vector3.one * (1f + 0.12f * k * k);   // leve, só para chamar atenção sem assustar
            yield return null;
        }

        rt.localScale = Vector3.one;
    }

    // ------------------------------------------------------------------
    // CONSTRUÇÃO DA UI (tudo por código)
    // ------------------------------------------------------------------
    void BuildUI()
    {
        // ---------- Canvas do HUD
        var canvasGo = new GameObject("HUDCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Começa invisível; o fade-in liga o HUD aos poucos.
        grupoRaiz = canvasGo.AddComponent<CanvasGroup>();
        grupoRaiz.alpha = 0f;
        grupoRaiz.interactable = false;
        grupoRaiz.blocksRaycasts = false;

        // Sem GraphicRaycaster de propósito: o HUD nunca bloqueia cliques.
        var root = (RectTransform)canvasGo.transform;
        RectTransform conteudo;

        // ---------- Painel RODADA (topo esquerdo)
        MakePanel(root, "PainelRodada",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -40f), new Vector2(300f, 100f),
            out conteudo);

        var lblRodada = MakeText(conteudo, "Label", textoRodada, 26f, corTextoSuave, TextAlignmentOptions.MidlineLeft);
        Place(lblRodada.rectTransform,
            new Vector2(0f, 0.58f), Vector2.one,
            new Vector2(18f, 0f), new Vector2(-10f, -6f));

        txtRodada = MakeText(conteudo, "Valor", "00", 56f, Color.white, TextAlignmentOptions.MidlineLeft);
        Place(txtRodada.rectTransform,
            Vector2.zero, new Vector2(1f, 0.62f),
            new Vector2(18f, 6f), new Vector2(-10f, 0f),
            new Vector2(0f, 0.5f));   // pivô à esquerda: o "pop" cresce para a direita

        // ---------- Painel TEMPO (topo centro)
        MakePanel(root, "PainelTempo",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -40f), new Vector2(760f, 100f),
            out conteudo);

        lblTempo = MakeText(conteudo, "Label", textoTempo, 26f, corTextoSuave, TextAlignmentOptions.MidlineLeft);
        Place(lblTempo.rectTransform,
            new Vector2(0f, 0.55f), Vector2.one,
            new Vector2(18f, 0f), new Vector2(-18f, -6f));

        txtTempo = MakeText(conteudo, "Segundos", "0.0s", 30f, Color.white, TextAlignmentOptions.MidlineRight);
        Place(txtTempo.rectTransform,
            new Vector2(0f, 0.55f), Vector2.one,
            new Vector2(18f, 0f), new Vector2(-18f, -6f));

        Image trilho = MakeImage(conteudo, "Trilho", corTrilho);
        Place(trilho.rectTransform,
            Vector2.zero, new Vector2(1f, 0.5f),
            new Vector2(18f, 12f), new Vector2(-18f, -2f));

        Image fill = MakeImage(trilho.rectTransform, "Fill", corVerde);
        Place(fill.rectTransform,
            Vector2.zero, Vector2.one,
            new Vector2(3f, 3f), new Vector2(-3f, -3f));

        barraFill = fill.rectTransform;
        barraFillImg = fill;

        // ---------- Painel ALVO (topo direito): a cor que o jogador precisa encontrar
        MakePanel(root, "PainelAlvo",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-40f, -40f), new Vector2(380f, 100f),
            out conteudo);

        var lblAlvo = MakeText(conteudo, "Label", textoAlvo, 26f, corTextoSuave, TextAlignmentOptions.MidlineLeft);
        Place(lblAlvo.rectTransform,
            new Vector2(0f, 0.55f), Vector2.one,
            new Vector2(18f, 0f), new Vector2(-18f, -6f));

        Image moldura = MakeImage(conteudo, "Moldura", corMolduraAlvo);
        Place(moldura.rectTransform,
            Vector2.zero, new Vector2(1f, 0.5f),
            new Vector2(18f, 12f), new Vector2(-18f, -2f));

        quadroAlvo = MakeImage(moldura.rectTransform, "CorAlvo", corAlvoVazia);
        Place(quadroAlvo.rectTransform,
            Vector2.zero, Vector2.one,
            new Vector2(3f, 3f), new Vector2(-3f, -3f));

        molduraAlvoRt = moldura.rectTransform;

        // ---------- Mensagem (embaixo, centro): regra do jogo ou explicação da derrota
        RectTransform raizDica = MakePanel(root, "PainelDica",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, margemDica), new Vector2(1280f, 120f),
            out conteudo);

        grupoDica = raizDica.gameObject.AddComponent<CanvasGroup>();
        grupoDica.alpha = 0f;
        grupoDica.interactable = false;
        grupoDica.blocksRaycasts = false;

        txtDica = MakeText(conteudo, "Dica", textoRegra, 40f, Color.white, TextAlignmentOptions.Center);
        Place(txtDica.rectTransform,
            new Vector2(0f, 0.45f), Vector2.one,
            new Vector2(20f, 0f), new Vector2(-20f, -8f));

        txtControles = MakeText(conteudo, "Controles", textoControles, 28f, corVerde, TextAlignmentOptions.Center);
        Place(txtControles.rectTransform,
            Vector2.zero, new Vector2(1f, 0.45f),
            new Vector2(20f, 10f), new Vector2(-20f, 0f));

        // ---------- Fade preto de entrada (Canvas separado, por cima de tudo)
        var fadeGo = new GameObject("HUDFade", typeof(RectTransform));
        fadeGo.transform.SetParent(transform, false);

        var fadeCanvas = fadeGo.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 500;

        fadePreto = MakeImage((RectTransform)fadeGo.transform, "Preto", Color.black);
        Inset(fadePreto.rectTransform, 0f);
    }

    /// <summary>
    /// Painel no estilo dos botões do menu: borda roxa, linha interna escura e miolo.
    /// Retorna a raiz (borda); "conteudo" é o miolo, onde vão os filhos.
    /// </summary>
    RectTransform MakePanel(RectTransform parent, string nome,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size,
        out RectTransform conteudo)
    {
        Image borda = MakeImage(parent, nome, corBorda);
        RectTransform rt = borda.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image sombra = MakeImage(rt, "Sombra", corPainelSombra);
        Inset(sombra.rectTransform, 4f);

        Image fundo = MakeImage(sombra.rectTransform, "Fundo", corPainel);
        Inset(fundo.rectTransform, 3f);

        conteudo = fundo.rectTransform;
        return rt;
    }

    Image MakeImage(RectTransform parent, string nome, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI MakeText(RectTransform parent, string nome, string texto,
        float tamanho, Color cor, TextAlignmentOptions alinhamento)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.color = cor;
        t.alignment = alinhamento;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;

        // Auto-ajuste: o texto sempre cabe na caixa, mesmo com fontes pixel largas.
        t.enableAutoSizing = true;
        t.fontSizeMax = tamanho;
        t.fontSizeMin = tamanho * 0.4f;
        t.fontSize = tamanho;

        if (font) t.font = font;
        else t.fontStyle = FontStyles.Bold;   // fonte pixel não tem negrito de verdade

        return t;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Vector2? pivot = null)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void Inset(RectTransform rt, float px)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(px, px);
        rt.offsetMax = new Vector2(-px, -px);
    }
}