using System;
using UnityEngine;

/// <summary>
/// CHROMA BLITZ - gerenciador de áudio único (música em stems + SFX + relógio de batidas).
///
/// - Persiste entre cenas (DontDestroyOnLoad). Coloque UMA vez na primeira cena (MainMenu).
/// - Música: 7 stems tocados em sincronia por amostra (PlayScheduled), com estados e fades.
/// - SFX: pool de AudioSources, com variação leve de pitch/volume a cada disparo.
/// - Ritmo: eventos OnBeat / OnBar calculados a partir da posição REAL da música
///   (continuam corretos mesmo se a velocidade/pitch da música mudar).
/// - Volumes (master/música/sfx) salvos em PlayerPrefs.
/// </summary>
[DefaultExecutionOrder(-200)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum MusicState { Menu, Options, Starting, Gameplay, Paused, GameOver, Silent }

    enum Stem { Bass, Arp, Pad, Lead, Drums, Fx, Vhs }
    const int StemCount = 7;

    // ---------------------------------------------------------------- Inspector
    [Header("Música - stems (todos com o mesmo tamanho)")]
    public AudioClip bass;
    public AudioClip arp;
    public AudioClip pad;
    public AudioClip lead;
    public AudioClip drums;
    public AudioClip fx;
    public AudioClip vhs;

    [Header("Ritmo da música")]
    public float bpm = 75f;
    public int beatsPerBar = 4;

    [Header("SFX de interface")]
    public AudioClip[] hover;      // ui_hover_1..3 (sorteia sem repetir seguido)
    public AudioClip click;        // ui_click
    public AudioClip back;         // ui_back
    public AudioClip error;        // ui_error   (cor errada)
    public AudioClip correct;      // ui_correct (cor certa)
    public AudioClip jingle;       // hook_jingle

    [Header("Notas das cores (vermelho, amarelo, verde, ciano, azul, roxo)")]
    public AudioClip[] colorNotes;

    [Header("Volumes")]
    [Range(0f, 1f)] public float master = 1f;
    [Range(0f, 1f)] public float music = 1f;
    [Range(0f, 1f)] public float sfx = 1f;
    [Range(0f, 1f)] public float vhsIntensity = 1f;   // chiado de fita (ligue ao shader de VHS)

    [Header("Estado inicial / SFX")]
    public MusicState initialState = MusicState.Menu;
    public int sfxPoolSize = 10;

    [Header("Velocidade da música (gameplay)")]
    [Tooltip("Acima de 1.25 o pitch sobe mais de 4 semitons; prefira ficar até 1.25.")]
    public float maxMusicSpeed = 1.25f;

    [Header("Filtro 'abafado' (Opções / Pause / Game Over)")]
    public float openCutoff = 22000f;

    // ---------------------------------------------------------------- Eventos de ritmo
    /// <summary>Disparado a cada batida. Parâmetro = batida dentro do loop (0..LoopBeats-1).</summary>
    public event Action<int> OnBeat;
    /// <summary>Disparado a cada compasso. Parâmetro = compasso dentro do loop.</summary>
    public event Action<int> OnBar;

    // ---------------------------------------------------------------- Estado interno
    class Layer
    {
        public AudioSource src;
        public AudioLowPassFilter lp;
        public float cur, target, speed;
    }

    const string KeyMaster = "audio_master";
    const string KeyMusic = "audio_music";
    const string KeySfx = "audio_sfx";

    readonly Layer[] _layers = new Layer[StemCount];
    Layer _ref;                       // stem usado como relógio
    AudioSource[] _pool;
    float[] _poolTime;

    float _duck = 1f, _duckTarget = 1f;
    float _cutoff, _cutoffTarget, _cutoffSpeed;
    float _speed = 1f, _speedTarget = 1f, _speedRate = 1f;

    double _startDsp;
    bool _started;
    int _lastBeat = -1;
    int _lastHover = -1;

    // ---------------------------------------------------------------- Propriedades públicas
    /// <summary>True quando a música já começou a tocar.</summary>
    public bool IsPlaying => _started && _ref != null && AudioSettings.dspTime >= _startDsp;

    /// <summary>Posição atual dentro do loop, em segundos (já considera pitch/velocidade).</summary>
    public float MusicTime
    {
        get
        {
            if (!IsPlaying || _ref.src.clip == null) return 0f;
            return _ref.src.timeSamples / (float)_ref.src.clip.frequency;
        }
    }

    public float LoopLength => (_ref != null && _ref.src.clip != null) ? _ref.src.clip.length : 76.8f;

    /// <summary>Posição no loop de 0 a 1 (use para girar a plataforma em sincronia).</summary>
    public float Normalized => LoopLength > 0f ? Mathf.Repeat(MusicTime / LoopLength, 1f) : 0f;

    public float BeatLength => 60f / Mathf.Max(1f, bpm);
    public int LoopBeats => Mathf.RoundToInt(LoopLength / BeatLength);

    /// <summary>Progresso dentro da batida atual: 0 = acabou de bater, 1 = próxima batida.</summary>
    public float BeatProgress => Mathf.Repeat(MusicTime / BeatLength, 1f);

    public float MasterVolume => master;
    public float MusicVolume => music;
    public float SfxVolume => sfx;
    public float MusicSpeed => _speed;

    // ---------------------------------------------------------------- Unity
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        master = PlayerPrefs.GetFloat(KeyMaster, master);
        music = PlayerPrefs.GetFloat(KeyMusic, music);
        sfx = PlayerPrefs.GetFloat(KeySfx, sfx);

        _cutoff = _cutoffTarget = openCutoff;
        BuildLayers();
        BuildSfxPool();
    }

    void Start()
    {
        if (Instance != this) return;
        RestartMusic();
        SetState(initialState, 0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (Instance != this) return;
        float dt = Time.unscaledDeltaTime;

        _duck = Mathf.MoveTowards(_duck, _duckTarget, dt * 1.5f);
        _cutoff = Mathf.MoveTowards(_cutoff, _cutoffTarget, _cutoffSpeed * dt);
        _speed = Mathf.MoveTowards(_speed, _speedTarget, _speedRate * dt);

        for (int i = 0; i < StemCount; i++)
        {
            var l = _layers[i];
            if (l == null) continue;
            l.cur = Mathf.MoveTowards(l.cur, l.target, l.speed * dt);

            float v = l.cur * _duck * master * music;
            if (i == (int)Stem.Vhs) v *= vhsIntensity;

            l.src.volume = v;
            l.src.pitch = _speed;
            l.lp.cutoffFrequency = _cutoff;
        }

        UpdateBeatClock();
    }

    // ---------------------------------------------------------------- Montagem
    void BuildLayers()
    {
        AudioClip[] clips = { bass, arp, pad, lead, drums, fx, vhs };
        for (int i = 0; i < StemCount; i++)
        {
            if (clips[i] == null)
            {
                Debug.LogWarning($"AudioManager: stem '{(Stem)i}' sem clip.");
                continue;
            }

            var go = new GameObject("Stem_" + (Stem)i);
            go.transform.SetParent(transform, false);

            var s = go.AddComponent<AudioSource>();
            s.clip = clips[i];
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;

            var lp = go.AddComponent<AudioLowPassFilter>();
            lp.cutoffFrequency = openCutoff;

            _layers[i] = new Layer { src = s, lp = lp };
        }

        // relógio: prefere o pad (sempre presente), senão o primeiro que existir
        _ref = _layers[(int)Stem.Pad];
        for (int i = 0; _ref == null && i < StemCount; i++) _ref = _layers[i];
    }

    void BuildSfxPool()
    {
        int n = Mathf.Max(2, sfxPoolSize);
        _pool = new AudioSource[n];
        _poolTime = new float[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Sfx_" + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;   // cliques funcionam mesmo com AudioListener.pause
            _pool[i] = s;
        }
    }

    // ---------------------------------------------------------------- Música
    /// <summary>Reinicia todos os stems do começo, alinhados (começa com o "hit" do sting).</summary>
    public void RestartMusic()
    {
        _startDsp = AudioSettings.dspTime + 0.25;
        for (int i = 0; i < StemCount; i++)
        {
            var l = _layers[i];
            if (l == null) continue;
            l.src.Stop();
            l.src.timeSamples = 0;
            l.src.PlayScheduled(_startDsp);
        }
        _lastBeat = -1;
        _started = true;
    }

    struct Preset
    {
        public float[] v;     // Bass, Arp, Pad, Lead, Drums, Fx, Vhs
        public float cutoff;
        public float duck;
    }

    Preset GetPreset(MusicState s)
    {
        switch (s)
        {
            case MusicState.Options:
                return new Preset { v = new[] { 1f, 0.8f, 1f, 0.25f, 0f, 0.5f, 1f }, cutoff = 1800f, duck = 1f };
            case MusicState.Starting:
                return new Preset { v = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f }, cutoff = openCutoff, duck = 1f };
            case MusicState.Gameplay:
                // música ~3,7 dB mais baixa para os SFX e notas de cor aparecerem
                return new Preset { v = new[] { 1f, 1f, 0.8f, 0.5f, 1f, 0f, 1f }, cutoff = openCutoff, duck = 0.65f };
            case MusicState.Paused:
                return new Preset { v = new[] { 1f, 0.6f, 1f, 0.2f, 0.3f, 0f, 1f }, cutoff = 1400f, duck = 0.65f };
            case MusicState.GameOver:
                return new Preset { v = new[] { 0.6f, 0.4f, 1f, 0f, 0f, 0f, 1f }, cutoff = 2500f, duck = 0.8f };
            case MusicState.Silent:
                return new Preset { v = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f }, cutoff = openCutoff, duck = 1f };
            default: // Menu
                return new Preset { v = new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f }, cutoff = openCutoff, duck = 1f };
        }
    }

    /// <summary>Muda o "clima" da música com fade (segundos).</summary>
    public void SetState(MusicState state, float fadeSeconds = 1f)
    {
        var p = GetPreset(state);
        for (int i = 0; i < StemCount; i++)
        {
            var l = _layers[i];
            if (l == null) continue;
            l.target = Mathf.Clamp01(p.v[i]);
            if (fadeSeconds <= 0.001f) { l.cur = l.target; l.speed = 1000f; }
            else l.speed = Mathf.Abs(l.target - l.cur) / fadeSeconds;
        }

        _cutoffTarget = p.cutoff;
        _duckTarget = p.duck;
        if (fadeSeconds <= 0.001f)
        {
            _cutoff = p.cutoff;
            _duck = p.duck;
            _cutoffSpeed = 1e6f;
        }
        else
        {
            _cutoffSpeed = Mathf.Abs(p.cutoff - _cutoff) / fadeSeconds;
        }
    }

    /// <summary>
    /// Velocidade da música (1 = normal). Muda o pitch junto (efeito "fita acelerando", combina com o VHS).
    /// Use para acompanhar a dificuldade, ex.: SetMusicSpeed(1f + 0.25f * dificuldade01, 2f).
    /// </summary>
    public void SetMusicSpeed(float speed, float fadeSeconds = 1f)
    {
        _speedTarget = Mathf.Clamp(speed, 0.5f, Mathf.Max(1f, maxMusicSpeed));
        _speedRate = fadeSeconds <= 0.001f ? 1000f : Mathf.Abs(_speedTarget - _speed) / fadeSeconds;
        if (fadeSeconds <= 0.001f) _speed = _speedTarget;
    }

    /// <summary>Intensidade do chiado de VHS (0..1). Ligue ao valor do shader.</summary>
    public void SetVhsIntensity(float value) => vhsIntensity = Mathf.Clamp01(value);

    // ---------------------------------------------------------------- Relógio de batidas
    void UpdateBeatClock()
    {
        if (!IsPlaying) return;

        int cur = Mathf.FloorToInt(MusicTime / BeatLength);

        if (_lastBeat < 0)
        {
            FireBeat(cur);
            _lastBeat = cur;
            return;
        }
        if (cur == _lastBeat) return;

        if (cur > _lastBeat && cur - _lastBeat <= 4)
        {
            for (int b = _lastBeat + 1; b <= cur; b++) FireBeat(b);
        }
        else
        {
            FireBeat(cur);   // loop reiniciou (ou o frame demorou demais)
        }
        _lastBeat = cur;
    }

    void FireBeat(int beat)
    {
        OnBeat?.Invoke(beat);
        if (beatsPerBar > 0 && beat % beatsPerBar == 0) OnBar?.Invoke(beat / beatsPerBar);
    }

    // ---------------------------------------------------------------- SFX
    public void PlayHover()
    {
        if (hover == null || hover.Length == 0) return;
        int i;
        int guard = 0;
        do { i = UnityEngine.Random.Range(0, hover.Length); }
        while (hover.Length > 1 && i == _lastHover && ++guard < 8);
        _lastHover = i;
        Play(hover[i], 0.025f, 0.08f);
    }

    public void PlayClick() => Play(click, 0.02f, 0.06f);
    public void PlayBack() => Play(back, 0.02f, 0.06f);
    public void PlayError() => Play(error, 0.02f, 0.05f);
    public void PlayCorrect() => Play(correct, 0.015f, 0.05f);
    public void PlayJingle() => Play(jingle, 0f, 0f);

    /// <summary>Toca a nota da cor (0 vermelho, 1 amarelo, 2 verde, 3 ciano, 4 azul, 5 roxo).</summary>
    public void PlayColorNote(int colorIndex)
    {
        if (colorNotes == null || colorNotes.Length == 0) return;
        int n = colorNotes.Length;
        int i = ((colorIndex % n) + n) % n;
        // pitch quase fixo: as notas formam a melodia, não podem sair da escala
        Play(colorNotes[i], 0.003f, 0.08f);
    }

    void Play(AudioClip clip, float pitchJitter, float volumeJitter)
    {
        if (clip == null || _pool == null) return;

        int idx = -1;
        for (int i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].isPlaying) { idx = i; break; }
        }
        if (idx < 0)   // todos ocupados: rouba o mais antigo
        {
            float oldest = float.MaxValue;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_poolTime[i] < oldest) { oldest = _poolTime[i]; idx = i; }
            }
        }

        var s = _pool[idx];
        s.clip = clip;
        s.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
        s.volume = Mathf.Clamp01(master * sfx * (1f + UnityEngine.Random.Range(-volumeJitter, volumeJitter)));
        s.Play();
        _poolTime[idx] = Time.unscaledTime;
    }

    // ---------------------------------------------------------------- Volumes (ligue aos sliders)
    public void SetMasterVolume(float v)
    {
        master = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMaster, master);
    }

    public void SetMusicVolume(float v)
    {
        music = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMusic, music);
    }

    public void SetSfxVolume(float v)
    {
        sfx = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeySfx, sfx);
    }
}
