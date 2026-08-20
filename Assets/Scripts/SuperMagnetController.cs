using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MagnetCollectible))]
public sealed class SuperMagnetController : MonoBehaviour
{
    [Header("Super Magnet")]
    [SerializeField, Min(0.01f)] private float effectDurationSeconds = 5f;
    [SerializeField, Min(0.01f)] private float attractionAcceleration = 80f;
    [SerializeField, Min(0.01f)] private float maximumAttractionSpeed = 40f;

    private MagnetCollectible magnetCollectible;
    private bool isCollected;

    private void Awake()
    {
        magnetCollectible = GetComponent<MagnetCollectible>();
    }

    private void OnEnable()
    {
        isCollected = false;
        magnetCollectible.ReachedPlayer += Collect;
    }

    private void OnDisable()
    {
        if (magnetCollectible != null)
            magnetCollectible.ReachedPlayer -= Collect;
    }

    private void Collect(PlayerMagnet magnet)
    {
        if (LevelUpPanelController.IsGamePaused || isCollected || magnet == null)
            return;

        isCollected = true;
        magnet.ActivateSuperMagnet(
            effectDurationSeconds,
            attractionAcceleration,
            maximumAttractionSpeed);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        effectDurationSeconds = Mathf.Max(0.01f, effectDurationSeconds);
        attractionAcceleration = Mathf.Max(0.01f, attractionAcceleration);
        maximumAttractionSpeed = Mathf.Max(0.01f, maximumAttractionSpeed);
    }
#endif
}
