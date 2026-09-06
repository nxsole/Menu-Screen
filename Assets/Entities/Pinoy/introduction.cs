using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Plays a scene intro: black screen up, music starts, text types out letter-by-letter, holds,
// then the text fades and the black background disappears right after. EVERYTHING else in the
// scene is frozen for the duration via Time.timeScale = 0 - the player, the AI troops, and the
// path-follower's start clip all use scaled time, so they pause here and resume together when the
// intro ends. This controller itself runs on UNSCALED time so it keeps animating during the freeze.
//
// Setup: put this on a GameObject in the scene. Assign a full-screen black UI Image (on a Canvas
// set to Screen Space - Overlay, sorting order high enough to sit on top of everything) and a
// TMP_Text for the intro line. Assign the music AudioSource and type your text below.
public class SceneIntroController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Full-screen black Image that covers the screen. Should start fully opaque.")]
    [SerializeField] private Image blackOverlay;
    [Tooltip("The intro text element (TextMeshPro UGUI).")]
    [SerializeField] private TMP_Text introText;
    [Tooltip("AudioSource holding the intro music. Played once at the very start.")]
    [SerializeField] private AudioSource musicSource;

    [Header("Text")]
    [TextArea(2, 5)]
    [SerializeField] private string introMessage = "1899. The revolution begins.";

    [Header("Timing (all in real seconds, unaffected by the freeze)")]
    [Tooltip("Seconds between each typed character.")]
    [SerializeField] private float typeSpeed = 0.05f;
    [Tooltip("How long the fully-typed text is held on screen before it fades out.")]
    [SerializeField] private float holdAfterTyped = 2f;
    [Tooltip("Seconds for the text to fade out.")]
    [SerializeField] private float textFadeDuration = 1f;
    [Tooltip("Seconds for the black background to fade away after the text is gone. Set to 0 for an instant cut.")]
    [SerializeField] private float blackFadeDuration = 0.5f;

    [Header("Behaviour")]
    [Tooltip("Freeze the whole scene (Time.timeScale = 0) during the intro and restore it at the end.")]
    [SerializeField] private bool freezeSceneDuringIntro = true;
    [Tooltip("Also lock and hide the cursor when the intro finishes (typical for FPS).")]
    [SerializeField] private bool lockCursorOnFinish = true;
    [SerializeField] private bool playOnStart = true;

    private void Start()
    {
        if (playOnStart) StartCoroutine(RunIntro());
    }

    public IEnumerator RunIntro()
    {
        // --- Freeze everything else. This controller uses unscaled time so it keeps running. ---
        float restoreScale = Time.timeScale;
        if (freezeSceneDuringIntro) Time.timeScale = 0f;

        // --- Initial state: black up, text empty, music from the very start. ---
        if (blackOverlay != null) SetImageAlpha(blackOverlay, 1f);
        if (introText != null)
        {
            introText.text = "";
            SetTextAlpha(introText, 1f);
        }

        if (musicSource != null && musicSource.clip != null)
        {
            musicSource.ignoreListenerPause = true; // so it isn't silenced by AudioListener.pause if you use it
            musicSource.Play();
        }

        // --- Typewriter: reveal one character at a time. ---
        if (introText != null && !string.IsNullOrEmpty(introMessage))
        {
            for (int i = 0; i <= introMessage.Length; i++)
            {
                introText.text = introMessage.Substring(0, i);
                if (typeSpeed > 0f) yield return WaitRealtime(typeSpeed);
            }
        }

        // --- Hold the finished line. ---
        if (holdAfterTyped > 0f) yield return WaitRealtime(holdAfterTyped);

        // --- Fade the text out. ---
        if (introText != null) yield return FadeText(introText, 1f, 0f, textFadeDuration);

        // --- Black disappears immediately after the text is gone. ---
        if (blackOverlay != null)
        {
            if (blackFadeDuration > 0f) yield return FadeImage(blackOverlay, 1f, 0f, blackFadeDuration);
            else SetImageAlpha(blackOverlay, 0f);
            blackOverlay.gameObject.SetActive(false);
        }
        if (introText != null) introText.gameObject.SetActive(false);

        // --- Unfreeze: player, AI, and the path start clip all resume from here. ---
        if (freezeSceneDuringIntro) Time.timeScale = restoreScale == 0f ? 1f : restoreScale;

        if (lockCursorOnFinish)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // --- helpers (all unscaled so they animate while timeScale is 0) ---

    private IEnumerator FadeText(TMP_Text text, float from, float to, float duration)
    {
        if (duration <= 0f) { SetTextAlpha(text, to); yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetTextAlpha(text, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetTextAlpha(text, to);
    }

    private IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        if (duration <= 0f) { SetImageAlpha(img, to); yield break; }
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            SetImageAlpha(img, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        SetImageAlpha(img, to);
    }

    private IEnumerator WaitRealtime(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static void SetImageAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    private static void SetTextAlpha(TMP_Text text, float a)
    {
        Color c = text.color;
        c.a = a;
        text.color = c;
    }
}