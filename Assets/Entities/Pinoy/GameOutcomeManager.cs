using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

// Central win/lose handler. Put this on a GameObject in the scene and wire up the references.
// LOSE: fires automatically when the last ally-tagged unit dies. Shows a typewriter text screen
//       (same style as the intro) then loads the main menu.
// WIN:  call TriggerWin() (a WinZone trigger does this when the player enters). Detaches the main
//       camera, pans it forward, types text on screen, fades a black background in, loads the menu.
public class GameOutcomeManager : MonoBehaviour
{
    [Header("Teams")]
    [Tooltip("Tag of the player's allied troops. When none with this tag remain alive, it's a loss.")]
    [SerializeField] private string allyTag = "Ally";

    [Header("Shared UI")]
    [Tooltip("Full-screen black Image (Screen Space - Overlay canvas, high sort order). Starts transparent/off.")]
    [SerializeField] private Image blackOverlay;
    [Tooltip("Outcome text element (TextMeshPro UGUI).")]
    [SerializeField] private TMP_Text outcomeText;

    [Header("Lose Screen")]
    [TextArea(2, 5)]
    [SerializeField] private string loseMessage = "The line has fallen.";
    [SerializeField] private float loseBlackFadeIn = 1f;   // black fades up first
    [SerializeField] private float loseTypeSpeed = 0.05f;
    [SerializeField] private float loseHoldAfterTyped = 3f;
    [SerializeField] private AudioClip loseStinger;

    [Header("Win Sequence")]
    [TextArea(2, 5)]
    [SerializeField] private string winMessage = "The field is ours.";
    [Tooltip("How far (metres) the camera drifts forward during the win pan.")]
    [SerializeField] private float winPanDistance = 6f;
    [Tooltip("How long the camera takes to pan that distance.")]
    [SerializeField] private float winPanDuration = 6f;
    [Tooltip("Delay after the pan starts before the win text begins typing.")]
    [SerializeField] private float winTextDelay = 1.5f;
    [SerializeField] private float winTypeSpeed = 0.06f;
    [Tooltip("How long the fully-typed win text holds before the black fades in.")]
    [SerializeField] private float winHoldAfterTyped = 2f;
    [SerializeField] private float winBlackFadeIn = 2f;
    [SerializeField] private AudioClip winStinger;

    [Header("Win - Second Line (on black)")]
    [Tooltip("A second line shown on the fully-black screen after the first line has faded under it. Leave empty to skip and go straight to the menu.")]
    [TextArea(2, 5)]
    [SerializeField] private string winSecondMessage = "1902. The war is won.";
    [Tooltip("Seconds to wait on black (after it's fully faded in) before the second line appears.")]
    [SerializeField] private float winSecondTextDelay = 0.6f;
    [Tooltip("Seconds the second line holds on screen before the game loads the menu.")]
    [SerializeField] private float winSecondTextHold = 3f;
    [Tooltip("Separate TMP text for the second line. If left empty, the same outcomeText element is reused.")]
    [SerializeField] private TMP_Text winSecondText;

    [Header("Audio")]
    [Tooltip("Optional source for the win/lose stinger. If empty, one is added when needed.")]
    [SerializeField] private AudioSource stingerSource;

