using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMagnet : MonoBehaviour
{
    public static PlayerMagnet Instance { get; private set; }

    [Header("Item Magnet")]
    [SerializeField, Min(0f)] private float baseCollectRadius = 5f;
    [SerializeField, Min(0)] private int radiusPercent = 100;

    private float superMagnetRemainingTime;
    private float superMagnetAcceleration;
    private float superMagnetMaxSpeed;

    public Transform Target => transform;
    public float BaseCollectRadius => baseCollectRadius;
    public int RadiusPercent => radiusPercent;
    public float CurrentCollectRadius => baseCollectRadius * radiusPercent / 100f;
    public bool IsSuperMagnetActive => superMagnetRemainingTime > 0f;
    public float SuperMagnetAcceleration => superMagnetAcceleration;
    public float SuperMagnetMaxSpeed => superMagnetMaxSpeed;

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

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused || !IsSuperMagnetActive)
            return;

        superMagnetRemainingTime = Mathf.Max(
            0f,
            superMagnetRemainingTime - Time.deltaTime);
    }

    public void SetRadiusPercent(int value)
    {
        radiusPercent = Mathf.Max(0, value);
    }

    public void ActivateSuperMagnet(float duration, float acceleration, float maxSpeed)
    {
        superMagnetRemainingTime = Mathf.Max(0.01f, duration);
        superMagnetAcceleration = Mathf.Max(0.01f, acceleration);
        superMagnetMaxSpeed = Mathf.Max(0.01f, maxSpeed);
        MagnetCollectible.PullAllActiveCollectibles();
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
