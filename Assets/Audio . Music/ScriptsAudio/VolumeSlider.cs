using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    void Start()
    {
        var s = GetComponent<Slider>();
        if (AudioManager.Instance != null) s.value = AudioManager.Instance.MasterVolume;
        s.onValueChanged.AddListener(v =>
        {
            if (AudioManager.Instance != null) AudioManager.Instance.SetMasterVolume(v);
        });
    }
}