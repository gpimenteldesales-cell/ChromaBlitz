using UnityEngine;

/// <summary>
/// Liga a intensidade do efeito VHS (visual) ao chiado de fita da música.
/// Opção A: arraste o Material do shader e o nome da propriedade float (ex.: _VhsIntensity).
/// Opção B: deixe sem material e altere "manual" (por Animator, script, Volume, etc.).
/// </summary>
public class VhsAudioLink : MonoBehaviour
{
    public Material material;
    public string property = "_VhsIntensity";
    public float min = 0f;
    public float max = 1f;

    [Range(0f, 1f)] public float manual = 1f;

    void Update()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        float v = manual;
        if (material != null && material.HasProperty(property))
            v = Mathf.InverseLerp(min, max, material.GetFloat(property));

        am.SetVhsIntensity(v);
    }
}
