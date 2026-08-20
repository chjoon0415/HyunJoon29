using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MagnetCollectible))]
public sealed class CoinPocketController : MonoBehaviour
{
    [Header("Coin Pocket")]
    [SerializeField, Min(0)] private int minimumCoins = 10;
    [SerializeField, Min(0)] private int maximumCoins = 100;

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
        if (LevelUpPanelController.IsGamePaused || isCollected)
            return;

        CoinManager coinManager = CoinManager.Instance;
        if (coinManager == null || !coinManager.isActiveAndEnabled)
            return;

        int coinAmount = Random.Range(minimumCoins, maximumCoins + 1);
        isCollected = true;
        coinManager.AddCoins(coinAmount);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        minimumCoins = Mathf.Max(0, minimumCoins);
        maximumCoins = Mathf.Max(minimumCoins, maximumCoins);
    }
#endif
}
