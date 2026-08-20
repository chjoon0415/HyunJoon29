using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MagnetCollectible))]
public sealed class PotionController : MonoBehaviour
{
    [Header("Potion")]
    [SerializeField, Min(0f)] private float healAmount = 25f;

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

        PlayerHealth playerHealth = magnet.GetComponent<PlayerHealth>();
        if (playerHealth == null)
            return;

        isCollected = true;
        playerHealth.Heal(healAmount);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        healAmount = Mathf.Max(0f, healAmount);
    }
#endif
}
