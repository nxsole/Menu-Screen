using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SplashScreenFade : MonoBehaviour
{
    // Static bool persists in memory across scene reloads/UI toggles during a single gaming session
    private static bool hasPlayedSplash = false;

    [Header("Canvas Groups")]
    [Tooltip("CanvasGroup on SplashImage. Controls fade for splash image, text, and slider together.")]
    [SerializeField] private CanvasGroup splashCanvasGroup;

    [Tooltip("CanvasGroup on FadeOverlay (black full-screen panel).")]
    [SerializeField] private CanvasGroup overlayCanvasGroup;

    [Tooltip("Drag SplashImage (or SplashText) here for scale animation.")]
    [SerializeField] private RectTransform splashRectTransform;

    [Header("UI References")]
    [SerializeField] private Slider loadingSlider;

    [Header("Audio Settings")]
    [Tooltip("Drag your BGM Audio Source here.")]
    [SerializeField] private AudioSource bgmAudioSource;

    [Header("Timings (in seconds)")]
    [SerializeField] private float initialBlackDelay = 2f;
    [SerializeField] private float fadeInTime = 3f;
    [SerializeField] private float splashDisplayTime = 3.5f; // Hold time after fading in
    [SerializeField] private float fadeOutTime = 3f;

    [Header("Slider Timings & Settings")]
    [Tooltip("How fast the loading bar fills to 100%. Set lower than FadeIn + DisplayTime so it finishes early.")]
    [SerializeField] private float sliderFillTime = 4f;

    [Tooltip("Number of discrete jumps/steps for the loading bar (e.g., 5, 10, 20).")]
    [SerializeField] private int numberOfSteps = 10;

    [Header("Zoom Out Settings")]
    [SerializeField] private Vector3 startScale = new Vector3(1.06f, 1.06f, 1f);
    [SerializeField] private Vector3 endScale = new Vector3(1.0f, 1.0f, 1f);

    private void Start()
    {
        // 1. If splash has already played, instantly hide overlay and abort sequence
        if (hasPlayedSplash)
        {
            SkipSplashScreen();
            return;
        }

        // 2. First-time setup
        hasPlayedSplash = true; // Mark as played for the rest of the session

        if (splashCanvasGroup != null) splashCanvasGroup.alpha = 0f;
        if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = 1f;

        if (splashRectTransform != null)
            splashRectTransform.localScale = startScale;

        if (loadingSlider != null)
            loadingSlider.value = 0f;

        StartCoroutine(PlaySplashScreenSequence());
    }

    private void SkipSplashScreen()
    {
        if (splashCanvasGroup != null) splashCanvasGroup.alpha = 0f;
        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = 0f;
            overlayCanvasGroup.blocksRaycasts = false;
        }

        // Ensure music plays if skipped or revisited
        PlayBackgroundMusic();

        gameObject.SetActive(false);
    }

    private IEnumerator PlaySplashScreenSequence()
    {
        float totalSequenceDuration = fadeInTime + splashDisplayTime + fadeOutTime;

        // 1. Initial Black Screen Delay
        yield return new WaitForSeconds(initialBlackDelay);

        // Start scale animation across full sequence, but slider fills on its own faster duration
        StartCoroutine(AnimateSplashScale(totalSequenceDuration));
        StartCoroutine(AnimateLoadingSlider(sliderFillTime));

        // 2. Fade IN Splash Screen + Slider
        float timer = 0f;
        while (timer < fadeInTime)
        {
            timer += Time.deltaTime;
            if (splashCanvasGroup != null)
                splashCanvasGroup.alpha = Mathf.Lerp(0f, 1f, timer / fadeInTime);
            yield return null;
        }
        if (splashCanvasGroup != null) splashCanvasGroup.alpha = 1f;

        // 3. Hold Splash Screen Visible (Slider finishes filling during this phase)
        yield return new WaitForSeconds(splashDisplayTime);

        // 4. Fade OUT Splash Screen + Slider to Black
        timer = 0f;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            if (splashCanvasGroup != null)
                splashCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutTime);
            yield return null;
        }
        if (splashCanvasGroup != null) splashCanvasGroup.alpha = 0f;

        // 5. Fade OUT Black Screen to Main Menu
        // Trigger BGM early as the black overlay starts fading away
        PlayBackgroundMusic();

        timer = 0f;
        while (timer < fadeOutTime)
        {
            timer += Time.deltaTime;
            if (overlayCanvasGroup != null)
                overlayCanvasGroup.alpha = Mathf.Lerp(1f, 0f, timer / fadeOutTime);
            yield return null;
        }
        if (overlayCanvasGroup != null) overlayCanvasGroup.alpha = 0f;

        // Cleanup
        if (overlayCanvasGroup != null)
            overlayCanvasGroup.blocksRaycasts = false;

        gameObject.SetActive(false);
    }

    private void PlayBackgroundMusic()
    {
        if (bgmAudioSource != null && !bgmAudioSource.isPlaying)
        {
            bgmAudioSource.Play();
        }
    }

    private IEnumerator AnimateSplashScale(float duration)
    {
        float timer = 0f;
        while (timer < duration && splashRectTransform != null)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);
            splashRectTransform.localScale = Vector3.Lerp(startScale, endScale, smoothProgress);
            yield return null;
        }
        if (splashRectTransform != null)
            splashRectTransform.localScale = endScale;
    }

    private IEnumerator AnimateLoadingSlider(float duration)
    {
        float timer = 0f;
        while (timer < duration && loadingSlider != null)
        {
            timer += Time.deltaTime;
            float smoothProgress = Mathf.Clamp01(timer / duration);

            // Convert continuous progress into rigid, stepped increments
            float rigidProgress = Mathf.Floor(smoothProgress * numberOfSteps) / numberOfSteps;

            loadingSlider.value = rigidProgress;
            yield return null;
        }

        if (loadingSlider != null)
            loadingSlider.value = 1f;
    }
}