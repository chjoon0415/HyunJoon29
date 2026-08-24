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

    [Header("Spawn Formations")]
    [SerializeField, Min(0.1f)] private float lineSpacing = 1.25f;
    [SerializeField, Min(0.1f)] private float crowdSpacing = 0.8f;
    [SerializeField, Min(0.1f)] private float tornadoArmSpacing = 0.65f;

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
            while (stageElapsedTime >= state.NextWaveTime &&
                   state.TotalSpawned < state.Rule.TotalBudget &&
                   state.WaveIndex < state.Rule.WaveCount)
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

        List<Vector3> positions = GetWaveSpawnPositions(state.Rule.SpawnShape, spawnCount);
        float tornadoDirection = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        for (int i = 0; i < spawnCount; i++)
        {
            MonsterController monster = Instantiate(state.Prefab, positions[i], Quaternion.identity);
            monster.Initialize(
                player,
                destroyedMonster => OnMonsterDestroyed(state, destroyedMonster),
                state.Rule.MonsterHP,
                state.Rule.AttackDamage,
                state.Rule.SpawnShape,
                tornadoDirection);
            state.TotalSpawned++;
            state.AliveCount++;
        }
    }

    private void OnMonsterDestroyed(SpawnState state, MonsterController monster)
    {
        state.AliveCount = Mathf.Max(0, state.AliveCount - 1);
    }

    private List<Vector3> GetWaveSpawnPositions(SpawnShape shape, int count)
    {
        List<Vector3> positions = new List<Vector3>(count);
        if (count <= 0)
            return positions;

        Vector3 playerPosition = player.position;
        GetViewExtents(playerPosition, out float halfWidth, out float halfHeight, out float outsideRadius);
        float baseAngle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        int formationSide = UnityEngine.Random.Range(0, 4);

        for (int i = 0; i < count; i++)
        {
            Vector2 offset;
            switch (shape)
            {
                case SpawnShape.LINE:
                    offset = GetLineOffset(i, count, halfWidth, halfHeight, formationSide);
                    break;
                case SpawnShape.CROWD:
                    offset = GetCrowdOffset(i, count, outsideRadius, baseAngle);
                    break;
                case SpawnShape.SQUARE:
                    offset = GetSquareOffset(i, count, halfWidth, halfHeight);
                    break;
                case SpawnShape.TORNADO:
                    offset = GetTornadoOffset(i, count, outsideRadius, baseAngle);
                    break;
                default:
                    float angle = baseAngle + Mathf.PI * 2f * i / count;
                    float radius = outsideRadius + UnityEngine.Random.Range(0f, spawnRingWidth);
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    break;
            }

            positions.Add(new Vector3(playerPosition.x + offset.x, playerPosition.y + offset.y, playerPosition.z));
        }

        return positions;
    }

    private void GetViewExtents(Vector3 playerPosition, out float halfWidth, out float halfHeight, out float outsideRadius)
    {
        float playerDepth = gameplayCamera.WorldToViewportPoint(playerPosition).z;
        halfWidth = 0f;
        halfHeight = 0f;
        outsideRadius = 0f;
        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Vector3 offset = gameplayCamera.ViewportToWorldPoint(new Vector3(x, y, playerDepth)) - playerPosition;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(offset.x));
                halfHeight = Mathf.Max(halfHeight, Mathf.Abs(offset.y));
                outsideRadius = Mathf.Max(outsideRadius, new Vector2(offset.x, offset.y).magnitude);
            }
        }
        halfWidth += outsideViewPadding;
        halfHeight += outsideViewPadding;
        outsideRadius += outsideViewPadding;
    }

    private Vector2 GetLineOffset(int index, int count, float halfWidth, float halfHeight, int side)
    {
        float centeredIndex = index - (count - 1) * 0.5f;
        switch (side)
        {
            case 0: return new Vector2(halfWidth, centeredIndex * lineSpacing);
            case 1: return new Vector2(centeredIndex * lineSpacing, halfHeight);
            case 2: return new Vector2(-halfWidth, centeredIndex * lineSpacing);
            default: return new Vector2(centeredIndex * lineSpacing, -halfHeight);
        }
    }

    private Vector2 GetCrowdOffset(int index, int count, float outsideRadius, float baseAngle)
    {
        float clusterRadius = Mathf.Max(crowdSpacing, Mathf.Sqrt(Mathf.Max(0, count - 1)) * crowdSpacing);
        Vector2 center = new Vector2(Mathf.Cos(baseAngle), Mathf.Sin(baseAngle)) *
                         (outsideRadius + clusterRadius);
        float angle = index * 2.399963f;
        float radius = crowdSpacing * Mathf.Sqrt(index);
        return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private Vector2 GetSquareOffset(int index, int count, float halfWidth, float halfHeight)
    {
        float perimeterPosition = index * 4f / count;
        int side = Mathf.FloorToInt(perimeterPosition) % 4;
        float t = perimeterPosition - Mathf.Floor(perimeterPosition);
        switch (side)
        {
            case 0: return new Vector2(Mathf.Lerp(-halfWidth, halfWidth, t), halfHeight);
            case 1: return new Vector2(halfWidth, Mathf.Lerp(halfHeight, -halfHeight, t));
            case 2: return new Vector2(Mathf.Lerp(halfWidth, -halfWidth, t), -halfHeight);
            default: return new Vector2(-halfWidth, Mathf.Lerp(-halfHeight, halfHeight, t));
        }
    }

    private Vector2 GetTornadoOffset(int index, int count, float outsideRadius, float baseAngle)
    {
        float progress = count > 1 ? index / (float)(count - 1) : 0f;
        float angle = baseAngle + progress * Mathf.PI * 4f;
        float radius = outsideRadius + progress * Mathf.Max(spawnRingWidth, tornadoArmSpacing * Mathf.Sqrt(count));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private void OnValidate()
    {
        currentStageId = Mathf.Max(1, currentStageId);
        outsideViewPadding = Mathf.Max(0.01f, outsideViewPadding);
        spawnRingWidth = Mathf.Max(0f, spawnRingWidth);
        lineSpacing = Mathf.Max(0.1f, lineSpacing);
        crowdSpacing = Mathf.Max(0.1f, crowdSpacing);
        tornadoArmSpacing = Mathf.Max(0.1f, tornadoArmSpacing);
    }
}
