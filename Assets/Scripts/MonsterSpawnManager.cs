using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MonsterSpawnManager : MonoBehaviour
{
    [Header("Stage")]
    [SerializeField, Min(1)] private int currentStageId = 1;
    [SerializeField] private TextAsset stageMonsterCsv;

    [Header("Scene References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera gameplayCamera;

    [Header("Spawn Area")]
    [SerializeField, Min(0.01f)] private float outsideViewPadding = 2f;
    [SerializeField, Min(0f)] private float spawnRingWidth = 2f;

    private readonly List<SpawnState> spawnStates = new List<SpawnState>();
    private float stageElapsedTime;

    private sealed class SpawnState
    {
        public StageMonsterRule Rule;
        public MonsterController Prefab;
        public int WaveIndex;
        public int TotalSpawned;
        public int AliveCount;
        public float NextWaveTime;
    }

    private void Awake()
    {
        if (player == null)
        {
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null)
                player = playerMovement.transform;
        }

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;

        if (stageMonsterCsv == null)
            stageMonsterCsv = Resources.Load<TextAsset>("StageMonster");
    }

    private void Start()
    {
        BuildSchedule();
    }

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused)
            return;

        if (player == null || gameplayCamera == null)
            return;

        stageElapsedTime += Time.deltaTime;

        foreach (SpawnState state in spawnStates)
        {
            while (stageElapsedTime >= state.NextWaveTime && state.TotalSpawned < state.Rule.TotalBudget)
            {
                RunWave(state);
                state.WaveIndex++;
                state.NextWaveTime += state.Rule.WaveIntervalSec;
            }
        }
    }

    private void BuildSchedule()
    {
        spawnStates.Clear();
        stageElapsedTime = 0f;

        if (stageMonsterCsv == null)
        {
            Debug.LogError("StageMonster.csv was not found in Assets/Resources.", this);
            enabled = false;
            return;
        }

        if (player == null || gameplayCamera == null)
        {
            Debug.LogError("MonsterSpawnManager needs a Player and gameplay Camera.", this);
            enabled = false;
            return;
        }

        List<StageMonsterRule> rules;
        try
        {
            rules = StageMonsterRule.ParseForStage(stageMonsterCsv.text, currentStageId);
        }
        catch (FormatException exception)
        {
            Debug.LogError(exception.Message, this);
            enabled = false;
            return;
        }

        foreach (StageMonsterRule rule in rules)
        {
            GameObject prefabObject = Resources.Load<GameObject>($"Prefabs/{rule.MonsterId}");
            MonsterController prefab = prefabObject != null ? prefabObject.GetComponent<MonsterController>() : null;
            if (prefab == null)
            {
                Debug.LogError(
                    $"Monster prefab 'Assets/Resources/Prefabs/{rule.MonsterId}.prefab' is missing or has no MonsterController.",
                    this);
                continue;
            }

            spawnStates.Add(new SpawnState
            {
                Rule = rule,
                Prefab = prefab,
                NextWaveTime = rule.SpawnStartSec
            });
        }

        if (rules.Count == 0)
            Debug.LogWarning($"StageMonster.csv has no rows for StageId {currentStageId}.", this);
    }

    private void RunWave(SpawnState state)
    {
        int requested = Mathf.Min(
            state.Rule.WaveSizeStart + state.WaveIndex * state.Rule.WaveSizeGrowth,
            state.Rule.WaveSizeMax);
        int budgetRemaining = state.Rule.TotalBudget - state.TotalSpawned;
        int aliveSlots = state.Rule.MaxAliveCap - state.AliveCount;
        int spawnCount = Mathf.Min(requested, budgetRemaining, aliveSlots);

        for (int i = 0; i < spawnCount; i++)
        {
            Vector3 position = GetSpawnPositionOutsideView();
            MonsterController monster = Instantiate(state.Prefab, position, Quaternion.identity);
            monster.Initialize(player, destroyedMonster => OnMonsterDestroyed(state, destroyedMonster));
            state.TotalSpawned++;
            state.AliveCount++;
        }
    }

    private void OnMonsterDestroyed(SpawnState state, MonsterController monster)
    {
        state.AliveCount = Mathf.Max(0, state.AliveCount - 1);
    }

    private Vector3 GetSpawnPositionOutsideView()
    {
        Vector3 playerPosition = player.position;
        float playerDepth = gameplayCamera.WorldToViewportPoint(playerPosition).z;
        float farthestCornerDistance = 0f;

        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Vector3 corner = gameplayCamera.ViewportToWorldPoint(new Vector3(x, y, playerDepth));
                float distance = Vector2.Distance(playerPosition, corner);
                farthestCornerDistance = Mathf.Max(farthestCornerDistance, distance);
            }
        }

        float radius = farthestCornerDistance + outsideViewPadding + UnityEngine.Random.Range(0f, spawnRingWidth);
        Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;
        if (direction == Vector2.zero)
            direction = Vector2.right;

        Vector2 spawnPoint = (Vector2)playerPosition + direction * radius;
        return new Vector3(spawnPoint.x, spawnPoint.y, playerPosition.z);
    }

    private void OnValidate()
    {
        currentStageId = Mathf.Max(1, currentStageId);
        outsideViewPadding = Mathf.Max(0.01f, outsideViewPadding);
        spawnRingWidth = Mathf.Max(0f, spawnRingWidth);
    }
}
