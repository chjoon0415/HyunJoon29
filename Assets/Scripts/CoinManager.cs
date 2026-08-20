using System;
using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CoinManager : MonoBehaviour
{
    public static CoinManager Instance { get; private set; }

    [Header("Coin UI")]
    [SerializeField] private TMP_Text coinText;

    private int currentCoins;

    public int CurrentCoins => currentCoins;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one CoinManager can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
        currentCoins = 0;

        if (coinText == null)
        {
            TMP_Text[] texts = FindObjectsByType<TMP_Text>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (TMP_Text text in texts)
            {
                if (text.gameObject.name == "CoinText")
                {
                    coinText = text;
                    break;
                }
            }
        }

        UpdateCoinText();
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        currentCoins = (int)Math.Min(int.MaxValue, (long)currentCoins + amount);
        UpdateCoinText();
    }

    private void UpdateCoinText()
    {
        if (coinText != null)
        {
            coinText.text = "Coin : " +
                currentCoins.ToString("N0", CultureInfo.InvariantCulture);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
