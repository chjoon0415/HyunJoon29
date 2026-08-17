using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats), typeof(PlayerDamageService), typeof(AutoShooter))]
public sealed class DOT_FIREController : MonoBehaviour
{
    private sealed class FireDebuff
    {
        public IPlayerAttackTarget Target;
        public float ExpiresAt;
        public float NextDamageAt;
    }

    [Header("Fire Debuff")]
    [SerializeField, Min(0.01f)] private float debuffDuration = 3f;
    [SerializeField, Min(0.01f)] private float damageInterval = 0.5f;
    [Tooltip("DOT damage as a percentage of the player's current attack power.")]
    [SerializeField, Min(0f)] private float attackPowerPercent;

    private readonly Dictionary<int, FireDebuff> activeDebuffs = new Dictionary<int, FireDebuff>();
    private readonly List<int> activeDebuffIds = new List<int>();
    private readonly List<int> expiredDebuffIds = new List<int>();
    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;
    private AutoShooter autoShooter;

    public float DebuffDuration => debuffDuration;
    public float DamageInterval => damageInterval;
    public float AttackPowerPercent => attackPowerPercent;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        damageService = GetComponent<PlayerDamageService>();
        autoShooter = GetComponent<AutoShooter>();
    }

    private void OnEnable()
    {
        if (damageService == null)
            damageService = GetComponent<PlayerDamageService>();
        if (damageService != null)
            damageService.DamageApplied += HandleDamageApplied;
    }

    private void OnDisable()
    {
        if (damageService != null)
            damageService.DamageApplied -= HandleDamageApplied;
        activeDebuffs.Clear();
    }

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused || activeDebuffs.Count == 0)
            return;

        float now = Time.time;
        activeDebuffIds.Clear();
        expiredDebuffIds.Clear();
        activeDebuffIds.AddRange(activeDebuffs.Keys);

        foreach (int targetId in activeDebuffIds)
        {
            if (!activeDebuffs.TryGetValue(targetId, out FireDebuff debuff))
                continue;

            if (debuff.Target == null || debuff.Target.TargetObject == null ||
                debuff.Target.IsDead || now > debuff.ExpiresAt)
            {
                expiredDebuffIds.Add(targetId);
                continue;
            }

            if (now < debuff.NextDamageAt)
                continue;

            float damage = attackStats.CalculateDamage(attackPowerPercent);
            damageService.DealDamage(
                debuff.Target,
                damage,
                PlayerDamageSource.FireDamageOverTime);

            if (debuff.Target.TargetObject == null || debuff.Target.IsDead)
                expiredDebuffIds.Add(targetId);
            else
                debuff.NextDamageAt = now + damageInterval;
        }

        foreach (int targetId in expiredDebuffIds)
            activeDebuffs.Remove(targetId);
    }

    public void SetAttackPowerPercent(int value)
    {
        // Re-selecting INCHANTFRIE replaces the previous DOT ratio.
        attackPowerPercent = Mathf.Max(0, value);
        if (autoShooter == null)
            autoShooter = GetComponent<AutoShooter>();
        if (autoShooter != null)
            autoShooter.SetFireEnchanted(attackPowerPercent > 0f);
    }

    private void HandleDamageApplied(PlayerDamageEvent damageEvent)
    {
        if (attackPowerPercent <= 0f ||
            damageEvent.Source == PlayerDamageSource.FireDamageOverTime ||
            damageEvent.Killed)
        {
            return;
        }

        IPlayerAttackTarget target = damageEvent.Target as IPlayerAttackTarget;
        if (target == null || target.TargetObject == null || target.IsDead)
            return;

        float now = Time.time;
        int targetId = target.AttackTargetId;
        if (activeDebuffs.TryGetValue(targetId, out FireDebuff existingDebuff))
        {
            // Repeated hits only refresh duration. Tick timing and damage do not stack.
            existingDebuff.ExpiresAt = now + debuffDuration;
            return;
        }

        activeDebuffs.Add(targetId, new FireDebuff
        {
            Target = target,
            ExpiresAt = now + debuffDuration,
            NextDamageAt = now + damageInterval
        });
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        debuffDuration = Mathf.Max(0.01f, debuffDuration);
        damageInterval = Mathf.Max(0.01f, damageInterval);
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
    }
#endif
}
