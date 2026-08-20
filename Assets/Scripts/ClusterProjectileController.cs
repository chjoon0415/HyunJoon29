using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class ClusterProjectileController : MonoBehaviour
{
    [Header("Projectile Movement")]
    [SerializeField, Min(0f)] private float speed = 7f;
    [SerializeField, Min(0.01f)] private float lifetime = 2f;

    private Rigidbody2D body;
    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;
    private LayerMask damageLayers;
    private float attackPowerPercent;
    private bool initialized;
    private bool hasHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    public void Initialize(
        Vector2 direction,
        PlayerAttackStats playerAttackStats,
        PlayerDamageService playerDamageService,
        float damagePercent,
        LayerMask targetLayers,
        float effectSize)
    {
        attackStats = playerAttackStats;
        damageService = playerDamageService;
        attackPowerPercent = Mathf.Max(0f, damagePercent);
        damageLayers = targetLayers;
        transform.localScale *= Mathf.Max(0.01f, effectSize);
        body.linearVelocity = direction.normalized * speed;
        initialized = true;

        CancelInvoke(nameof(Expire));
        Invoke(nameof(Expire), lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamageTarget(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamageTarget(collision.collider);
    }

    private void TryDamageTarget(Collider2D other)
    {
        if (!initialized || hasHit || other == null)
            return;
        if ((damageLayers.value & (1 << other.gameObject.layer)) == 0)
            return;

        // A Cluster projectile is consumed by any collider on a configured
        // monster layer, even when that monster is already dead or unavailable.
        hasHit = true;
        IPlayerAttackTarget target = PlayerAttackTargetUtility.FindInParents(other);
        if (!LevelUpPanelController.IsGamePaused && target != null && !target.IsDead)
        {
            float damage = attackStats != null
                ? attackStats.CalculateDamage(attackPowerPercent)
                : 0f;
            if (damageService != null)
                damageService.DealDamage(target, damage);
        }

        Destroy(gameObject);
    }

    private void Expire()
    {
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        speed = Mathf.Max(0f, speed);
        lifetime = Mathf.Max(0.01f, lifetime);
    }
#endif
}
