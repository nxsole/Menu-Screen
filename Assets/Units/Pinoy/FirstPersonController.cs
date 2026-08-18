using System.Collections;
using UnityEngine;
using TMPro;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(AudioSource))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Crouching")]
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;
    private bool isCrouching = false;
    private float cameraStandingY;
    private float cameraCrouchingY;

    [Header("Look Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float upDownRange = 80f;

    [Header("References")]
    [SerializeField] private Transform playerCamera;
    [SerializeField] private Animator weaponAnimator;
    [SerializeField] private AudioSource audioSource;

    [Header("Weapon & Ammo")]
    [SerializeField] private int maxAmmo = 1;
    [SerializeField] private float weaponRange = 100f; // How far the bullet travels
    [SerializeField] private float reloadDuration = 1.5f;
    private int currentAmmo;
    private bool isReloading = false;

    [Header("Aiming (ADS)")]
    [SerializeField] private Transform weaponTransform;
    [SerializeField] private Vector3 hipFirePosition;
    [SerializeField] private Vector3 aimPosition;
    [SerializeField] private float aimSpeed = 10f;

    [Header("Effects")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private GameObject impactEffectPrefab; // Optional: Sparks/dust when you hit a wall
    [SerializeField] private float muzzleFlashLifetime = 0.1f;
    [SerializeField] private AudioClip fireSound;
    [SerializeField, Range(0f, 1f)] private float fireVolume = 1.0f;

    [Header("Health / Death / Respawn")]
    [SerializeField] private Transform initialSpawnPoint;      // Fallback if no RespawnPoint has been touched yet
    [SerializeField] private float respawnDelay = 3f;          // Seconds the black screen stays up
    [SerializeField] private float hitInvulnerabilityDuration = 1f; // Brief immunity right after respawning
    private Transform currentRespawnPoint;
    private bool isDead = false;
    private bool isInvulnerable = false;

    [Header("Death Screen UI")]
    [SerializeField] private CanvasGroup deathScreenCanvasGroup; // Full-screen black Image's CanvasGroup
    [SerializeField] private TMP_Text deathScreenText;           // Centered text on the death screen
    [SerializeField]
    private string[] deathMessages = new string[]
    {
        "Name 1",
        "Name 2",
        "Name 3",
        "Name 4",
    };
    private System.Collections.Generic.List<string> deathMessageBag = new System.Collections.Generic.List<string>();
    private string lastDeathMessage = null;

    private CharacterController controller;
    private Vector3 velocity;
    private float verticalRotation;

    // Animation Hashes
    private static readonly int HashFire = Animator.StringToHash("Fire");
    private static readonly int HashReload = Animator.StringToHash("Reload");

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        currentAmmo = maxAmmo;

        cameraStandingY = playerCamera.localPosition.y;
        cameraCrouchingY = cameraStandingY * (crouchHeight / standingHeight);

        // Default respawn point is wherever the player starts, unless one is assigned
        currentRespawnPoint = initialSpawnPoint != null ? initialSpawnPoint : transform;

        if (deathScreenCanvasGroup != null)
        {
            deathScreenCanvasGroup.alpha = 0f;
            deathScreenCanvasGroup.blocksRaycasts = false;
            deathScreenCanvasGroup.interactable = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        // Freeze all player control while the death screen is up / mid-respawn
        if (isDead) return;

        HandleMouseLook();
        HandleCrouch();
        HandleMovement();
        HandleAiming();
        HandleShootingAndReloading();
    }

    private void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        verticalRotation -= mouseY;
        verticalRotation = Mathf.Clamp(verticalRotation, -upDownRange, upDownRange);
        playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);

        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleCrouch()
    {
        isCrouching = Input.GetKey(KeyCode.C);

        float targetHeight = isCrouching ? crouchHeight : standingHeight;
        float targetCameraY = isCrouching ? cameraCrouchingY : cameraStandingY;

        controller.height = Mathf.Lerp(controller.height, targetHeight, Time.deltaTime * crouchTransitionSpeed);
        controller.center = new Vector3(0, controller.height / 2f, 0);

        Vector3 camPos = playerCamera.localPosition;
        camPos.y = Mathf.Lerp(camPos.y, targetCameraY, Time.deltaTime * crouchTransitionSpeed);
        playerCamera.localPosition = camPos;
    }

    private void HandleMovement()
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        float currentSpeed = isCrouching ? crouchSpeed : walkSpeed;

        Vector3 move = transform.right * moveX + transform.forward * moveZ;
        controller.Move(move * currentSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleAiming()
    {
        if (weaponTransform == null) return;

        bool isAiming = Input.GetButton("Fire2") && !isReloading;
        Vector3 targetPosition = isAiming ? aimPosition : hipFirePosition;

        weaponTransform.localPosition = Vector3.Lerp(weaponTransform.localPosition, targetPosition, Time.deltaTime * aimSpeed);
    }

    private void HandleShootingAndReloading()
    {
        if (weaponAnimator == null) return;

        // Fire (Left Click)
        if (Input.GetButtonDown("Fire1") && !isReloading)
        {
            if (currentAmmo > 0)
            {
                currentAmmo--;
                weaponAnimator.SetTrigger(HashFire);
                ShootFX();
                CastRay(); // Shoot the invisible laser
            }
            else
            {
                StartCoroutine(ReloadRoutine());
            }
        }

        // Manual Reload (R Key)
        if (Input.GetKeyDown(KeyCode.R) && currentAmmo < maxAmmo && !isReloading)
        {
            StartCoroutine(ReloadRoutine());
        }
    }

    private void CastRay()
    {
        // 1. Create a Ray starting at the camera's position and pointing forward
        Ray ray = new Ray(playerCamera.position, playerCamera.forward);

        // 2. Shoot the raycast. If it hits something within weaponRange, execute the code inside
        if (Physics.Raycast(ray, out RaycastHit hit, weaponRange))
        {
            // 3. Check if the object we hit (or its parent) has the EnemyAI script
            EnemyAI target = hit.collider.GetComponentInParent<EnemyAI>();

            if (target != null)
            {
                // We hit an enemy! Trigger their death logic.
                target.Die();
            }
            else if (impactEffectPrefab != null)
            {
                // We hit a wall/ground. Spawn a bullet hole or spark effect at the exact hit point.
                // Quaternion.LookRotation(hit.normal) makes the particle effect face away from the wall.
                GameObject impact = Instantiate(impactEffectPrefab, hit.point, Quaternion.LookRotation(hit.normal));
                Destroy(impact, 2f); // Clean up the effect after 2 seconds
            }
        }
    }

    private void ShootFX()
    {
        if (fireSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(fireSound, fireVolume);
        }

        if (muzzleFlashPrefab != null && firePoint != null)
        {
            GameObject flash = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation, firePoint);
            Destroy(flash, muzzleFlashLifetime);
        }
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;
        weaponAnimator.SetTrigger(HashReload);

        yield return new WaitForSeconds(reloadDuration);

        currentAmmo = maxAmmo;
        isReloading = false;
    }

    // ---------------------------------------------------------------
    //  Health / Death / Respawn
    // ---------------------------------------------------------------

    /// <summary>
    /// Call this from an NPC's attack code when it successfully hits the player,
    /// e.g. player.GetComponent&lt;FirstPersonController&gt;().TakeHit();
    /// </summary>
    public void TakeHit()
    {
        if (isDead || isInvulnerable) return;

        StartCoroutine(DeathSequence());
    }

    /// <summary>
    /// Called by a RespawnPoint trigger when the player walks over it.
    /// </summary>
    public void RegisterRespawnPoint(Transform point)
    {
        currentRespawnPoint = point;
    }

    private IEnumerator DeathSequence()
    {
        isDead = true;

        // Cancel any in-progress reload so it doesn't silently finish while dead
        isReloading = false;

        ShowDeathScreen();

        yield return new WaitForSeconds(respawnDelay);

        RespawnPlayer();

        HideDeathScreen();

        isDead = false;

        // Brief grace period so the player can't be hit again the instant they respawn
        if (hitInvulnerabilityDuration > 0f)
        {
            StartCoroutine(InvulnerabilityWindow());
        }
    }

    private IEnumerator InvulnerabilityWindow()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(hitInvulnerabilityDuration);
        isInvulnerable = false;
    }

    private void ShowDeathScreen()
    {
        if (deathScreenCanvasGroup == null) return;

        deathScreenCanvasGroup.alpha = 1f;
        deathScreenCanvasGroup.blocksRaycasts = true;
        deathScreenCanvasGroup.interactable = true;

        if (deathScreenText != null && deathMessages.Length > 0)
        {
            deathScreenText.text = GetNextDeathMessage();
        }
    }

    /// <summary>
    /// Hands out death messages from a shuffled "bag" so every line is used exactly
    /// once before any line repeats. When the bag empties, it's reshuffled — and if
    /// there's more than one message, the reshuffle is guarded so the message that
    /// just played can't immediately show up again as the first pick of the new bag.
    /// </summary>
    private string GetNextDeathMessage()
    {
        if (deathMessageBag.Count == 0)
        {
            RefillDeathMessageBag();
        }

        int lastIndex = deathMessageBag.Count - 1;
        string message = deathMessageBag[lastIndex];
        deathMessageBag.RemoveAt(lastIndex);

        lastDeathMessage = message;
        return message;
    }

    private void RefillDeathMessageBag()
    {
        deathMessageBag.Clear();
        deathMessageBag.AddRange(deathMessages);

        // Fisher-Yates shuffle
        for (int i = deathMessageBag.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (deathMessageBag[i], deathMessageBag[j]) = (deathMessageBag[j], deathMessageBag[i]);
        }

        // Avoid the new bag's first pick matching the message that just played
        if (deathMessages.Length > 1 && deathMessageBag[deathMessageBag.Count - 1] == lastDeathMessage)
        {
            (deathMessageBag[deathMessageBag.Count - 1], deathMessageBag[0]) =
                (deathMessageBag[0], deathMessageBag[deathMessageBag.Count - 1]);
        }
    }

    private void HideDeathScreen()
    {
        if (deathScreenCanvasGroup == null) return;

        deathScreenCanvasGroup.alpha = 0f;
        deathScreenCanvasGroup.blocksRaycasts = false;
        deathScreenCanvasGroup.interactable = false;
    }

    private void RespawnPlayer()
    {
        // CharacterController must be disabled before teleporting via transform,
        // otherwise Unity's collision resolution can fight the move.
        controller.enabled = false;

        transform.position = currentRespawnPoint.position;
        transform.rotation = currentRespawnPoint.rotation;

        velocity = Vector3.zero;
        currentAmmo = maxAmmo;

        controller.enabled = true;
    }
}