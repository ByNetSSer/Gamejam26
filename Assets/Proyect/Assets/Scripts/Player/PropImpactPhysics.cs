using UnityEngine;

// Opt-in: the prop stays static until it survives a player hit.
[RequireComponent(typeof(ImpactTarget))]
public sealed class PropImpactPhysics : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float mass = 18f;
    [SerializeField, Min(0f)] float impulsePerStrength = 0.35f;
    [SerializeField, Min(0f)] float maximumImpulse = 7f;
    [SerializeField, Range(0f, 1f)] float upwardImpulse = 0.1f;
    ImpactTarget target;
    Rigidbody body;
    void Awake() { target = GetComponent<ImpactTarget>(); body = GetComponent<Rigidbody>(); }
    void OnEnable() { if (target == null) target = GetComponent<ImpactTarget>(); target.SurvivedHit += OnSurvivedHit; }
    void OnDisable() { if (target != null) target.SurvivedHit -= OnSurvivedHit; }
    void OnSurvivedHit(Vector3 direction, float strength)
    {
        if (direction.sqrMagnitude < 0.0001f || strength <= 0f || float.IsNaN(strength)) return;
        if (body == null)
        {
            // Dynamic bodies cannot use a non-convex mesh collider. Leave the object
            // static rather than changing a user-authored collider at runtime.
            foreach (var mesh in GetComponentsInChildren<MeshCollider>())
                if (mesh.enabled && !mesh.isTrigger && !mesh.convex && mesh.attachedRigidbody == null)
                { Debug.LogWarning("No se activó la física: hay un MeshCollider no convexo en " + name, this); return; }
            body = gameObject.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(0.01f, mass);
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        if (body.isKinematic) body.isKinematic = false;
        var impulse = direction.normalized + Vector3.up * upwardImpulse;
        body.AddForce(impulse.normalized * Mathf.Min(maximumImpulse, strength * impulsePerStrength), ForceMode.Impulse);
    }
}
