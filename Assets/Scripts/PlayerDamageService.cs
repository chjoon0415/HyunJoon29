using System;
using UnityEngine;

public interface IDamageable
{
    DamageResult ApplyDamage(float requestedDamage);
}

public interface IPlayerAttackTarget : IDamageable
{
    bool IsDead { get; }
    int AttackTargetId { get; }
    UnityEngine.Object TargetObject { get; }
}

public static class PlayerAttackTargetUtility
{
    public static IPlayerAttackTarget FindInParents(Collider2D collider)
    {
        if (collider == null)
            return null;

        Transform current = collider.transform;
        while (current != null)
        {
            MonoBehaviour[] behaviours = current.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IPlayerAttackTarget target)
                    return target;
            }

            current = current.parent;
        }

        return null;
    }
}

public readonly struct DamageResult
{
    public float AppliedDamage { get; }
    public bool Killed { get; }
    public Vector3 TargetPosition { get; }

    public DamageResult(float appliedDamage, bool killed, Vector3 targetPosition)
    {
        AppliedDamage = Mathf.Max(0f, appliedDamage);
        Killed = killed;
        TargetPosition = targetPosition;
    }
}

public enum PlayerDamageSource
{
    Attack,
    FireDamageOverTime
}

public readonly struct PlayerDamageEvent
{
    public IDamageable Target { get; }
    public float AppliedDamage { get; }
    public bool Killed { get; }
    public Vector3 TargetPosition { get; }
    public PlayerDamageSource Source { get; }

    public PlayerDamageEvent(
        IDamageable target,
        DamageResult result,
        PlayerDamageSource source)
    {
        Target = target;
        AppliedDamage = result.AppliedDamage;
        Killed = result.Killed;
        TargetPosition = result.TargetPosition;
        Source = source;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerHealth))]
public sealed class PlayerDamageService : MonoBehaviour
{
    [SerializeField, Min(0)] private int healPercent;

    private PlayerHealth playerHealth;

    public int HealPercent => healPercent;

    // Every player-owned attack must report damage through DealDamage.
    // Subscribers receive the final amount actually removed from the target.
    public event Action<float> DamageConfirmed;
    public event Action<PlayerDamageEvent> DamageApplied;
    // All player-owned attacks report confirmed kills through this event.
    public event Action<Vector3> EnemyKilled;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        DamageConfirmed += HealFromConfirmedDamage;
    }

    public void SetHealPercent(int value)
    {
        healPercent = Mathf.Max(0, value);
    }

    public float DealDamage(
        IDamageable target,
        float requestedDamage,
        PlayerDamageSource source = PlayerDamageSource.Attack)
    {
        if (LevelUpPanelController.IsGamePaused || target == null || requestedDamage <= 0f)
            return 0f;

        DamageResult result = target.ApplyDamage(requestedDamage);
        float appliedDamage = result.AppliedDamage;
        if (appliedDamage > 0f)
        {
            DamageConfirmed?.Invoke(appliedDamage);
            DamageApplied?.Invoke(new PlayerDamageEvent(target, result, source));
        }
        if (result.Killed)
            EnemyKilled?.Invoke(result.TargetPosition);

        return appliedDamage;
    }

    private void HealFromConfirmedDamage(float appliedDamage)
    {
        if (healPercent <= 0 || playerHealth == null)
            return;

        playerHealth.Heal(appliedDamage * healPercent / 100f);
    }

    private void OnDestroy()
    {
        DamageConfirmed -= HealFromConfirmedDamage;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        healPercent = Mathf.Max(0, healPercent);
    }
#endif
}