    [Header("After")]
    [Tooltip("Name of the main menu scene to load. Must be added to Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [Tooltip("Real seconds to wait on the final screen before loading the menu.")]
    [SerializeField] private float delayBeforeMenu = 3f;

    [Header("Debug")]
    [Tooltip("Logs subscription, every unit death, the live ally count, and why lose did/didn't fire. Turn off once confirmed.")]
    [SerializeField] private bool debugLogging = true;

    private bool outcomeDecided; // guards against win + lose both firing, or firing twice

    private void Awake()
    {
        if (blackOverlay != null)
        {
            SetImageAlpha(blackOverlay, 0f);
            blackOverlay.gameObject.SetActive(true); // active but transparent, ready to fade
        }
        if (outcomeText != null)
        {
            outcomeText.text = "";
            SetTextAlpha(outcomeText, 0f);
        }
        if (stingerSource == null) stingerSource = GetComponent<AudioSource>();
    }

    private void OnEnable()
    {
        EnemyAI.OnAnyUnitDied += HandleUnitDied;
        if (debugLogging) Debug.Log($"[Outcome] Subscribed to death events. Watching for allyTag='{allyTag}'. Living allies at start: {EnemyAI.CountLivingWithTag(allyTag)}.", this);
    }
    private void OnDisable() => EnemyAI.OnAnyUnitDied -= HandleUnitDied;

    private void HandleUnitDied(string diedTag)
    {
        if (outcomeDecided) return;

        int living = EnemyAI.CountLivingWithTag(allyTag);
        if (debugLogging) Debug.Log($"[Outcome] Unit died with tag '{diedTag}'. My allyTag='{allyTag}'. Living allies now: {living}.", this);

        if (diedTag != allyTag)
        {
            if (debugLogging) Debug.Log($"[Outcome] Died tag '{diedTag}' != allyTag '{allyTag}', ignoring for lose check.", this);
            return;
        }

        // Last ally just died? (Count is already post-death - the event fires after removal.)
        if (living <= 0)
        {
            if (debugLogging) Debug.Log("[Outcome] No allies left -> triggering lose.", this);
            TriggerLose();
        }
    }

    // ---------------- LOSE ----------------

    public void TriggerLose()
    {
        if (outcomeDecided) return;
        outcomeDecided = true;
        StartCoroutine(LoseRoutine());
    }

    private IEnumerator LoseRoutine()
    {
        PlayStinger(loseStinger);

        // Black fades up first, then the text types over it.
        if (blackOverlay != null) yield return FadeImage(blackOverlay, 0f, 1f, loseBlackFadeIn);

        if (outcomeText != null)
        {
            SetTextAlpha(outcomeText, 1f);
            yield return Typewriter(outcomeText, loseMessage, loseTypeSpeed);
        }

        if (loseHoldAfterTyped > 0f) yield return WaitRealtime(loseHoldAfterTyped);
        if (delayBeforeMenu > 0f) yield return WaitRealtime(delayBeforeMenu);

        LoadMenu();
    }

    // ---------------- WIN ----------------

    // Called by WinZone when the player enters the win area.
    public void TriggerWin(Transform playerToFreeze = null)
    {
        if (outcomeDecided) return;
        outcomeDecided = true;
        StartCoroutine(WinRoutine(playerToFreeze));
    }

    private IEnumerator WinRoutine(Transform playerToFreeze)
    {
        PlayStinger(winStinger);

        // Take over the camera: detach it from the player rig so our pan isn't fought by the
        // player's look/move control, and disable the player controller so input stops.
        Camera cam = Camera.main;
        if (playerToFreeze != null)
        {
            var fpc = playerToFreeze.GetComponentInChildren<FirstPersonController>();
            if (fpc != null) fpc.enabled = false;
        }

        Transform camT = cam != null ? cam.transform : null;

        if (camT != null) camT.SetParent(null); // detach so nothing else drives it

        // Constant forward drift speed (units/sec) so the camera moves at the same rate whether
        // it's during the initial pan, the hold, or the fade - the motion never visibly stops.
        Vector3 panDir = camT != null ? camT.forward : Vector3.zero;
        float panSpeed = winPanDuration > 0f ? winPanDistance / winPanDuration : 0f;

        // --- Phase 1: pan while typing the text, for winPanDuration ---
        bool textStarted = false;
        float t = 0f;
        while (t < winPanDuration)
        {
            if (camT != null) camT.position += panDir * (panSpeed * Time.unscaledDeltaTime);

            if (!textStarted && t >= winTextDelay)
            {
                textStarted = true;
                if (outcomeText != null)
                {
                    SetTextAlpha(outcomeText, 1f);
                    StartCoroutine(Typewriter(outcomeText, winMessage, winTypeSpeed));
                }
            }

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        // --- Phase 2: keep drifting while the text holds ---
        float holdTimer = 0f;
        while (holdTimer < winHoldAfterTyped)
        {
            if (camT != null) camT.position += panDir * (panSpeed * Time.unscaledDeltaTime);
            holdTimer += Time.unscaledDeltaTime;
            yield return null;
        }

        // --- Phase 3: keep drifting WHILE the black fades in (fade runs concurrently) ---
        if (blackOverlay != null && winBlackFadeIn > 0f)
        {
            float fadeTimer = 0f;
            while (fadeTimer < winBlackFadeIn)
            {
                if (camT != null) camT.position += panDir * (panSpeed * Time.unscaledDeltaTime);
                SetImageAlpha(blackOverlay, Mathf.Lerp(0f, 1f, fadeTimer / winBlackFadeIn));
                fadeTimer += Time.unscaledDeltaTime;
                yield return null;
            }
            SetImageAlpha(blackOverlay, 1f);
        }
        else if (blackOverlay != null)
        {
            SetImageAlpha(blackOverlay, 1f);
        }

        // --- Phase 4: second line, shown instantly on the now-black screen ---
        // Hide the first line (it's behind the black anyway) so reusing the same text element
        // doesn't briefly show the old message.
        if (outcomeText != null && winSecondText == null) outcomeText.text = "";
        else if (outcomeText != null) SetTextAlpha(outcomeText, 0f);

        if (!string.IsNullOrEmpty(winSecondMessage))
        {
            if (winSecondTextDelay > 0f) yield return WaitRealtime(winSecondTextDelay);

            TMP_Text secondTarget = winSecondText != null ? winSecondText : outcomeText;
            if (secondTarget != null)
            {
                // Make sure the second line draws ON TOP of the black overlay. The first line sits
                // behind black (so black can fade over it); the second must sit in front. Moving it
                // to the last sibling of its canvas puts it above the overlay in draw order.
                secondTarget.transform.SetAsLastSibling();
                secondTarget.text = winSecondMessage; // instant appear, no typewriter
                SetTextAlpha(secondTarget, 1f);
            }

            if (winSecondTextHold > 0f) yield return WaitRealtime(winSecondTextHold);
        }

        if (delayBeforeMenu > 0f) yield return WaitRealtime(delayBeforeMenu);

        LoadMenu();
    }

    // ---------------- shared ----------------

    private void LoadMenu()
    {
        Time.timeScale = 1f; // ensure normal time in the menu even if something paused us

        // Release the cursor from gameplay lock so the menu is clickable. The player controller
        // locked/hid it on start and nothing else frees it, so it would otherwise stay captured
        // into the menu scene.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogWarning("[GameOutcomeManager] No main menu scene name set - staying on this screen.", this);
        }
    }

    private void PlayStinger(AudioClip clip)
    {
        if (clip == null) return;
        if (stingerSource == null) stingerSource = gameObject.AddComponent<AudioSource>();
        stingerSource.PlayOneShot(clip);
    }

    private IEnumerator Typewriter(TMP_Text text, string message, float speed)
    {
        if (text == null || string.IsNullOrEmpty(message)) yield break;
        for (int i = 0; i <= message.Length; i++)
        {
            text.text = message.Substring(0, i);
            if (speed > 0f) yield return WaitRealtime(speed);
        }
    }

    private IEnumerator FadeImage(Image img, float from, float to, float duration)
    {
        if (img == null) yield break;
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
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }

    private static void SetImageAlpha(Image img, float a)
    {
        Color c = img.color; c.a = a; img.color = c;
    }

    private static void SetTextAlpha(TMP_Text text, float a)
    {
        Color c = text.color; c.a = a; text.color = c;
    }
}