#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class VampireSurvivorBalanceWindow : EditorWindow
{
    private const string StageMonsterPath = "Assets/Resources/StageMonster.csv";
    private const string StagePath = "Assets/Resources/Stage.csv";

    [Serializable]
    private sealed class BalanceRow
    {
        public int stageId;
        public string monsterId;
        public float spawnStartSec;
        public float waveIntervalSec;
        public int waveSizeStart;
        public int waveSizeGrowth;
        public int waveSizeMax;
        public int waveCount;
        public int totalBudget;
        public int maxAliveCap;
        public float monsterHP;
        public float attackDamage;
        public SpawnShape spawnShape;
    }

    private sealed class PreviewPoint
    {
        public float time;
        public float spawned;
        public float alive;
        public float playerDps;
    }

    [SerializeField] private int stageId = 1;
    [SerializeField] private float playerHealth = 100f;
    [SerializeField] private float playerAttackPower = 10f;
    [SerializeField] private float survivalTimeSec = 600f;
    [SerializeField] private float playerMoveSpeed = 4f;
    [SerializeField] private float monsterMoveSpeed = 2f;
    [SerializeField] private float firstMonsterHP = 10f;

    [SerializeField] private AnimationCurve stageDifficultyCurve = new AnimationCurve(
        new Keyframe(1f, 1f), new Keyframe(10f, 2f), new Keyframe(30f, 4f));
    [SerializeField] private AnimationCurve spawnPressureCurve = new AnimationCurve(
        new Keyframe(0f, 0.55f), new Keyframe(0.35f, 1f),
        new Keyframe(0.7f, 1.75f), new Keyframe(1f, 2.1f));
    [SerializeField] private AnimationCurve expectedPlayerGrowthCurve = new AnimationCurve(
        new Keyframe(0f, 1f), new Keyframe(0.5f, 1.7f),
        new Keyframe(0.72f, 2.7f), new Keyframe(1f, 5f));

    [SerializeField] private int phaseCount = 5;
    [SerializeField] private int maxMonsterTypes = 3;
    [SerializeField] private float baseWaveIntervalSec = 5f;
    [SerializeField, Range(0.25f, 2f)] private float quantityMultiplier = 1f;
    [SerializeField, Range(0.25f, 2f)] private float healthMultiplier = 1f;
    [SerializeField, Range(0.25f, 2f)] private float damageMultiplier = 1f;

    private readonly List<BalanceRow> rows = new List<BalanceRow>();
    private readonly List<PreviewPoint> timeline = new List<PreviewPoint>();
    private Vector2 scroll;
    private Vector2 tableScroll;
    private bool previewValid;
    private string statusMessage;

    [MenuItem("Tools/뱀서라이크 밸런스 툴")]
    private static void Open()
    {
        GetWindow<VampireSurvivorBalanceWindow>("뱀서 밸런스");
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUI.BeginChangeCheck();

        EditorGUILayout.LabelField("기준값", EditorStyles.boldLabel);
        stageId = EditorGUILayout.IntField(new GUIContent("스테이지 ID"), stageId);
        playerHealth = EditorGUILayout.FloatField(new GUIContent("플레이어 체력"), playerHealth);
        playerAttackPower = EditorGUILayout.FloatField(
            new GUIContent("플레이어 공격력", "기본 자동 사격 0.5초를 기준으로 초기 DPS를 계산합니다."),
            playerAttackPower);
        survivalTimeSec = EditorGUILayout.FloatField(new GUIContent("버텨야 할 시간 (초)"), survivalTimeSec);
        playerMoveSpeed = EditorGUILayout.FloatField(new GUIContent("플레이어 이동속도"), playerMoveSpeed);
        monsterMoveSpeed = EditorGUILayout.FloatField(new GUIContent("몬스터 이동속도"), monsterMoveSpeed);
        firstMonsterHP = EditorGUILayout.FloatField(new GUIContent("첫 등장 몬스터 체력"), firstMonsterHP);

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("난이도 곡선", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "스테이지 곡선의 X는 스테이지 ID, Y는 난이도 배율입니다. 시간 곡선의 X는 진행률(0~1)입니다. " +
            "스폰 압력은 끝까지 증가시키고, 예상 성장 곡선이 고비 이후 더 빨리 오르면 후반 학살 구간이 만들어집니다.",
            MessageType.Info);
        stageDifficultyCurve = EditorGUILayout.CurveField("스테이지 난이도", stageDifficultyCurve);
        spawnPressureCurve = EditorGUILayout.CurveField("시간별 스폰 압력", spawnPressureCurve);
        expectedPlayerGrowthCurve = EditorGUILayout.CurveField("예상 플레이어 성장", expectedPlayerGrowthCurve);

        EditorGUILayout.Space(10f);
        EditorGUILayout.LabelField("세부 튜닝", EditorStyles.boldLabel);
        phaseCount = EditorGUILayout.IntSlider(new GUIContent("스폰 규칙 행 수"), phaseCount, 2, 12);
        maxMonsterTypes = EditorGUILayout.IntSlider(
            new GUIContent("사용할 몬스터 종류 수", "Resources/Prefabs에서 찾은 몬스터 프리팹을 이름순으로 이 수만큼 순환 사용합니다."),
            maxMonsterTypes,
            1,
            12);
        baseWaveIntervalSec = EditorGUILayout.Slider(new GUIContent("기본 웨이브 간격"), baseWaveIntervalSec, 0.5f, 30f);
        quantityMultiplier = EditorGUILayout.Slider("수량 배율", quantityMultiplier, 0.25f, 2f);
        healthMultiplier = EditorGUILayout.Slider("체력 배율", healthMultiplier, 0.25f, 2f);
        damageMultiplier = EditorGUILayout.Slider("공격력 배율", damageMultiplier, 0.25f, 2f);

        if (EditorGUI.EndChangeCheck())
        {
            ClampInputs();
            previewValid = false;
            statusMessage = "값이 변경되었습니다. 다시 미리보기를 생성하세요.";
        }

        EditorGUILayout.Space(12f);
        if (GUILayout.Button("밸런싱 결과 미리보기 생성", GUILayout.Height(32f)))
            GeneratePreview();

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, previewValid ? MessageType.Info : MessageType.Warning);

        if (previewValid)
            DrawPreview();

        using (new EditorGUI.DisabledScope(!previewValid))
        {
            GUI.backgroundColor = new Color(0.55f, 0.9f, 0.55f);
            if (GUILayout.Button("OK - 인스펙터와 CSV에 반영", GUILayout.Height(38f)))
                ApplyPreview();
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.EndScrollView();
    }

    private void ClampInputs()
    {
        stageId = Mathf.Max(1, stageId);
        playerHealth = Mathf.Max(1f, playerHealth);
        playerAttackPower = Mathf.Max(0.01f, playerAttackPower);
        survivalTimeSec = Mathf.Max(10f, survivalTimeSec);
        playerMoveSpeed = Mathf.Max(0f, playerMoveSpeed);
        monsterMoveSpeed = Mathf.Max(0f, monsterMoveSpeed);
        firstMonsterHP = Mathf.Max(0.01f, firstMonsterHP);
        phaseCount = Mathf.Clamp(phaseCount, 2, 12);
        maxMonsterTypes = Mathf.Max(1, maxMonsterTypes);
        baseWaveIntervalSec = Mathf.Max(0.5f, baseWaveIntervalSec);
    }

    private void GeneratePreview()
    {
        ClampInputs();
        List<string> monsterIds = FindMonsterPrefabIds();
        if (monsterIds.Count == 0)
        {
            rows.Clear();
            timeline.Clear();
            previewValid = false;
            statusMessage = "Assets/Resources/Prefabs 아래에서 MonsterController가 붙은 프리팹을 찾지 못했습니다.";
            return;
        }

        rows.Clear();
        float stageDifficulty = Mathf.Max(0.1f, stageDifficultyCurve.Evaluate(stageId));
        int typeCount = Mathf.Min(maxMonsterTypes, monsterIds.Count);
        float baseDps = playerAttackPower * 2f;

        for (int i = 0; i < phaseCount; i++)
        {
            float progress = i / (float)phaseCount;
            float endProgress = Mathf.Min(1f, (i + 1f) / phaseCount);
            float startSec = i == 0 ? 1f : Mathf.Round(survivalTimeSec * progress);
            float pressureStart = Mathf.Max(0.05f, spawnPressureCurve.Evaluate(progress));
            float pressureEnd = Mathf.Max(pressureStart, spawnPressureCurve.Evaluate(1f));
            float growthAtStart = Mathf.Max(0.1f, expectedPlayerGrowthCurve.Evaluate(progress));
            float growthAtEnd = Mathf.Max(growthAtStart, expectedPlayerGrowthCurve.Evaluate(1f));
            float interval = Mathf.Clamp(baseWaveIntervalSec / Mathf.Sqrt(pressureStart), 0.5f, 30f);
            int waveCount = Mathf.Max(1, Mathf.FloorToInt((survivalTimeSec - startSec) / interval) + 1);

            float hp = i == 0
                ? firstMonsterHP
                : firstMonsterHP * healthMultiplier * stageDifficulty * (1f + progress * 0.35f);
            hp = RoundStat(hp);

            float initialKillRate = baseDps * growthAtStart / Mathf.Max(0.01f, hp);
            float finalKillRate = baseDps * growthAtEnd / Mathf.Max(0.01f, hp);
            int waveStart = Mathf.Max(1, Mathf.CeilToInt(initialKillRate * interval * pressureStart * quantityMultiplier));
            int waveMax = Mathf.Max(waveStart, Mathf.CeilToInt(finalKillRate * interval * pressureEnd * quantityMultiplier));
            int waveGrowth = waveCount > 1
                ? Mathf.Max(0, Mathf.CeilToInt((waveMax - waveStart) / (float)(waveCount - 1)))
                : 0;
            if (waveGrowth > 0)
                waveMax = Mathf.Min(waveMax, waveStart + waveGrowth * (waveCount - 1));

            int totalBudget = SumWaves(waveStart, waveGrowth, waveMax, waveCount);
            int maxAlive = Mathf.Max(waveMax * 3, Mathf.CeilToInt(finalKillRate * 10f * pressureEnd));
            float damage = playerHealth * (0.035f + 0.0125f * stageDifficulty) *
                           (0.85f + 0.35f * endProgress) * damageMultiplier;

            rows.Add(new BalanceRow
            {
                stageId = stageId,
                monsterId = monsterIds[i % typeCount],
                spawnStartSec = startSec,
                waveIntervalSec = Round(interval, 2),
                waveSizeStart = waveStart,
                waveSizeGrowth = waveGrowth,
                waveSizeMax = waveMax,
                waveCount = waveCount,
                totalBudget = totalBudget,
                maxAliveCap = maxAlive,
                monsterHP = hp,
                attackDamage = RoundStat(damage),
                spawnShape = (SpawnShape)(i % Enum.GetValues(typeof(SpawnShape)).Length)
            });
        }

        BuildTimeline();
        previewValid = true;
        int uniqueTypes = rows.Select(row => row.monsterId).Distinct().Count();
        statusMessage = $"스테이지 {stageId}: 규칙 {rows.Count}개, 몬스터 {uniqueTypes}종, " +
                        $"총 스폰 예산 {rows.Sum(row => row.totalBudget):N0}마리";
        if (maxMonsterTypes > monsterIds.Count)
        {
            statusMessage += $" (요청 {maxMonsterTypes}종 중 사용 가능한 몬스터 프리팹이 {monsterIds.Count}개입니다.)";
        }
    }

    private void BuildTimeline()
    {
        timeline.Clear();
        float alive = 0f;
        float baseDps = playerAttackPower * 2f;
        Dictionary<BalanceRow, int> spawnedWaves = rows.ToDictionary(row => row, row => 0);
        float step = Mathf.Max(1f, survivalTimeSec / 300f);

        for (float time = 0f; time <= survivalTimeSec + 0.01f; time += step)
        {
            float spawned = 0f;
            foreach (BalanceRow row in rows)
            {
                int targetWaveCount = time < row.spawnStartSec
                    ? 0
                    : Mathf.Min(row.waveCount, Mathf.FloorToInt((time - row.spawnStartSec) / row.waveIntervalSec) + 1);
                while (spawnedWaves[row] < targetWaveCount)
                {
                    int waveIndex = spawnedWaves[row];
                    spawned += Mathf.Min(row.waveSizeStart + waveIndex * row.waveSizeGrowth, row.waveSizeMax);
                    spawnedWaves[row]++;
                }
            }

            alive += spawned;
            float progress = Mathf.Clamp01(time / survivalTimeSec);
            float dps = baseDps * Mathf.Max(0.1f, expectedPlayerGrowthCurve.Evaluate(progress));
            float averageHP = rows.Count > 0 ? rows.Average(row => row.monsterHP) : firstMonsterHP;
            float kills = dps * step / Mathf.Max(0.01f, averageHP);
            alive = Mathf.Max(0f, alive - kills);

            timeline.Add(new PreviewPoint { time = time, spawned = spawned, alive = alive, playerDps = dps });
        }
    }

    private void DrawPreview()
    {
        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("결과 미리보기", EditorStyles.boldLabel);
        DrawTimelineGraph(GUILayoutUtility.GetRect(100f, 190f, GUILayout.ExpandWidth(true)));

        float peakAlive = timeline.Count == 0 ? 0f : timeline.Max(point => point.alive);
        float lastSpawnTime = rows.Count == 0 ? 0f : rows.Max(row =>
            row.spawnStartSec + Mathf.Max(0, row.waveCount - 1) * row.waveIntervalSec);
        EditorGUILayout.LabelField(
            $"예상 최대 누적 생존: {peakAlive:N0}마리   |   마지막 웨이브: {lastSpawnTime:N1}초 / {survivalTimeSec:N0}초");
        EditorGUILayout.HelpBox(
            "누적 생존은 입력 공격력, 기본 자동사격(초당 2회), 예상 성장 곡선을 사용한 상대 비교 지표입니다. " +
            "실제 범위 공격·관통·스킬 구성에 따라 플레이테스트 결과는 달라질 수 있습니다.",
            MessageType.None);

        tableScroll = EditorGUILayout.BeginScrollView(tableScroll, GUILayout.Height(Mathf.Min(250f, 46f + rows.Count * 22f)));
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        Header("몬스터", 90); Header("시작", 48); Header("간격", 48); Header("시작량", 50);
        Header("증가", 42); Header("최대량", 50); Header("웨이브", 48); Header("예산", 58);
        Header("최대존재", 60); Header("HP", 58); Header("공격력", 58); Header("진입 형태", 70);
        EditorGUILayout.EndHorizontal();
        foreach (BalanceRow row in rows)
        {
            EditorGUILayout.BeginHorizontal();
            Cell(row.monsterId, 90); Cell(Format(row.spawnStartSec), 48); Cell(Format(row.waveIntervalSec), 48);
            Cell(row.waveSizeStart.ToString(), 50); Cell(row.waveSizeGrowth.ToString(), 42); Cell(row.waveSizeMax.ToString(), 50);
            Cell(row.waveCount.ToString(), 48); Cell(row.totalBudget.ToString(), 58); Cell(row.maxAliveCap.ToString(), 60);
            Cell(Format(row.monsterHP), 58); Cell(Format(row.attackDamage), 58); Cell(row.spawnShape.ToString(), 70);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTimelineGraph(Rect rect)
    {
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
        if (timeline.Count < 2)
            return;

        float maxAlive = Mathf.Max(1f, timeline.Max(point => point.alive));
        float maxDps = Mathf.Max(1f, timeline.Max(point => point.playerDps));
        Handles.BeginGUI();
        DrawSeries(rect, timeline.Select(point => point.alive / maxAlive).ToArray(), new Color(1f, 0.45f, 0.25f));
        DrawSeries(rect, timeline.Select(point => point.playerDps / maxDps).ToArray(), new Color(0.3f, 0.85f, 1f));
        Handles.EndGUI();
        GUI.Label(new Rect(rect.x + 6f, rect.y + 4f, 220f, 20f), "주황: 누적 압박   파랑: 예상 플레이어 DPS", EditorStyles.miniLabel);
    }

    private static void DrawSeries(Rect rect, float[] values, Color color)
    {
        Vector3[] points = new Vector3[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float x = rect.x + i / (float)(values.Length - 1) * rect.width;
            float y = rect.yMax - Mathf.Clamp01(values[i]) * (rect.height - 8f) - 4f;
            points[i] = new Vector3(x, y);
        }
        Handles.color = color;
        Handles.DrawAAPolyLine(2f, points);
    }

    private void ApplyPreview()
    {
        if (!previewValid || rows.Count == 0)
            return;

        try
        {
            WriteStageMonsterCsv();
            WriteStageCsv();
            ApplyMonsterPrefabSpeed();
            int changedScenes = ApplySceneInspectors();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            statusMessage = $"반영 완료: CSV 2개, 몬스터 프리팹, 씬 {changedScenes}개를 갱신했습니다.";
            ShowNotification(new GUIContent("밸런스 적용 완료"));
        }
        catch (Exception exception)
        {
            statusMessage = "반영 실패: " + exception.Message;
            Debug.LogException(exception);
        }
    }

    private void WriteStageMonsterCsv()
    {
        string[] header =
        {
            "StageId", "MonsterId", "SpawnStartSec", "WaveIntervalSec", "WaveSizeStart",
            "WaveSizeGrowth", "WaveSizeMax", "WaveCount", "TotalBudget", "MaxAliveCap",
            "MonsterHP", "AttackDamage", "SpawnShape"
        };
        List<Dictionary<string, string>> keptRows = ReadCsvAsDictionaries(StageMonsterPath)
            .Where(values => !values.TryGetValue("StageId", out string value) || value != stageId.ToString(CultureInfo.InvariantCulture))
            .ToList();

        foreach (BalanceRow row in rows)
        {
            keptRows.Add(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StageId"] = row.stageId.ToString(CultureInfo.InvariantCulture),
                ["MonsterId"] = row.monsterId,
                ["SpawnStartSec"] = Format(row.spawnStartSec),
                ["WaveIntervalSec"] = Format(row.waveIntervalSec),
                ["WaveSizeStart"] = row.waveSizeStart.ToString(CultureInfo.InvariantCulture),
                ["WaveSizeGrowth"] = row.waveSizeGrowth.ToString(CultureInfo.InvariantCulture),
                ["WaveSizeMax"] = row.waveSizeMax.ToString(CultureInfo.InvariantCulture),
                ["WaveCount"] = row.waveCount.ToString(CultureInfo.InvariantCulture),
                ["TotalBudget"] = row.totalBudget.ToString(CultureInfo.InvariantCulture),
                ["MaxAliveCap"] = row.maxAliveCap.ToString(CultureInfo.InvariantCulture),
                ["MonsterHP"] = Format(row.monsterHP),
                ["AttackDamage"] = Format(row.attackDamage),
                ["SpawnShape"] = row.spawnShape.ToString()
            });
        }

        foreach (Dictionary<string, string> keptRow in keptRows)
        {
            if (!keptRow.TryGetValue("SpawnShape", out string shape) ||
                !Enum.TryParse(shape, true, out SpawnShape parsedShape) ||
                !Enum.IsDefined(typeof(SpawnShape), parsedShape))
            {
                keptRow["SpawnShape"] = SpawnShape.CIRCLE.ToString();
            }
        }

        WriteCsv(StageMonsterPath, header, keptRows.OrderBy(values => ParseSortInt(values, "StageId")));
    }

    private void WriteStageCsv()
    {
        List<Dictionary<string, string>> values = ReadCsvAsDictionaries(StagePath);
        Dictionary<string, string> target = values.FirstOrDefault(row =>
            row.TryGetValue("StageId", out string value) && value == stageId.ToString(CultureInfo.InvariantCulture));
        if (target == null)
        {
            target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["StageId"] = stageId.ToString(CultureInfo.InvariantCulture),
                ["StageName"] = "Stage " + stageId,
                ["TilemapName"] = "Tilemap",
                ["BGM"] = string.Empty
            };
            values.Add(target);
        }
        target["Time"] = Format(survivalTimeSec);
        WriteCsv(StagePath, new[] { "StageId", "StageName", "TilemapName", "Time", "BGM" },
            values.OrderBy(row => ParseSortInt(row, "StageId")));
    }

    private void ApplyMonsterPrefabSpeed()
    {
        HashSet<string> usedIds = new HashSet<string>(rows.Select(row => row.monsterId));
        foreach (string prefabPath in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            MonsterController monster = prefab != null ? prefab.GetComponent<MonsterController>() : null;
            if (monster == null || !usedIds.Contains(prefab.name))
                continue;

            SerializedObject serializedMonster = new SerializedObject(monster);
            serializedMonster.FindProperty("moveSpeed").floatValue = monsterMoveSpeed;
            serializedMonster.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
        }
    }

    private int ApplySceneInspectors()
    {
        int changedScenes = 0;
        foreach (string scenePath in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes" })
                     .Select(AssetDatabase.GUIDToAssetPath))
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            bool changed = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (PlayerHealth component in root.GetComponentsInChildren<PlayerHealth>(true))
                    changed |= SetFloat(component, "maxHP", playerHealth);
                foreach (PlayerAttackStats component in root.GetComponentsInChildren<PlayerAttackStats>(true))
                    changed |= SetFloat(component, "baseAttackPower", playerAttackPower);
                foreach (PlayerMovement component in root.GetComponentsInChildren<PlayerMovement>(true))
                    changed |= SetFloat(component, "moveSpeed", playerMoveSpeed);
                foreach (MonsterSpawnManager component in root.GetComponentsInChildren<MonsterSpawnManager>(true))
                {
                    changed |= SetInt(component, "currentStageId", stageId);
                    SerializedObject serialized = new SerializedObject(component);
                    SerializedProperty csvProperty = serialized.FindProperty("stageMonsterCsv");
                    UnityEngine.Object csv = AssetDatabase.LoadAssetAtPath<TextAsset>(StageMonsterPath);
                    if (csvProperty.objectReferenceValue != csv)
                    {
                        csvProperty.objectReferenceValue = csv;
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(component);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changedScenes++;
            }
            if (openedHere)
                EditorSceneManager.CloseScene(scene, true);
        }
        return changedScenes;
    }

    private static bool SetFloat(UnityEngine.Object target, string propertyName, float value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || Mathf.Approximately(property.floatValue, value))
            return false;
        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        return true;
    }

    private static bool SetInt(UnityEngine.Object target, string propertyName, int value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.intValue == value)
            return false;
        property.intValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(target);
        return true;
    }

    private static List<string> FindMonsterPrefabIds()
    {
        return AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Prefabs" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(prefab => prefab != null && prefab.GetComponent<MonsterController>() != null)
            .Select(prefab => prefab.name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int SumWaves(int start, int growth, int maximum, int count)
    {
        long sum = 0;
        for (int i = 0; i < count; i++)
            sum += Math.Min((long)start + (long)i * growth, maximum);
        return (int)Math.Min(int.MaxValue, sum);
    }

    private static float RoundStat(float value) => Round(value, value < 10f ? 2 : 1);
    private static float Round(float value, int decimals) => (float)Math.Round(value, decimals, MidpointRounding.AwayFromZero);
    private static string Format(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);
    private static void Header(string value, float width) => GUILayout.Label(value, EditorStyles.miniBoldLabel, GUILayout.Width(width));
    private static void Cell(string value, float width) => GUILayout.Label(value, EditorStyles.miniLabel, GUILayout.Width(width));

    private static int ParseSortInt(Dictionary<string, string> row, string key)
    {
        return row.TryGetValue(key, out string value) && int.TryParse(value, out int result) ? result : int.MaxValue;
    }

    private static List<Dictionary<string, string>> ReadCsvAsDictionaries(string assetPath)
    {
        if (!File.Exists(assetPath))
            return new List<Dictionary<string, string>>();
        List<List<string>> parsed = ParseCsv(File.ReadAllText(assetPath, Encoding.UTF8));
        if (parsed.Count == 0)
            return new List<Dictionary<string, string>>();
        List<string> header = parsed[0].Select(value => value.Trim().TrimStart('\uFEFF')).ToList();
        List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
        for (int rowIndex = 1; rowIndex < parsed.Count; rowIndex++)
        {
            if (parsed[rowIndex].All(string.IsNullOrWhiteSpace))
                continue;
            Dictionary<string, string> row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int column = 0; column < header.Count; column++)
                row[header[column]] = column < parsed[rowIndex].Count ? parsed[rowIndex][column] : string.Empty;
            if (row.TryGetValue("SpwanStartSec", out string legacyStart) && !row.ContainsKey("SpawnStartSec"))
                row["SpawnStartSec"] = legacyStart;
            result.Add(row);
        }
        return result;
    }

    private static void WriteCsv(
        string assetPath,
        IEnumerable<string> header,
        IEnumerable<Dictionary<string, string>> rowsToWrite)
    {
        string[] columns = header.ToArray();
        StringBuilder builder = new StringBuilder();
        builder.AppendLine(string.Join(",", columns.Select(EscapeCsv)));
        foreach (Dictionary<string, string> row in rowsToWrite)
        {
            builder.AppendLine(string.Join(",", columns.Select(column =>
                EscapeCsv(row.TryGetValue(column, out string value) ? value : string.Empty))));
        }
        File.WriteAllText(assetPath, builder.ToString(), new UTF8Encoding(false));
    }

    private static string EscapeCsv(string value)
    {
        value = value ?? string.Empty;
        return value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
    }

    private static List<List<string>> ParseCsv(string csv)
    {
        List<List<string>> rows = new List<List<string>>();
        List<string> row = new List<string>();
        StringBuilder field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < csv.Length; i++)
        {
            char current = csv[i];
            if (current == '"')
            {
                if (quoted && i + 1 < csv.Length && csv[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else quoted = !quoted;
            }
            else if (current == ',' && !quoted)
            {
                row.Add(field.ToString());
                field.Length = 0;
            }
            else if ((current == '\n' || current == '\r') && !quoted)
            {
                if (current == '\r' && i + 1 < csv.Length && csv[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Length = 0;
                rows.Add(row);
                row = new List<string>();
            }
            else field.Append(current);
        }
        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }
}
#endif
