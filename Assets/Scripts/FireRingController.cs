using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public sealed class FireRingController : MonoBehaviour
{
    [Header("Orbit")]
    [SerializeField, Min(0f)] private float orbitDiameter = 6f;
    [SerializeField] private float degreesPerSecond = 90f;

    [Header("Damage")]
    [SerializeField, Min(0f)] private float attackPowerPercent = 50f;
    [SerializeField, Min(0.01f)] private float hitCooldown = 0.5f;

    private readonly Dictionary<int, float> nextDamageTimes = new Dictionary<int, float>();
    private Transform player;
    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;
    private int orbitIndex;
    private int orbitCount = 1;
    private float nextCleanupTime;

    public float OrbitDiameter => orbitDiameter;
    public float DegreesPerSecond => degreesPerSecond;
    public float AttackPowerPercent => attackPowerPercent;
    public float HitCooldown => hitCooldown;

    public void Initialize(
        Transform playerTransform,
        int index,
        int count,
        PlayerAttackStats playerAttackStats,
        PlayerDamageService playerDamageService)
    {
        player = playerTransform;
        orbitIndex = Mathf.Max(0, index);
        orbitCount = Mathf.Max(1, count);
        attackStats = playerAttackStats;
        damageService = playerDamageService;
        UpdateOrbitPosition();
    }

    private void FixedUpdate()
    {
        if (LevelUpPanelController.IsGamePaused)
            return;

        UpdateOrbitPosition();

        if (Time.time >= nextCleanupTime)
        {
            RemoveExpiredCooldowns();
            nextCleanupTime = Time.time + Mathf.Max(1f, hitCooldown);
        }
    }

    private void UpdateOrbitPosition()
    {
        if (player == null)
            return;

        float slotAngle = 360f * orbitIndex / orbitCount;
        float angle = (Time.time * degreesPerSecond + slotAngle) * Mathf.Deg2Rad;
        float radius = orbitDiameter * 0.5f;
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
        transform.position = player.position + offset;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamageMonster(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamageMonster(other);
    }

    private void TryDamageMonster(Collider2D other)
    {
        if (LevelUpPanelController.IsGamePaused || attackStats == null || damageService == null)
            return;

        IPlayerAttackTarget target = PlayerAttackTargetUtility.FindInParents(other);
        if (target == null)
            return;

        int targetId = target.AttackTargetId;
        if (nextDamageTimes.TryGetValue(targetId, out float nextDamageTime) && Time.time < nextDamageTime)
            return;

        nextDamageTimes[targetId] = Time.time + hitCooldown;
        float damage = attackStats.CalculateDamage(attackPowerPercent);
        damageService.DealDamage(target, damage);
    }

    private void RemoveExpiredCooldowns()
    {
        if (nextDamageTimes.Count == 0)
            return;

        List<int> expiredIds = null;
        foreach (KeyValuePair<int, float> cooldown in nextDamageTimes)
        {
            if (cooldown.Value > Time.time)
                continue;

            if (expiredIds == null)
                expiredIds = new List<int>();
            expiredIds.Add(cooldown.Key);
        }

        if (expiredIds == null)
            return;

        foreach (int expiredId in expiredIds)
            nextDamageTimes.Remove(expiredId);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        orbitDiameter = Mathf.Max(0f, orbitDiameter);
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
        hitCooldown = Mathf.Max(0.01f, hitCooldown);
    }
#endif
}
