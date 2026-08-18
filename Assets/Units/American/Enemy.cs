using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    // Added 'Search' to the state machine
    private enum State { Idle, Aim, Fire, Kneel, Reposition, Investigate, Search }

    [Header("Detection")]
    [SerializeField] private string targetTag = "Ally";
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float fieldOfViewAngle = 120f;
    [SerializeField] private LayerMask lineOfSightBlockers;
    [SerializeField] private float detectionCheckInterval = 0.25f;
    [SerializeField] private float eyeHeightStanding = 1.6f;
    [SerializeField] private float eyeHeightCrouched = 0.9f;
    [SerializeField] private float targetAimHeight = 1f;
    [SerializeField] private float wallClearanceDistance = 0.5f;

    [Header("Aiming / Firing")]
    [SerializeField] private float aimDurationMin = 0.4f;
    [SerializeField] private float aimDurationMax = 0.9f;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private float aimRotationOffsetDegrees = 30f;
    [SerializeField] private Transform firePoint;
    [SerializeField] private GameObject muzzleFlashPrefab;
    [SerializeField] private float muzzleFlashLifetime = 1f;

    [Tooltip("0 = always miss, 1 = always hit. Provides a chance to survive the instant-kill shot.")]
    [SerializeField, Range(0f, 1f)] private float hitChance = 0.6f;

    [SerializeField] private AudioClip gunShotSound;
    [SerializeField, Range(0f, 1f)] private float gunShotVolume = 1.0f;
    [SerializeField] private AudioSource audioSource;

    [Header("Kneel / Reload")]
    [SerializeField] private float kneelReloadDuration = 1.5f;
    [SerializeField] private AudioClip reloadSound;
    [SerializeField, Range(0f, 1f)] private float reloadVolume = 0.4f;

    [Header("Repositioning (optional)")]
    [Range(0f, 1f)]
    [SerializeField] private float repositionChance = 0.4f;
    [SerializeField] private float repositionRadius = 6f;
    [SerializeField] private float repositionSpeed = 2.5f;

    [Header("Spacing (avoid clumping in groups)")]
    [SerializeField] private float minSeparationDistance = 3f;
    [SerializeField] private int repositionCandidateAttempts = 8;

    [Header("Alert / Squad Communication")]
    [SerializeField] private float alertShareRadius = 20f;
    [SerializeField] private float alertExpireTime = 8f;
    [SerializeField] private float investigateArrivalDistance = 1.5f;

    [Header("Search (Alert State)")]
    [SerializeField] private float searchDuration = 30f; // Returns to Relaxed/Idle after this time
    [SerializeField] private float searchRadius = 8f; // How far they wander while looking for you
    [SerializeField] private float searchWaitTime = 2f; // How long they pause to look around between steps

    [Header("Crouch Collider Adjustment")]
    [SerializeField] private float standingColliderHeight = 1.8f;
    [SerializeField] private float crouchedColliderHeight = 1.2f;
    [SerializeField] private float standingColliderCenterY = 0.9f;
    [SerializeField] private float crouchedColliderCenterY = 0.6f;

    [Header("Death / Ragdoll")]
    [SerializeField] private float ragdollDisappearDelay = 3f;
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] private float deathVolume = 1f;

    [Header("Root Motion")]
    [SerializeField] private bool useRootMotion = true;

    [Header("Animator State Names")]
    [SerializeField] private string idleStateName = "Idle";
    [SerializeField] private string kneelStateName = "Kneel";
    [SerializeField] private string walkStateName = "Walk";
    [SerializeField] private string crouchIdleStateName = "Crouch Idle";
    [SerializeField] private float stateWaitTimeout = 5f;

    private Animator animator;
    private NavMeshAgent agent;
    private CapsuleCollider bodyCapsule;
    private CharacterController characterController;
    private Transform currentTarget;
    private State state = State.Idle;
    private Coroutine behaviorRoutine;
    private bool isCrouchedStance;
    private Transform alertedTarget;
    private Vector3 alertedPosition;
    private float alertedTime = -Mathf.Infinity;
    private bool isDead;
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;

    private static readonly System.Collections.Generic.List<EnemyAI> ActiveEnemies = new System.Collections.Generic.List<EnemyAI>();

    private static readonly int HashIdle = Animator.StringToHash("Idle");
    private static readonly int HashAiming = Animator.StringToHash("Aiming");
    private static readonly int HashFiring = Animator.StringToHash("Firing");
    private static readonly int HashKneel = Animator.StringToHash("Kneel");
    private static readonly int HashWalking = Animator.StringToHash("Walking");
    private static readonly int HashCrouching = Animator.StringToHash("Crouching");

    private void Awake()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        bodyCapsule = GetComponent<CapsuleCollider>();
        characterController = GetComponent<CharacterController>();

        if (firePoint == null) firePoint = transform;
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        animator.applyRootMotion = useRootMotion;

        if (useRootMotion && agent != null)
        {
            agent.updatePosition = false;
            agent.updateRotation = false;
        }

        ragdollRigidbodies = System.Array.FindAll(
            GetComponentsInChildren<Rigidbody>(true),
            rb => rb.gameObject != gameObject);
        ragdollColliders = System.Array.FindAll(
            GetComponentsInChildren<Collider>(true),
            col => col.gameObject != gameObject);
        SetRagdollPhysicsEnabled(false);
    }

    private void OnEnable()
    {
        ActiveEnemies.Add(this);
        behaviorRoutine = StartCoroutine(BehaviorLoop());
    }

    private void OnDisable()
    {
        ActiveEnemies.Remove(this);
        if (behaviorRoutine != null) StopCoroutine(behaviorRoutine);
    }

    private void OnAnimatorMove()
    {
        if (!useRootMotion) return;

        if (agent != null)
        {
            agent.nextPosition = animator.rootPosition;
            transform.position = animator.rootPosition;
        }
        else
        {
            transform.position = animator.rootPosition;
        }
    }

    private IEnumerator BehaviorLoop()
    {
        while (true)
        {
            switch (state)
            {
                case State.Idle:
                    yield return StateIdle();
                    break;
                case State.Aim:
                    yield return StateAim();
                    break;
                case State.Fire:
                    yield return StateFire();
                    break;
                case State.Kneel:
                    yield return StateKneel();
                    break;
                case State.Reposition:
                    yield return StateReposition();
                    break;
                case State.Investigate:
                    yield return StateInvestigate();
                    break;
                case State.Search:
                    yield return StateSearch();
                    break;
            }
        }
    }

    // ---------------------------------------------------------------
    // IDLE (Relaxed)
    // ---------------------------------------------------------------
    private IEnumerator StateIdle()
    {
        animator.SetBool(HashIdle, true);
        animator.SetBool(HashAiming, false);
        animator.SetBool(HashWalking, false);
        animator.SetBool(HashKneel, false);

        while (true)
        {
            currentTarget = FindTarget();
            if (currentTarget != null)
            {
                BroadcastAlert(currentTarget);
                ClearAlert();
                state = State.Aim; // Triggers Alert state
                yield break;
            }

            if (alertedTarget != null && Time.time - alertedTime <= alertExpireTime)
            {
                state = State.Investigate; // Triggers Alert state
                yield break;
            }

            yield return new WaitForSeconds(detectionCheckInterval);
        }
    }

    // ---------------------------------------------------------------
    // AIM
    // ---------------------------------------------------------------
    private IEnumerator StateAim()
    {
        if (!TargetStillValid())
        {
            state = State.Search; // Lost target, start searching instead of relaxing
            yield break;
        }

        animator.SetBool(HashIdle, false);
        animator.SetBool(HashAiming, true);

        isCrouchedStance = DetermineCrouchStance();
        SetCrouching(isCrouchedStance);

        if (!HasLineOfSightAtCurrentStance())
        {
            animator.SetBool(HashAiming, false);
            state = State.Search; // Blocked sight, start searching
            yield break;
        }

        float aimDuration = Random.Range(aimDurationMin, aimDurationMax);
        float t = 0f;
        while (t < aimDuration)
        {
            FaceTarget();
            t += Time.deltaTime;
            yield return null;
        }

        state = State.Fire;
    }

    // ---------------------------------------------------------------
    // FIRE
    // ---------------------------------------------------------------
    private IEnumerator StateFire()
    {
        if (!TargetStillValid())
        {
            state = State.Search; // Target moved out of range, search
            yield break;
        }

        if (!HasLineOfSightAtCurrentStance())
        {
            state = State.Search; // Target hid, search
            yield break;
        }

        SnapFaceTarget();
        animator.SetTrigger(HashFiring);
        Shoot();

        yield return new WaitForSeconds(0.1f);

        state = State.Kneel;
    }

    private void Shoot()
    {
        if (muzzleFlashPrefab != null)
        {
            GameObject fx = Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
            Destroy(fx, muzzleFlashLifetime);
        }

        if (audioSource != null && gunShotSound != null)
        {
            audioSource.PlayOneShot(gunShotSound, gunShotVolume);
        }

        if (currentTarget != null)
        {
            if (Random.value <= hitChance)
            {
                var hittable = currentTarget.GetComponentInParent<EnemyAI>();
                if (hittable != null) hittable.Die();
            }

            var playerTarget = currentTarget.GetComponentInParent<FirstPersonController>();
            if (playerTarget != null)
            {
                playerTarget.TakeHit();
            }
        }
    }

    // ---------------------------------------------------------------
    // KNEEL (Reload)
    // ---------------------------------------------------------------
    private IEnumerator StateKneel()
    {
        animator.SetBool(HashAiming, false);
        SetCrouching(isCrouchedStance);
        animator.SetBool(HashKneel, true);

        yield return WaitUntilInState(kneelStateName);

        if (audioSource != null && reloadSound != null)
        {
            audioSource.PlayOneShot(reloadSound, reloadVolume);
        }

        yield return new WaitForSeconds(kneelReloadDuration);

        animator.SetBool(HashKneel, false);
        yield return WaitUntilInState(isCrouchedStance ? crouchIdleStateName : idleStateName);

        if (TargetStillValid() && Random.value < repositionChance)
        {
            state = State.Reposition;
        }
        else if (TargetStillValid())
        {
            state = State.Aim;
        }
        else
        {
            state = State.Search; // Reloaded but target is gone -> search
        }
    }

    // ---------------------------------------------------------------
    // REPOSITION
    // ---------------------------------------------------------------
    // ---------------------------------------------------------------
    // REPOSITION
    // ---------------------------------------------------------------
    private IEnumerator StateReposition()
    {
        Vector3 destination = PickSpacedDestination();

        // Always crouch when repositioning
        SetCrouching(true);
        animator.SetBool(HashWalking, true);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = repositionSpeed;

            if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, repositionRadius, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }

            float timeout = stateWaitTimeout;
            while ((agent.pathPending || agent.remainingDistance > agent.stoppingDistance) && timeout > 0f)
            {
                SteerTowardAgentPath();
                timeout -= Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            float timeout = stateWaitTimeout;
            while (Vector3.Distance(transform.position, destination) > 0.5f && timeout > 0f)
            {
                Vector3 dir = destination - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        animator.SetBool(HashWalking, false);
        // Wait until we enter the crouch idle state, since we know we are crouched
        yield return WaitUntilInState(crouchIdleStateName);

        state = TargetStillValid() ? State.Aim : State.Search; // Lost them during reposition -> search
    }

    // ---------------------------------------------------------------
    // INVESTIGATE
    // ---------------------------------------------------------------
    private IEnumerator StateInvestigate()
    {
        Vector3 destination = alertedPosition;

        animator.SetBool(HashWalking, true);
        SetCrouching(false);

        if (agent != null && agent.isOnNavMesh)
        {
            agent.speed = repositionSpeed;

            if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, repositionRadius * 3f, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }

            float timeout = stateWaitTimeout * 3f;
            while ((agent.pathPending || agent.remainingDistance > investigateArrivalDistance) && timeout > 0f)
            {
                SteerTowardAgentPath();
                if (TryAcquireDirectSight()) yield break;
                timeout -= Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            float timeout = stateWaitTimeout * 3f;
            while (Vector3.Distance(transform.position, destination) > investigateArrivalDistance && timeout > 0f)
            {
                Vector3 dir = destination - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(dir);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                }

                if (TryAcquireDirectSight()) yield break;
                timeout -= Time.deltaTime;
                yield return null;
            }
        }

        animator.SetBool(HashWalking, false);
        yield return WaitUntilInState(idleStateName);

        if (TryAcquireDirectSight()) yield break;

        ClearAlert();
        state = State.Search; // Reached the area but nothing is here -> start searching
    }

    // ---------------------------------------------------------------
    // SEARCH (Active Alert Patrol)
    // ---------------------------------------------------------------
    private IEnumerator StateSearch()
    {
        float endTime = Time.time + searchDuration;

        while (Time.time < endTime)
        {
            Vector3 destination = PickSearchDestination();

            animator.SetBool(HashWalking, true);
            SetCrouching(false); // Stand up to search faster/see better

            // Walk to the random point
            if (agent != null && agent.isOnNavMesh)
            {
                agent.speed = repositionSpeed;
                if (NavMesh.SamplePosition(destination, out NavMeshHit navHit, searchRadius, NavMesh.AllAreas))
                {
                    agent.SetDestination(navHit.position);
                }

                float timeout = stateWaitTimeout * 2f;
                while ((agent.pathPending || agent.remainingDistance > agent.stoppingDistance) && timeout > 0f)
                {
                    SteerTowardAgentPath();

                    // Spot target mid-walk? Attack.
                    if (TryAcquireDirectSight()) yield break;

                    // Buddy alerts them while searching? Run to the buddy's spot.
                    if (alertedTarget != null && Time.time - alertedTime <= alertExpireTime)
                    {
                        state = State.Investigate;
                        yield break;
                    }

                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                float timeout = stateWaitTimeout * 2f;
                while (Vector3.Distance(transform.position, destination) > 0.5f && timeout > 0f)
                {
                    Vector3 dir = destination - transform.position;
                    dir.y = 0f;
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(dir);
                        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
                    }

                    if (TryAcquireDirectSight()) yield break;

                    if (alertedTarget != null && Time.time - alertedTime <= alertExpireTime)
                    {
                        state = State.Investigate;
                        yield break;
                    }

                    timeout -= Time.deltaTime;
                    yield return null;
                }
            }

            // Arrived at the random point. Pause and look around.
            animator.SetBool(HashWalking, false);
            yield return WaitUntilInState(idleStateName);

            float pauseTimer = searchWaitTime;
            while (pauseTimer > 0f)
            {
                if (TryAcquireDirectSight()) yield break;

                if (alertedTarget != null && Time.time - alertedTime <= alertExpireTime)
                {
                    state = State.Investigate;
                    yield break;
                }

                pauseTimer -= Time.deltaTime;
                yield return null;
            }
        }

        // 30 seconds have passed without finding anything. Return to Relaxed/Idle.
        state = State.Idle;
    }

    private Vector3 PickSearchDestination()
    {
        // Try up to 5 times to find a valid spot around them to walk to
        for (int i = 0; i < 5; i++)
        {
            Vector3 candidate = transform.position + (Random.insideUnitSphere * searchRadius);
            candidate.y = transform.position.y;

            if (HasWallClearance(candidate)) return candidate;
        }
        return transform.position; // Fallback to not moving if stuck
    }

    private bool TryAcquireDirectSight()
    {
        Transform found = FindTarget();
        if (found == null) return false;

        currentTarget = found;
        animator.SetBool(HashWalking, false);
        ClearAlert();
        BroadcastAlert(currentTarget);
        state = State.Aim;
        return true;
    }

    private void BroadcastAlert(Transform target)
    {
        if (target == null) return;

        foreach (var other in ActiveEnemies)
        {
            if (other == null || other == this) continue;

            float dist = Vector3.Distance(transform.position, other.transform.position);
            if (dist <= alertShareRadius)
            {
                other.ReceiveAlert(target, target.position);
            }
        }
    }

    public void ReceiveAlert(Transform target, Vector3 position)
    {
        // Allow a Relaxed (Idle) OR Searching enemy to be redirected by an alert
        if (state != State.Idle && state != State.Search) return;

        alertedTarget = target;
        alertedPosition = position;
        alertedTime = Time.time;
    }

    private void ClearAlert()
    {
        alertedTarget = null;
        alertedTime = -Mathf.Infinity;
    }

    private void SetCrouching(bool crouched)
    {
        animator.SetBool(HashCrouching, crouched);

        float height = crouched ? crouchedColliderHeight : standingColliderHeight;
        float centerY = crouched ? crouchedColliderCenterY : standingColliderCenterY;

        if (bodyCapsule != null)
        {
            bodyCapsule.height = height;
            Vector3 c = bodyCapsule.center;
            c.y = centerY;
            bodyCapsule.center = c;
        }

        if (characterController != null)
        {
            characterController.height = height;
            Vector3 c = characterController.center;
            c.y = centerY;
            characterController.center = c;
        }
    }

    private void SetRagdollPhysicsEnabled(bool active)
    {
        foreach (var rb in ragdollRigidbodies)
        {
            if (rb == null) continue;
            rb.isKinematic = !active;
        }

        foreach (var col in ragdollColliders)
        {
            if (col == null) continue;
            col.enabled = active;
        }
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;

        if (behaviorRoutine != null) StopCoroutine(behaviorRoutine);
        ActiveEnemies.Remove(this);

        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound, deathVolume);
        }

        animator.enabled = false;
        if (agent != null) agent.enabled = false;
        if (bodyCapsule != null) bodyCapsule.enabled = false;
        if (characterController != null) characterController.enabled = false;

        SetRagdollPhysicsEnabled(true);

        Destroy(gameObject, ragdollDisappearDelay);
    }

    private IEnumerator WaitUntilInState(string stateName, int layer = 0)
    {
        float t = 0f;
        while (!animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName) && t < stateWaitTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
    }

    private Vector3 PickSpacedDestination()
    {
        Vector3 best = transform.position;
        float bestScore = -1f;

        for (int i = 0; i < repositionCandidateAttempts; i++)
        {
            Vector3 candidate = transform.position + (Random.insideUnitSphere * repositionRadius);
            candidate.y = transform.position.y;

            if (!HasWallClearance(candidate)) continue;

            float closestOtherDist = float.MaxValue;
            foreach (var other in ActiveEnemies)
            {
                if (other == null || other == this) continue;
                float d = Vector3.Distance(candidate, other.transform.position);
                if (d < closestOtherDist) closestOtherDist = d;
            }

            if (closestOtherDist > bestScore)
            {
                bestScore = closestOtherDist;
                best = candidate;
            }

            if (closestOtherDist >= minSeparationDistance)
            {
                return candidate;
            }
        }

        return best;
    }

    private bool HasWallClearance(Vector3 point)
    {
        if (lineOfSightBlockers.value == 0) return true;

        Vector3 checkPoint = point + Vector3.up * eyeHeightStanding;
        return !IsEmbeddedInObstruction(checkPoint, wallClearanceDistance);
    }

    private bool IsEmbeddedInObstruction(Vector3 point, float radius)
    {
        Collider[] hits = Physics.OverlapSphere(point, radius, lineOfSightBlockers);
        foreach (var hit in hits)
        {
            if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    private bool DetermineCrouchStance()
    {
        if (currentTarget == null) return true;
        if (lineOfSightBlockers.value == 0) return true;

        Vector3 crouchedEye = transform.position + Vector3.up * eyeHeightCrouched;
        Vector3 targetPoint = currentTarget.position + Vector3.up * targetAimHeight;

        if (IsEmbeddedInObstruction(crouchedEye, 0.05f)) return false;

        bool blockedWhileCrouched = Physics.Linecast(crouchedEye, targetPoint, lineOfSightBlockers);
        return !blockedWhileCrouched;
    }

    private bool HasLineOfSightAtCurrentStance()
    {
        if (currentTarget == null) return false;
        if (lineOfSightBlockers.value == 0) return true;

        float eyeHeight = isCrouchedStance ? eyeHeightCrouched : eyeHeightStanding;
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPoint = currentTarget.position + Vector3.up * targetAimHeight;

        if (IsEmbeddedInObstruction(eye, 0.05f)) return false;
        return !Physics.Linecast(eye, targetPoint, lineOfSightBlockers);
    }

    private Transform FindTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius);
        Transform best = null;
        float bestDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.CompareTag(targetTag)) continue;

            Vector3 toTarget = hit.transform.position - transform.position;

            if (fieldOfViewAngle < 360f)
            {
                float angle = Vector3.Angle(transform.forward, toTarget);
                if (angle > fieldOfViewAngle * 0.5f) continue;
            }

            if (lineOfSightBlockers.value != 0)
            {
                Vector3 eyeOrigin = transform.position + Vector3.up * eyeHeightStanding;

                if (IsEmbeddedInObstruction(eyeOrigin, 0.05f)) continue;

                Vector3 targetPoint = hit.transform.position + Vector3.up * targetAimHeight;
                if (Physics.Linecast(eyeOrigin, targetPoint, lineOfSightBlockers)) continue;
            }

            float dist = toTarget.sqrMagnitude;
            if (dist < bestDist)
            {
                bestDist = dist;
                best = hit.transform;
            }
        }

        return best;
    }

    private bool TargetStillValid()
    {
        if (currentTarget == null) return false;
        float dist = Vector3.Distance(transform.position, currentTarget.position);
        return dist <= detectionRadius;
    }

    private void FaceTarget()
    {
        if (currentTarget == null) return;
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, aimRotationOffsetDegrees, 0f);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    private void SnapFaceTarget()
    {
        if (currentTarget == null) return;
        Vector3 dir = currentTarget.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;

        transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, aimRotationOffsetDegrees, 0f);
    }

    private void SteerTowardAgentPath()
    {
        if (agent == null || agent.pathPending) return;
        if (agent.remainingDistance < 0.5f) return;

        Vector3 dir = agent.steeringTarget - transform.position;
        dir.y = 0f;

        if (dir.sqrMagnitude < 0.01f) return;

        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, searchRadius);
    }
}