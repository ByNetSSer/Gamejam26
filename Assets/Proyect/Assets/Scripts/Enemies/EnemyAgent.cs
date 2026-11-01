using UnityEngine;

public enum AttackRequirement { Any, PlayerGrounded, PlayerAirborne }

// A director can be assigned explicitly or discovered from a parent at runtime.
public abstract class EnemyAgent : MonoBehaviour
{
    [SerializeField] EnemyDirector director;
    protected EnemyDirector Director => director;
    protected PlayerController Player => director != null ? director.Player : null;
    public PlayerController TargetPlayer => Player;
    public abstract AttackRequirement Requirement { get; }
    public abstract bool CanRequestAttack { get; }
    public virtual bool CanBeRepelled => isActiveAndEnabled;
    public virtual bool UsesAttackTimeout => true;
    // Optional easy-mode contact point; traps and non-attacking encounters opt out.
    public virtual bool TryGetEasyParryPoint(Vector3 playerPosition, out Vector3 point) { point = default; return false; }
    public abstract void AttackGranted();
    public abstract void AttackCancelled();
    public abstract bool TryRepel(float power);
    public void SetDirector(EnemyDirector target)
    {
        if (director == target) return;
        if (director != null && isActiveAndEnabled) director.Unregister(this);
        director = target;
        if (director != null && isActiveAndEnabled) director.Register(this);
    }
    protected virtual void OnEnable()
    {
        if (director == null) director = GetComponentInParent<EnemyDirector>();
        if (director != null) director.Register(this);
    }
    protected virtual void Start()
    {
        if (director == null) SetDirector(GetComponentInParent<EnemyDirector>());
    }
    protected virtual void OnDisable()
    {
        if (director != null) director.Unregister(this);
    }
    protected void RequestAttack() { if (director != null) director.RequestAttack(this); }
    protected void ReleaseAttack() { if (director != null) director.ReleaseAttack(this); }
}
