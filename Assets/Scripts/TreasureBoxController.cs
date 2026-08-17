using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class TreasureBoxController : MonoBehaviour, IPlayerAttackTarget
{
    [Serializable]
    private sealed class ItemDrop
    {
        [SerializeField] private GameObject itemPrefab;
        [SerializeField, Range(0f, 100f)] private float dropChance;

        public GameObject ItemPrefab => itemPrefab;
        public float DropChance => dropChance;
    }

    private static readonly HashSet<TreasureBoxController> ActiveBoxes =
        new HashSet<TreasureBoxController>();

    [Header("Treasure Box Stats")]
    [SerializeField, Min(0.01f)] private float maxHealth = 10f;

    [Header("Item Drops")]
    [Tooltip("One 0-100 roll checks entries from top to bottom. Unused probability means no item drops.")]
    [SerializeField] private ItemDrop[] itemDrops = Array.Empty<ItemDrop>();

    private float currentHealth;
    private bool isDead;

    public static int ActiveCount => ActiveBoxes.Count;
    public bool IsDead => isDead;
    public int AttackTargetId => GetInstanceID();
    public UnityEngine.Object TargetObject => this;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveBoxes()
    {
        ActiveBoxes.Clear();
    }

    private void Awake()
    {
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        currentHealth = maxHealth;
    }

    private void OnEnable()
    {
        currentHealth = maxHealth;
        isDead = false;
        ActiveBoxes.Add(this);
    }

    private void OnDisable()
    {
        ActiveBoxes.Remove(this);
    }

    public DamageResult ApplyDamage(float damage)
    {
        Vector3 hitPosition = transform.position;
        if (LevelUpPanelController.IsGamePaused || isDead || damage <= 0f)
            return new DamageResult(0f, false, hitPosition);

        float appliedDamage = Mathf.Min(currentHealth, damage);
        currentHealth -= appliedDamage;
        bool killed = currentHealth <= 0f;

        if (killed)
        {
            isDead = true;
            TryDropItem(hitPosition);
            Destroy(gameObject);
        }

        return new DamageResult(appliedDamage, killed, hitPosition);
    }

    private void TryDropItem(Vector3 dropPosition)
    {
        float roll = UnityEngine.Random.Range(0f, 100f);
        float cumulativeChance = 0f;

        foreach (ItemDrop drop in itemDrops)
        {
            if (drop == null || drop.ItemPrefab == null || drop.DropChance <= 0f)
                continue;

            cumulativeChance += drop.DropChance;
            if (roll < cumulativeChance)
            {
                Instantiate(drop.ItemPrefab, dropPosition, Quaternion.identity);
                return;
            }
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxHealth = Mathf.Max(0.01f, maxHealth);
    }
#endif
}
