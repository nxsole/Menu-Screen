using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonAudio : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Audio Clips")]
    [SerializeField] private AudioClip hoverSound;
    [SerializeField] private AudioClip clickSound;

    [Header("Volume Controls")]
    [Tooltip("Subtle background volume for hover. Default is 0.25 (25% volume).")]
    [Range(0f, 1f)]
    [SerializeField] private float hoverVolume = 0.25f;

    [Tooltip("Volume for button click.")]
    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 1.0f;

    [Header("Audio Source Reference")]
    [Tooltip("Drag your SFX AudioSource here. If left empty, it will auto-find one on 'AudioManager'.")]
    [SerializeField] private AudioSource sfxSource;

    private void Awake()
    {
        // Auto-assign AudioSource if empty
        if (sfxSource == null)
        {
            sfxSource = GameObject.Find("AudioManager")?.GetComponent<AudioSource>();
        }

        // Pre-warm the AudioSource buffer on Awake to eliminate initial hover lag
        if (sfxSource != null && hoverSound != null)
        {
            sfxSource.PlayOneShot(hoverSound, 0f);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(hoverSound, hoverVolume);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (clickSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clickSound, clickVolume);
        }
    }
}