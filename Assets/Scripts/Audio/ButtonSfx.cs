using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Coloque em cada botão do menu. Toca hover (mouse e navegação por teclado/controle) e clique
/// (ou "voltar"). Opcionalmente já muda o estado da música ao clicar.
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public enum ClickKind { Click, Back, None }

    public ClickKind clickSound = ClickKind.Click;

    [Header("Música (opcional)")]
    public bool changeMusicState = false;
    public AudioManager.MusicState musicState = AudioManager.MusicState.Menu;
    public float fade = 0.8f;

    Button _button;
    float _lastHoverTime = -1f;

    void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(OnClick);
    }

    void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(OnClick);
    }

    public void OnPointerEnter(PointerEventData eventData) => Hover();
    public void OnSelect(BaseEventData eventData) => Hover();

    void Hover()
    {
        if (AudioManager.Instance == null || !_button.interactable) return;
        // evita hover duplo (mouse + seleção no mesmo instante)
        if (Time.unscaledTime - _lastHoverTime < 0.06f) return;
        _lastHoverTime = Time.unscaledTime;
        AudioManager.Instance.PlayHover();
    }

    void OnClick()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        switch (clickSound)
        {
            case ClickKind.Click: am.PlayClick(); break;
            case ClickKind.Back: am.PlayBack(); break;
        }
        if (changeMusicState) am.SetState(musicState, fade);
    }
}
