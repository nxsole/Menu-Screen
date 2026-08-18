using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioMixer mainMixer;
    [SerializeField] private Slider masterSlider;

    private const string MASTER_KEY = "MasterVolume";

    private void Awake()
    {
        // Load saved volume preferences when the panel wakes up
        ApplySavedVolume();
    }

    private void Start()
    {
        // Link the UI slider to the volume function if it exists in the scene
        if (masterSlider != null)
        {
            float savedVolume = PlayerPrefs.GetFloat(MASTER_KEY, 1f);
            masterSlider.value = savedVolume;
            masterSlider.onValueChanged.AddListener(SetMasterVolume);
        }
    }

    public void ApplySavedVolume()
    {
        float savedVolume = PlayerPrefs.GetFloat(MASTER_KEY, 1f);
        SetMasterVolume(savedVolume);
    }

    public void SetMasterVolume(float sliderValue)
    {
        // Convert 0.0001 - 1.0 linear slider value to decibels (-80dB to 0dB)
        float volumeInDb = Mathf.Log10(Mathf.Max(0.0001f, sliderValue)) * 20f;

        if (mainMixer != null)
        {
            mainMixer.SetFloat("MasterVolume", volumeInDb);
        }

        // Save slider preference across game sessions
        PlayerPrefs.SetFloat(MASTER_KEY, sliderValue);
    }

    // --- Main Menu Functions ---

    public void ExitGame()
    {
        Debug.Log("Game Quit!");

#if UNITY_EDITOR
        // Stops Play Mode when testing inside the Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#else
            // Closes the built application (.exe / executable)
            Application.Quit();
#endif
    }
}