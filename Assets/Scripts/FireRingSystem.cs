using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats), typeof(PlayerDamageService))]
public sealed class FireRingSystem : MonoBehaviour
{
    [SerializeField] private FireRingController fireRingPrefab;
    [SerializeField, Range(0, 4)] private int fireBallCount;

    private readonly List<FireRingController> activeFireBalls = new List<FireRingController>();
    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;

    public int FireBallCount => fireBallCount;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        damageService = GetComponent<PlayerDamageService>();

        if (fireRingPrefab == null)
        {
            GameObject prefabObject = Resources.Load<GameObject>("Prefabs/Firering");
            if (prefabObject != null)
                fireRingPrefab = prefabObject.GetComponent<FireRingController>();
        }
    }

    private void Start()
    {
        if (fireRingPrefab == null)
        {
            Debug.LogError("Firering prefab is missing a FireRingController.", this);
            enabled = false;
            return;
        }

        RebuildFireBalls();
    }

    public void SetFireBallCount(int value)
    {
        fireBallCount = Mathf.Clamp(value, 0, 4);
        RebuildFireBalls();
    }

    private void RebuildFireBalls()
    {
        foreach (FireRingController fireBall in activeFireBalls)
        {
            if (fireBall != null)
                Destroy(fireBall.gameObject);
        }
        activeFireBalls.Clear();

        if (fireRingPrefab == null)
            return;

        for (int index = 0; index < fireBallCount; index++)
        {
            FireRingController fireBall = Instantiate(
                fireRingPrefab,
                transform.position,
                Quaternion.identity);
            fireBall.Initialize(transform, index, fireBallCount, attackStats, damageService);
            activeFireBalls.Add(fireBall);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fireBallCount = Mathf.Clamp(fireBallCount, 0, 4);
    }
#endif
}
