using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats), typeof(PlayerDamageService))]
public sealed class ExplosionController : MonoBehaviour
{
    [Header("Explosion Damage")]
    [SerializeField, Min(0f)] private float explosionRadius = 3f;
    [SerializeField] private LayerMask damageLayers;
    [Tooltip("Damage as a percentage of the player's current attack power.")]
    [SerializeField, Min(0f)] private float attackPowerPercent = 30f;

    [Header("Ring Effect")]
    [SerializeField] private GameObject explosionEffectPrefab;
    [SerializeField, Min(0.01f)] private float effectSize = 1f;
    [SerializeField, Min(0.01f)] private float effectDuration = 0.5f;

    [Header("Level Up State")]
    [SerializeField, Range(0, 100)] private int triggerChancePercent;

    private readonly Queue<Vector3> pendingExplosions = new Queue<Vector3>();
    private readonly HashSet<int> damagedTargetIds = new HashSet<int>();
    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;
    private Coroutine explosionRoutine;

    public float ExplosionRadius => explosionRadius;
    public float AttackPowerPercent => attackPowerPercent;
    public int TriggerChancePercent => triggerChancePercent;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        damageService = GetComponent<PlayerDamageService>();

        if (explosionEffectPrefab == null)
            explosionEffectPrefab = Resources.Load<GameObject>("Prefabs/ExplosionEffect");
    }

    private void OnEnable()
    {
        if (damageService == null)
            damageService = GetComponent<PlayerDamageService>();
        if (damageService != null)
            damageService.EnemyKilled += TryQueueExplosion;
    }

    private void OnDisable()
    {
        if (damageService != null)
            damageService.EnemyKilled -= TryQueueExplosion;

        pendingExplosions.Clear();
        explosionRoutine = null;
    }

    public void SetTriggerChance(int value)
    {
        // Re-selecting EXPLOSION replaces the old probability.
        triggerChancePercent = Mathf.Clamp(value, 0, 100);
    }

    private void TryQueueExplosion(Vector3 deathPosition)
    {
        if (triggerChancePercent <= 0)
            return;
        if (triggerChancePercent < 100 && Random.Range(0f, 100f) >= triggerChancePercent)
            return;

        pendingExplosions.Enqueue(deathPosition);
        if (explosionRoutine == null)
            explosionRoutine = StartCoroutine(ProcessExplosionQueue());
    }

    private IEnumerator ProcessExplosionQueue()
    {
        while (pendingExplosions.Count > 0)
        {
            Vector3 explosionPosition = pendingExplosions.Dequeue();
            SpawnRingEffect(explosionPosition);
            DamageMonsters(explosionPosition);

            // Separating chained explosions by one frame prevents deep recursive calls.
            yield return null;
        }

        explosionRoutine = null;
    }

    private void DamageMonsters(Vector3 explosionPosition)
    {
        if (attackStats == null || damageService == null || explosionRadius <= 0f)
            return;

        float damage = attackStats.CalculateDamage(attackPowerPercent);
        if (damage <= 0f)
            return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(explosionPosition, explosionRadius, damageLayers);
        damagedTargetIds.Clear();
        foreach (Collider2D hit in hits)
        {
            IPlayerAttackTarget target = PlayerAttackTargetUtility.FindInParents(hit);
            if (target == null || !damagedTargetIds.Add(target.AttackTargetId))
                continue;

            damageService.DealDamage(target, damage);
        }
    }

    private void SpawnRingEffect(Vector3 explosionPosition)
    {
        if (explosionEffectPrefab == null)
            return;

        GameObject effect = Instantiate(explosionEffectPrefab, explosionPosition, Quaternion.identity);
        StartCoroutine(AnimateRingEffect(effect));
    }

    private IEnumerator AnimateRingEffect(GameObject effect)
    {
        if (effect == null)
            yield break;

        SpriteRenderer[] renderers = effect.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] originalColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
            originalColors[i] = renderers[i].color;

        Vector3 startScale = Vector3.one * (effectSize * 0.05f);
        Vector3 targetScale = Vector3.one * effectSize;
        effect.transform.localScale = startScale;

        float elapsed = 0f;
        while (elapsed < effectDuration && effect != null)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / effectDuration);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 3f);
            effect.transform.localScale = Vector3.LerpUnclamped(startScale, targetScale, easedProgress);

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null)
                    continue;
                Color color = originalColors[i];
                color.a *= 1f - progress;
                renderers[i].color = color;
            }

            yield return null;
        }

        if (effect != null)
            Destroy(effect);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        explosionRadius = Mathf.Max(0f, explosionRadius);
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
        effectSize = Mathf.Max(0.01f, effectSize);
        effectDuration = Mathf.Max(0.01f, effectDuration);
        triggerChancePercent = Mathf.Clamp(triggerChancePercent, 0, 100);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.35f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
#endif
}
