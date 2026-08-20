using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats), typeof(PlayerDamageService))]
public sealed class CLUSTERController : MonoBehaviour
{
    private static readonly Vector2[] ShotDirections =
    {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right
    };

    [Header("Cluster Damage")]
    [Tooltip("Damage as a percentage of the player's current attack power.")]
    [SerializeField, Min(0f)] private float attackPowerPercent = 20f;
    [Tooltip("Only colliders on these layers can receive Cluster damage.")]
    [SerializeField] private LayerMask damageLayers;

    [Header("Cluster Effect")]
    [SerializeField, Min(0.01f)] private float effectSize = 1f;
    [SerializeField] private ClusterProjectileController clusterProjectilePrefab;
    [SerializeField] private ClusterProjectileController clusterFireProjectilePrefab;

    [SerializeField, HideInInspector, Range(0, 100)] private int triggerChancePercent;

    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;
    private DOT_FIREController fireDotController;

    public float AttackPowerPercent => attackPowerPercent;
    public LayerMask DamageLayers => damageLayers;
    public float EffectSize => effectSize;
    public int TriggerChancePercent => triggerChancePercent;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        damageService = GetComponent<PlayerDamageService>();
        fireDotController = GetComponent<DOT_FIREController>();

        if (clusterProjectilePrefab == null)
            clusterProjectilePrefab = LoadProjectilePrefab("Prefabs/Skill_Cluster");
        if (clusterFireProjectilePrefab == null)
            clusterFireProjectilePrefab = LoadProjectilePrefab("Prefabs/Skill_ClusterFire");
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
    }

    public void SetTriggerChance(int value)
    {
        // Re-selecting CLUSTER replaces the previous card Value.
        triggerChancePercent = Mathf.Clamp(value, 0, 100);
    }

    private void HandleDamageApplied(PlayerDamageEvent damageEvent)
    {
        if (triggerChancePercent <= 0 || !damageEvent.Killed ||
            !(damageEvent.Target is MonsterController))
        {
            return;
        }

        if (triggerChancePercent < 100 && Random.Range(0f, 100f) >= triggerChancePercent)
            return;

        FireFourDirections(damageEvent.TargetPosition);
    }

    private void FireFourDirections(Vector3 origin)
    {
        bool useFireProjectile = fireDotController != null && fireDotController.AttackPowerPercent > 0f;
        ClusterProjectileController prefab = useFireProjectile && clusterFireProjectilePrefab != null
            ? clusterFireProjectilePrefab
            : clusterProjectilePrefab;

        if (prefab == null)
        {
            Debug.LogError("CLUSTERController could not find a Cluster projectile prefab.", this);
            return;
        }

        foreach (Vector2 direction in ShotDirections)
        {
            ClusterProjectileController projectile = Instantiate(prefab, origin, Quaternion.identity);
            projectile.Initialize(
                direction,
                attackStats,
                damageService,
                attackPowerPercent,
                damageLayers,
                effectSize);
        }
    }

    private static ClusterProjectileController LoadProjectilePrefab(string resourcePath)
    {
        GameObject prefabObject = Resources.Load<GameObject>(resourcePath);
        return prefabObject != null ? prefabObject.GetComponent<ClusterProjectileController>() : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
        effectSize = Mathf.Max(0.01f, effectSize);
        triggerChancePercent = Mathf.Clamp(triggerChancePercent, 0, 100);
    }
#endif
}
