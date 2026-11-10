using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Menu principal do Chroma Blitz.
/// Ligue os botões aos métodos públicos (Play, OpenOptions, CloseOptions, Quit)
/// pelo evento OnClick de cada Button.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Cena do jogo (precisa estar em File > Build Settings)")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Painéis")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject optionsPanel;

    [Header("Botão Sair (escondido no WebGL, onde Quit não funciona)")]
    [SerializeField] private GameObject quitButton;

    [Header("Opções")]
    [SerializeField] private Slider volumeSlider;

    [Header("Transição (painel preto em tela cheia com CanvasGroup)")]
    [SerializeField] private CanvasGroup fadeGroup;
    [SerializeField] private float fadeDuration = 0.4f;

    private const string VolumeKey = "master_volume";
    private bool isLoading;

    private void Start()
    {
        Time.timeScale = 1f;

#if UNITY_WEBGL
        if (quitButton != null) quitButton.SetActive(false);
#endif

        float volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        AudioListener.volume = volume;
        if (volumeSlider != null)
        {
            volumeSlider.SetValueWithoutNotify(volume);
            volumeSlider.onValueChanged.AddListener(SetVolume);
        }

        CloseOptions();

        if (fadeGroup != null)
        {
            fadeGroup.alpha = 1f;
            StartCoroutine(Fade(0f));
        }
    }

    public void Play()
    {   
        if (!isLoading) StartCoroutine(LoadGame()); 
    }

    public void OpenOptions()
    {
        if (mainPanel != null) mainPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (mainPanel != null) mainPanel.SetActive(true);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
    }

    private IEnumerator LoadGame()
    {
        isLoading = true;
        yield return Fade(1f);
        SceneManager.LoadScene(gameSceneName);
    }

    private IEnumerator Fade(float target)
    {
        if (fadeGroup == null) yield break;

        fadeGroup.blocksRaycasts = true;
        float start = fadeGroup.alpha;
        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, t / fadeDuration);
            yield return null;
        }

        fadeGroup.alpha = target;
        fadeGroup.blocksRaycasts = target > 0.5f;
    }
}
