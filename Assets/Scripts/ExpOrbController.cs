using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MagnetCollectible))]
public sealed class ExpOrbController : MonoBehaviour
{
    [Header("Experience Orb")]
    [SerializeField, Min(1)] private int expValue = 1;

    public int ExpValue => expValue;

    private bool isCollected;
    private MagnetCollectible magnetCollectible;

    private void Awake()
    {
        magnetCollectible = GetComponent<MagnetCollectible>();
    }

    private void OnEnable()
    {
        if (magnetCollectible != null)
            magnetCollectible.ReachedPlayer += Collect;
    }

    private void OnDisable()
    {
        if (magnetCollectible != null)
            magnetCollectible.ReachedPlayer -= Collect;
    }

    private void Collect(PlayerMagnet magnet)
    {
        if (LevelUpPanelController.IsGamePaused || isCollected)
            return;

        ExpDropManager manager = ExpDropManager.Instance;
        if (manager == null || !manager.isActiveAndEnabled)
            return;

        isCollected = true;
        manager.AddExperience(expValue);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        expValue = Mathf.Max(1, expValue);
    }
#endif
}
