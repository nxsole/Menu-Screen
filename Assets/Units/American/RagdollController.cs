using UnityEngine;

public class RagdollController : MonoBehaviour
{
    private Animator animator;
    private Rigidbody[] ragdollRigidbodies;
    private Collider[] ragdollColliders;

    void Awake()
    {
        animator = GetComponent<Animator>();
        ragdollRigidbodies = GetComponentsInChildren<Rigidbody>();
        ragdollColliders = GetComponentsInChildren<Collider>();

        // Start in normal animated state
        SetRagdollActive(false);
    }

    public void SetRagdollActive(bool active)
    {
        // Disable animator so physics takes full control
        if (animator != null)
            animator.enabled = !active;

        // Toggle rigidbodies
        foreach (var rb in ragdollRigidbodies)
        {
            rb.isKinematic = !active;
        }

        // Toggle colliders (skip root collider if present)
        foreach (var col in ragdollColliders)
        {
            if (col.gameObject != gameObject)
                col.enabled = active;
        }
    }

    void Update()
    {
        // Example trigger: Press 'R' to ragdoll
        if (Input.GetKeyDown(KeyCode.R))
        {
            SetRagdollActive(true);
        }
    }
}