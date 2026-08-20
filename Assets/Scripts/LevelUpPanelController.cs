using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LevelUpPanelController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExpDropManager experienceManager;
    [SerializeField] private PlayerAttackStats playerAttackStats;
    [SerializeField] private PlayerDamageService playerDamageService;
    [SerializeField] private PlayerMagnet playerMagnet;
    [SerializeField] private AutoShooter autoShooter;
    [SerializeField] private FireRingSystem fireRingSystem;
    [SerializeField] private ExplosionController explosionController;
    [SerializeField] private DOT_FIREController fireDotController;
    [SerializeField] private KillWaveSystem killWaveSystem;
    [SerializeField] private TextAsset levelUpCardCsv;
    [SerializeField] private GameObject levelUpPanel;
    [SerializeField] private Button[] cards = new Button[3];

    private readonly HashSet<int> selectedCardIds = new HashSet<int>();
    private readonly LevelUpCardData[] displayedCards = new LevelUpCardData[3];
    private Image[] cardImages;
    private TMP_Text[] cardDescriptions;
    private LevelUpCardTable cardTable;
    private int pendingLevelUps;
    private float timeScaleBeforePause = 1f;
    private Coroutine nextPanelRoutine;

    public static bool IsGamePaused { get; private set; }
    public bool IsPanelOpen => levelUpPanel != null && levelUpPanel.activeSelf;
    public int PendingLevelUps => pendingLevelUps;

    // Sends the selected table ID so other systems can react without owning the UI.
    public event Action<int> CardSelected;

    private void Awake()
    {
        FindMissingReferences();
        TryLoadCardTable();

        if (levelUpPanel != null)
            levelUpPanel.SetActive(false);

        for (int i = 0; cards != null && i < cards.Length; i++)
        {
            int cardIndex = i;
            if (cards[i] != null)
                cards[i].onClick.AddListener(() => SelectCard(cardIndex));
        }
    }

    private void OnEnable()
    {
        if (experienceManager != null)
            experienceManager.LevelUp += QueueLevelUp;
    }

    private void Start()
    {
        if (experienceManager == null || playerAttackStats == null || playerDamageService == null ||
            playerMagnet == null || autoShooter == null || fireRingSystem == null || explosionController == null ||
            fireDotController == null || killWaveSystem == null ||
            levelUpPanel == null || cardTable == null ||
            cards == null || cards.Length < 3 || cards[0] == null || cards[1] == null || cards[2] == null ||
            cardImages == null || cardDescriptions == null ||
            cardImages[0] == null || cardImages[1] == null || cardImages[2] == null ||
            cardDescriptions[0] == null || cardDescriptions[1] == null || cardDescriptions[2] == null)
        {
            Debug.LogError(
                "LevelUpPanelController needs ExpDropManager, player combat/magnet components, LevelUpCard.csv, " +
                "Panel_LevelUp, and Card1~3 with CardImage/CardDesc children.", this);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (experienceManager != null)
            experienceManager.LevelUp -= QueueLevelUp;

        if (nextPanelRoutine != null)
        {
            StopCoroutine(nextPanelRoutine);
            nextPanelRoutine = null;
        }

        if (IsGamePaused)
            ResumeGame();
    }

    private void QueueLevelUp(int reachedLevel)
    {
        pendingLevelUps++;

        if (!IsPanelOpen && nextPanelRoutine == null)
            OpenNextPanel();
    }

    private void OpenNextPanel()
    {
        if (pendingLevelUps <= 0 || levelUpPanel == null)
            return;

        pendingLevelUps--;
        PauseGame();

        if (!TryDrawAndDisplayCards())
        {
            pendingLevelUps = 0;
            ResumeGame();
            return;
        }

        levelUpPanel.SetActive(true);
    }

    private void SelectCard(int cardIndex)
    {
        if (!IsPanelOpen)
            return;

        if (cardIndex < 0 || cardIndex >= displayedCards.Length || displayedCards[cardIndex] == null)
            return;

        LevelUpCardData selectedCard = displayedCards[cardIndex];
        ApplyEffect(selectedCard);
        selectedCardIds.Add(selectedCard.Id);
        CardSelected?.Invoke(selectedCard.Id);
        levelUpPanel.SetActive(false);

        if (pendingLevelUps > 0)
            nextPanelRoutine = StartCoroutine(OpenQueuedPanelNextFrame());
        else
            ResumeGame();
    }

    private IEnumerator OpenQueuedPanelNextFrame()
    {
        yield return null;
        nextPanelRoutine = null;
        OpenNextPanel();
    }

    private void PauseGame()
    {
        if (IsGamePaused)
            return;

        timeScaleBeforePause = Time.timeScale;
        IsGamePaused = true;
        Time.timeScale = 0f;
    }

    private void ResumeGame()
    {
        Time.timeScale = timeScaleBeforePause;
        IsGamePaused = false;
    }

    private bool TryDrawAndDisplayCards()
    {
        List<LevelUpCardData> pool = cardTable.GetEligibleCards(selectedCardIds);
        if (pool.Count < displayedCards.Length)
        {
            Debug.LogError(
                $"LevelUpCard.csv has only {pool.Count} eligible cards, but {displayedCards.Length} are required.",
                this);
            return false;
        }

        for (int slot = 0; slot < displayedCards.Length; slot++)
        {
            long totalWeight = 0;
            foreach (LevelUpCardData candidate in pool)
                totalWeight += candidate.Rate;

            if (totalWeight <= 0)
            {
                Debug.LogError("The eligible LevelUpCard.csv rows have no positive Rate.", this);
                return false;
            }

            double roll = UnityEngine.Random.value * totalWeight;
            int selectedIndex = pool.Count - 1;
            for (int candidateIndex = 0; candidateIndex < pool.Count; candidateIndex++)
            {
                roll -= pool[candidateIndex].Rate;
                if (roll < 0d)
                {
                    selectedIndex = candidateIndex;
                    break;
                }
            }

            LevelUpCardData selectedCard = pool[selectedIndex];
            pool.RemoveAt(selectedIndex);
            displayedCards[slot] = selectedCard;

            cardImages[slot].sprite = Resources.Load<Sprite>($"Sprites/{selectedCard.Icon}");
            if (cardImages[slot].sprite == null)
                Debug.LogWarning($"Card icon 'Resources/Sprites/{selectedCard.Icon}' was not found.", this);

            cardImages[slot].enabled = cardImages[slot].sprite != null;
            cardDescriptions[slot].text = selectedCard.Description;
        }

        return true;
    }

    private void ApplyEffect(LevelUpCardData selectedCard)
    {
        switch (selectedCard.Effect)
        {
            case LevelUpCardEffect.ATKUP:
                playerAttackStats.SetAttackPercent(selectedCard.Value);
                break;
            case LevelUpCardEffect.HEAL:
                playerDamageService.SetHealPercent(selectedCard.Value);
                break;
            case LevelUpCardEffect.MAGNET:
                playerMagnet.SetRadiusPercent(selectedCard.Value);
                break;
            case LevelUpCardEffect.PLUS1:
                autoShooter.SetProjectileCount(selectedCard.Value);
                break;
            case LevelUpCardEffect.FIRERING:
                fireRingSystem.SetFireBallCount(selectedCard.Value);
                break;
            case LevelUpCardEffect.EXPLOSION:
                explosionController.SetTriggerChance(selectedCard.Value);
                break;
            case LevelUpCardEffect.INCHANTFRIE:
                fireDotController.SetAttackPowerPercent(selectedCard.Value);
                break;
            case LevelUpCardEffect.KillWave:
                killWaveSystem.SetKillsPerWave(selectedCard.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(selectedCard.Effect), selectedCard.Effect, null);
        }
    }

    private void TryLoadCardTable()
    {
        if (levelUpCardCsv == null)
            levelUpCardCsv = Resources.Load<TextAsset>("LevelUpCard");

        if (levelUpCardCsv == null)
        {
            Debug.LogError("LevelUpCard.csv was not found in Assets/Resources.", this);
            return;
        }

        try
        {
            cardTable = LevelUpCardTable.Parse(levelUpCardCsv.text);
        }
        catch (FormatException exception)
        {
            Debug.LogError(exception.Message, this);
        }
    }

    private void FindMissingReferences()
    {
        if (experienceManager == null)
            experienceManager = FindFirstObjectByType<ExpDropManager>();
        if (playerAttackStats == null)
            playerAttackStats = FindFirstObjectByType<PlayerAttackStats>();
        if (playerDamageService == null)
            playerDamageService = FindFirstObjectByType<PlayerDamageService>();
        if (playerMagnet == null)
            playerMagnet = FindFirstObjectByType<PlayerMagnet>();
        if (autoShooter == null)
            autoShooter = FindFirstObjectByType<AutoShooter>();
        if (fireRingSystem == null)
            fireRingSystem = FindFirstObjectByType<FireRingSystem>();
        if (explosionController == null)
            explosionController = FindFirstObjectByType<ExplosionController>();
        if (fireDotController == null)
            fireDotController = FindFirstObjectByType<DOT_FIREController>();
        if (killWaveSystem == null)
            killWaveSystem = FindFirstObjectByType<KillWaveSystem>();

        Transform[] descendants = GetComponentsInChildren<Transform>(true);
        foreach (Transform descendant in descendants)
        {
            if (levelUpPanel == null && descendant.name == "Panel_LevelUp")
                levelUpPanel = descendant.gameObject;
        }

        if (levelUpPanel == null)
            return;

        Button[] panelButtons = levelUpPanel.GetComponentsInChildren<Button>(true);
        cards = new Button[3];
        foreach (Button button in panelButtons)
        {
            if (button.name == "Card1") cards[0] = button;
            else if (button.name == "Card2") cards[1] = button;
            else if (button.name == "Card3") cards[2] = button;
        }

        cardImages = new Image[3];
        cardDescriptions = new TMP_Text[3];
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null)
                continue;

            Image[] images = cards[i].GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image.name == "CardImage")
                    cardImages[i] = image;
            }

            TMP_Text[] texts = cards[i].GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in texts)
            {
                if (text.name == "CardDesc")
                    cardDescriptions[i] = text;
            }
        }
    }
}
