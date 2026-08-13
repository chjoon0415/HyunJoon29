using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExpDropManager : MonoBehaviour
{
    public static ExpDropManager Instance { get; private set; }

    [Header("Orb Magnet")]
    [SerializeField, Min(0f)] private float magnetRange = 5f;

    [Header("Experience")]
    [SerializeField] private TextAsset levelXpCsv;
    [SerializeField, Min(0)] private int currentExperience;

    private LevelXPTable levelXPTable;
    private int currentLevel = 1;
    private int experienceToCurrentLevel;
    private int needXPForNextLevel;

    public float MagnetRange => magnetRange;
    public int CurrentExperience => currentExperience;
    public int CurrentLevel => currentLevel;
    public int CurrentLevelExperience => currentExperience - experienceToCurrentLevel;
    public int NeedXPForNextLevel => needXPForNextLevel;
    public Transform PlayerTransform => transform;

    public event Action<int> ExperienceChanged;
    public event Action<int> LevelUp;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one ExpDropManager can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;

        if (levelXpCsv == null)
            levelXpCsv = Resources.Load<TextAsset>("LevelXP");

        if (!TryLoadLevelTable())
            enabled = false;
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0 || levelXPTable == null)
            return;

        int previousLevel = currentLevel;
        currentExperience = (int)Math.Min((long)currentExperience + amount, int.MaxValue);
        RecalculateLevel();
        ExperienceChanged?.Invoke(currentExperience);

        for (int reachedLevel = previousLevel + 1; reachedLevel <= currentLevel; reachedLevel++)
            LevelUp?.Invoke(reachedLevel);
    }

    public void EvaluateExperience(float totalExperience, out int level, out float progress)
    {
        level = 1;
        progress = 0f;
        if (levelXPTable == null)
            return;

        float remainingExperience = Mathf.Max(0f, totalExperience);
        while (levelXPTable.TryGetNeedXP(level, out int needXP))
        {
            if (remainingExperience < needXP)
            {
                progress = remainingExperience / needXP;
                return;
            }

            remainingExperience -= needXP;
            level++;
        }

        // Reaching one level beyond the final row means every configured level is complete.
        progress = 1f;
    }

    private bool TryLoadLevelTable()
    {
        if (levelXpCsv == null)
        {
            Debug.LogError("LevelXP.csv was not found in Assets/Resources.", this);
            return false;
        }

        try
        {
            levelXPTable = LevelXPTable.Parse(levelXpCsv.text);
            RecalculateLevel();
            return true;
        }
        catch (FormatException exception)
        {
            Debug.LogError(exception.Message, this);
            return false;
        }
    }

    private void RecalculateLevel()
    {
        currentLevel = 1;
        experienceToCurrentLevel = 0;

        while (levelXPTable.TryGetNeedXP(currentLevel, out int needXP))
        {
            long nextLevelThreshold = (long)experienceToCurrentLevel + needXP;
            if (currentExperience < nextLevelThreshold)
            {
                needXPForNextLevel = needXP;
                return;
            }

            experienceToCurrentLevel = (int)Math.Min(nextLevelThreshold, int.MaxValue);
            currentLevel++;
        }

        needXPForNextLevel = 0;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        magnetRange = Mathf.Max(0f, magnetRange);
        currentExperience = Mathf.Max(0, currentExperience);
    }
#endif
}
