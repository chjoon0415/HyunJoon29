using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMagnet : MonoBehaviour
{
    public static PlayerMagnet Instance { get; private set; }

    [Header("Item Magnet")]
    [SerializeField, Min(0f)] private float baseCollectRadius = 5f;
    [SerializeField, Min(0)] private int radiusPercent = 100;

    public Transform Target => transform;
    public float BaseCollectRadius => baseCollectRadius;
    public int RadiusPercent => radiusPercent;
    public float CurrentCollectRadius => baseCollectRadius * radiusPercent / 100f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one PlayerMagnet can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
    }

    public void SetRadiusPercent(int value)
    {
        radiusPercent = Mathf.Max(0, value);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        baseCollectRadius = Mathf.Max(0f, baseCollectRadius);
        radiusPercent = Mathf.Max(0, radiusPercent);
    }
#endif
}
