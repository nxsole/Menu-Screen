using UnityEngine;
using UnityEngine.UI;

public class BrightnessController : MonoBehaviour
{
    [SerializeField] private Image brightnessOverlay;
    [SerializeField] private Slider brightnessSlider;

    void Start()
    {
        // Load saved brightness or default to 0
        float savedBrightness = PlayerPrefs.GetFloat("Brightness", 0f);
        brightnessSlider.value = savedBrightness;
        SetBrightness(savedBrightness);

        // Add listener to update overlay automatically when slider moves
        brightnessSlider.onValueChanged.AddListener(SetBrightness);
    }

    public void SetBrightness(float value)
    {
        if (brightnessOverlay != null)
        {
            Color color = brightnessOverlay.color;
            color.a = value; // Controls the transparency/dimming
            brightnessOverlay.color = color;

            // Save player preference
            PlayerPrefs.SetFloat("Brightness", value);
        }
    }
}