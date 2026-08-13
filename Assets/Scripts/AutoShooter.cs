using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats))]
public sealed class AutoShooter : MonoBehaviour
{
    [Header("Firing")]
    [SerializeField, Min(0.01f)] private float fireInterval = 0.5f;
    [SerializeField, Min(0f)] private float minTurnCooldown = 0.15f;
    [SerializeField] private ProjectileController projectilePrefab;

    [Header("Spawn and Rotation")]
    [Tooltip("If empty, projectiles spawn at the Player transform.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Optional transform whose Z rotation is used as the projectile's base rotation.")]
    [SerializeField] private Transform baseRotationReference;

    private Vector2 lastInputDirection = Vector2.down;
    private float nextFireTime;
    private PlayerAttackStats attackStats;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
    }

    private void OnEnable()
    {
        nextFireTime = Time.time;
    }

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused)
            return;

        Vector2 input = ReadKeyboardInput();
        if (input.sqrMagnitude > 0f)
        {
            Vector2 newDirection = input.normalized;
            if (!ApproximatelySameDirection(newDirection, lastInputDirection))
            {
                lastInputDirection = newDirection;
                nextFireTime = Mathf.Max(nextFireTime, Time.time + minTurnCooldown);
            }
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
        ProjectileController projectile = Instantiate(
            projectilePrefab,
            origin.position,
            Quaternion.identity);

        float baseRotation = baseRotationReference != null
            ? baseRotationReference.eulerAngles.z
            : 0f;

        projectile.Initialize(lastInputDirection, attackStats, baseRotation);
    }

    private static Vector2 ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            horizontal -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            horizontal += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            vertical -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            vertical += 1f;

        return new Vector2(horizontal, vertical);
    }

    private static bool ApproximatelySameDirection(Vector2 a, Vector2 b)
    {
        return Vector2.Dot(a, b) > 0.999f;
    }
}
