using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExpDropManager : MonoBehaviour
{
    public static ExpDropManager Instance { get; private set; }

    [Header("Orb Magnet")]
    [SerializeField, Min(0f)] private float magnetRange = 5f;

    [Header("Experience")]
    [SerializeField, Min(0)] private int currentExperience;

    public float MagnetRange => magnetRange;
    public int CurrentExperience => currentExperience;
    public Transform PlayerTransform => transform;

    public event Action<int> ExperienceChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one ExpDropManager can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    public void AddExperience(int amount)
    {
        if (amount <= 0)
            return;

        currentExperience += amount;
        ExperienceChanged?.Invoke(currentExperience);
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
