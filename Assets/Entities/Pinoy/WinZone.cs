using UnityEngine;

// Put this on a GameObject with a trigger Collider (e.g. a Box Collider with Is Trigger ticked)
// placed at the location the player must reach to win. When the player enters, it tells the
// GameOutcomeManager to run the win sequence.
[RequireComponent(typeof(Collider))]
public class WinZone : MonoBehaviour
{
    [Tooltip("The scene's GameOutcomeManager. If left empty, it's found automatically.")]
    [SerializeField] private GameOutcomeManager outcomeManager;
    [Tooltip("Tag of the player object that triggers the win.")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Trigger only once.")]
    [SerializeField] private bool oneShot = true;

    private bool fired;

    private void Reset()
    {
        // Convenience: auto-tick Is Trigger when the component is added in the editor.
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        if (outcomeManager == null) outcomeManager = FindObjectOfType<GameOutcomeManager>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (fired && oneShot) return;
        if (!other.CompareTag(playerTag)) return;
        if (outcomeManager == null) return;

        fired = true;
        // Pass the player's transform so the manager can freeze its controller during the pan.
        outcomeManager.TriggerWin(other.transform);
    }
}
