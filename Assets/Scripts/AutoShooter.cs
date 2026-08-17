using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats))]
[RequireComponent(typeof(PlayerDamageService))]
[RequireComponent(typeof(PlayerMovement))]
public sealed class AutoShooter : MonoBehaviour
{
    [Header("Firing")]
    [SerializeField, Min(0.01f)] private float fireInterval = 0.5f;
    [SerializeField, Min(0f)] private float minTurnCooldown = 0.15f;
    [SerializeField, Range(1, 4)] private int projectileCount = 1;
    [SerializeField] private ProjectileController projectilePrefab;

    [Header("Spawn and Rotation")]
    [Tooltip("If empty, projectiles spawn at the Player transform.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Optional transform whose Z rotation is used as the projectile's base rotation.")]
    [SerializeField] private Transform baseRotationReference;

    private Vector2 lastFacingDirection = Vector2.down;
    private float nextFireTime;
    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;
    private PlayerMovement playerMovement;
    private Sprite fireProjectileSprite;
    private bool fireEnchanted;

    public int ProjectileCount => projectileCount;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        damageService = GetComponent<PlayerDamageService>();
        playerMovement = GetComponent<PlayerMovement>();
        fireProjectileSprite = Resources.Load<Sprite>("Sprites/Projectile_Fire");
    }

    private void OnEnable()
    {
        nextFireTime = Time.time;
    }

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused)
            return;

        Vector2 facingDirection = playerMovement.FacingDirection;
        if (!ApproximatelySameDirection(facingDirection, lastFacingDirection))
        {
            lastFacingDirection = facingDirection;
            nextFireTime = Mathf.Max(nextFireTime, Time.time + minTurnCooldown);
        }

        if (projectilePrefab != null && Time.time >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.time + fireInterval;
        }
    }

    private void Fire()
    {
        Transform origin = spawnPoint != null ? spawnPoint : transform;
        float baseRotation = baseRotationReference != null
            ? baseRotationReference.eulerAngles.z
            : 0f;

        for (int projectileIndex = 0; projectileIndex < projectileCount; projectileIndex++)
        {
            ProjectileController projectile = Instantiate(
                projectilePrefab,
                origin.position,
                Quaternion.identity);

            projectile.Initialize(
                GetProjectileDirection(projectileIndex),
                attackStats,
                damageService,
                baseRotation,
                fireEnchanted ? fireProjectileSprite : null);
        }
    }

    public void SetProjectileCount(int value)
    {
        projectileCount = Mathf.Clamp(value, 1, 4);
    }

    public void SetFireEnchanted(bool value)
    {
        fireEnchanted = value;
        if (fireEnchanted && fireProjectileSprite == null)
            Debug.LogWarning("Resources/Sprites/Projectile_Fire was not found.", this);
    }

    private Vector2 GetProjectileDirection(int projectileIndex)
    {
        // Build the extra directions relative to the current facing direction.
        // This prevents world up/down shots from overlapping the front/back
        // shots while the player is facing vertically.
        Vector2 perpendicularDirection = new Vector2(
            -lastFacingDirection.y,
            lastFacingDirection.x);

        switch (projectileIndex)
        {
            case 0: return lastFacingDirection;
            case 1: return -lastFacingDirection;
            case 2: return perpendicularDirection;
            case 3: return -perpendicularDirection;
            default: return lastFacingDirection;
        }
    }

    private static bool ApproximatelySameDirection(Vector2 a, Vector2 b)
    {
        return Vector2.Dot(a, b) > 0.999f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        projectileCount = Mathf.Clamp(projectileCount, 1, 4);
    }
#endif
}
