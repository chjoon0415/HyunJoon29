using System;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class MonsterController : MonoBehaviour, IDamageable
{
    [Serializable]
    private sealed class ExpOrbDrop
    {
        [SerializeField] private ExpOrbController orbPrefab;
        [SerializeField, Range(0f, 100f)] private float dropChance;

        public ExpOrbController OrbPrefab => orbPrefab;
        public float DropChance => dropChance;
    }

    [Header("Monster Stats")]
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [FormerlySerializedAs("maxHealth")]
    [SerializeField, Min(0.01f)] private float maxHP = 3f;
    [SerializeField, Min(0f)] private float attackDamage = 10f;

    [Header("Experience Orb Drops")]
    [Tooltip("A single 0-100 roll checks entries from top to bottom. Unused probability means no orb drops.")]
    [SerializeField] private ExpOrbDrop[] expOrbDrops = Array.Empty<ExpOrbDrop>();

    [Header("Visual")]
    [SerializeField] private bool flipSpriteBasedOnTarget;
    [SerializeField] private SpriteRenderer spriteRenderer;

    private Rigidbody2D body;
    private Transform target;
    private Action<MonsterController> destroyedCallback;
    private float health;
    private bool isDead;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        body.gravityScale = 0f;
        body.freezeRotation = true;
        health = maxHP;
    }

    public void Initialize(Transform playerTarget, Action<MonsterController> onDestroyed)
    {
        target = playerTarget;
        destroyedCallback = onDestroyed;
        health = maxHP;
        isDead = false;
        UpdateSpriteDirection();
    }

    private void FixedUpdate()
    {
        if (isDead || target == null)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 direction = ((Vector2)target.position - body.position).normalized;
        body.linearVelocity = direction * moveSpeed;
        UpdateSpriteDirection();
    }

    private void UpdateSpriteDirection()
    {
        if (!flipSpriteBasedOnTarget || spriteRenderer == null || target == null)
            return;

        // The default image is used on the player's left and flipped on the right.
        spriteRenderer.flipX = body.position.x > target.position.x;
    }

    public void TakeDamage(float damage)
    {
        if (isDead || damage <= 0f)
            return;

        health -= damage;
        if (health <= 0f)
        {
            isDead = true;
            TryDropExperienceOrb();
            Destroy(gameObject);
        }
    }

    private void TryDropExperienceOrb()
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        float cumulativeChance = 0f;

        foreach (ExpOrbDrop drop in expOrbDrops)
        {
            if (drop == null || drop.OrbPrefab == null || drop.DropChance <= 0f)
                continue;

            cumulativeChance += drop.DropChance;
            if (roll < cumulativeChance)
            {
                Instantiate(drop.OrbPrefab, transform.position, Quaternion.identity);
                return;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamagePlayer(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryDamagePlayer(collision.collider);
    }

    private void TryDamagePlayer(Collider2D other)
    {
        if (isDead || attackDamage <= 0f)
        {
            return;
        }

        PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
        if (playerHealth != null)
        {
            // PlayerHealth가 무적 여부를 판정하므로 접촉 중에도 무적 시간마다 한 번만 피해를 줍니다.
            playerHealth.TakeDamage(attackDamage);
        }
    }

    private void OnDestroy()
    {
        Action<MonsterController> callback = destroyedCallback;
        destroyedCallback = null;
        callback?.Invoke(this);
    }
}
