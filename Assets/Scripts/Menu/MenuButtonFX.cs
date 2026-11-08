using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Coloque no objeto do Button. Dá escala e troca de cor ao passar o mouse
/// ou ao selecionar com teclado/controle.
/// </summary>
public class MenuButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(0.24f, 0.86f, 0.52f); // verde
    [SerializeField] private float hoverScale = 1.1f;
    [SerializeField] private float speed = 12f;

    private Vector3 baseScale;
    private bool highlighted;

    private void Awake()
    {
        baseScale = transform.localScale;
        if (label != null) label.color = normalColor;
    }

    private void OnDisable()
    {
        // Ao fechar um painel, o botão volta ao estado normal.
        highlighted = false;
        transform.localScale = baseScale;
        if (label != null) label.color = normalColor;
    }

    private void Update()
    {
        float k = Time.unscaledDeltaTime * speed;
        Vector3 targetScale = highlighted ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, targetScale, k);

        if (label != null)
        {
            Color targetColor = highlighted ? highlightColor : normalColor;
            label.color = Color.Lerp(label.color, targetColor, k);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => highlighted = true;
    public void OnPointerExit(PointerEventData eventData) => highlighted = false;
    public void OnSelect(BaseEventData eventData) => highlighted = true;
    public void OnDeselect(BaseEventData eventData) => highlighted = false;
}