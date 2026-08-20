using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerAttackStats), typeof(PlayerDamageService))]
public sealed class KillWaveSystem : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private KillWaveController killWavePrefab;
    [SerializeField] private KillWaveController fireKillWavePrefab;

    [Header("Level Up State")]
    [SerializeField, Min(0)] private int killsPerWave;

    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;
    private DOT_FIREController fireDotController;
    private int killCount;

    public int KillsPerWave => killsPerWave;
    public int KillCount => killCount;

    private void Awake()
    {
        attackStats = GetComponent<PlayerAttackStats>();
        damageService = GetComponent<PlayerDamageService>();
        fireDotController = GetComponent<DOT_FIREController>();

        if (killWavePrefab == null)
            killWavePrefab = LoadWavePrefab("Prefabs/Skill_KillWave");
        if (fireKillWavePrefab == null)
            fireKillWavePrefab = LoadWavePrefab("Prefabs/Skill_KillWaveFire");
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

    public void SetKillsPerWave(int value)
    {
        // Re-selecting KillWave replaces only the required kill count. Previously
        // accumulated kills are retained and can trigger waves immediately when
        // the new requirement is lower (for example, 27/30 becomes 7/20 after
        // one immediate wave).
        killsPerWave = Mathf.Max(0, value);
        TriggerPendingWaves();
    }

    private void HandleDamageApplied(PlayerDamageEvent damageEvent)
    {
        if (killsPerWave <= 0 || !damageEvent.Killed || !(damageEvent.Target is MonsterController))
            return;

        killCount++;
        TriggerPendingWaves();
    }

    private void TriggerPendingWaves()
    {
        if (killsPerWave <= 0)
            return;

        while (killCount >= killsPerWave)
        {
            killCount -= killsPerWave;
            SpawnWave();
        }
    }

    private void SpawnWave()
    {
        bool useFireWave = fireDotController != null && fireDotController.AttackPowerPercent > 0f;
        KillWaveController prefab = useFireWave && fireKillWavePrefab != null
            ? fireKillWavePrefab
            : killWavePrefab;

        if (prefab == null)
        {
            Debug.LogError("KillWaveSystem could not find a KillWaveController prefab.", this);
            return;
        }

        KillWaveController wave = Instantiate(prefab, transform.position, Quaternion.identity);
        wave.Initialize(attackStats, damageService);
    }

    private static KillWaveController LoadWavePrefab(string resourcePath)
    {
        GameObject prefabObject = Resources.Load<GameObject>(resourcePath);
        return prefabObject != null ? prefabObject.GetComponent<KillWaveController>() : null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        killsPerWave = Mathf.Max(0, killsPerWave);
    }
#endif
}
