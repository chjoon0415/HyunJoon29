using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float damage);
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class ProjectileController : MonoBehaviour
{
    [Header("Projectile Stats")]
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField, Min(0f)] private float speed = 8f;
    [SerializeField, Min(0f)] private float lifetime = 3f;
    [SerializeField, Min(0.01f)] private float scale = 1f;

    private Rigidbody2D body;
    private SpriteRenderer spriteRenderer;
    private Vector2 moveDirection = Vector2.right;
    private bool hasHit;
    private float baseRotation;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        body.gravityScale = 0f;
        body.freezeRotation = true;
    }

    private void Start()
    {
        ApplySettings();
    }

    /// <summary>Called by AutoShooter immediately after this projectile is created.</summary>
    public void Initialize(Vector2 direction, float rotationOffset = 0f)
    {
        if (direction.sqrMagnitude > 0f)
        {
            moveDirection = direction.normalized;
        }

        baseRotation = rotationOffset;
        ApplySettings();
    }

    private void ApplySettings()
    {
        transform.localScale = Vector3.one * scale;
        body.linearVelocity = moveDirection * speed;
        UpdateVisualDirection();

        CancelInvoke(nameof(Expire));
        Invoke(nameof(Expire), lifetime);
    }

    private void UpdateVisualDirection()
    {
        bool firesLeft = moveDirection.x < 0f;
        float targetAngle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;

        // A left-facing sprite is produced by flipping the original right-facing
        // sprite. Subtracting 180 degrees then keeps diagonal-left shots aligned.
        float visualAngle = firesLeft ? targetAngle - 180f : targetAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, baseRotation + visualAngle);

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = firesLeft;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamageEnemy(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamageEnemy(collision.collider);
    }

    private void TryDamageEnemy(Collider2D other)
    {
        if (hasHit || other.gameObject.layer != LayerMask.NameToLayer("Enemy"))
        {
            return;
        }

        IDamageable damageable = other.GetComponentInParent<IDamageable>();
        if (damageable == null)
        {
            return;
        }

        hasHit = true;
        damageable.TakeDamage(damage);
        Destroy(gameObject);
    }

    private void Expire()
    {
        Destroy(gameObject);
    }
}
