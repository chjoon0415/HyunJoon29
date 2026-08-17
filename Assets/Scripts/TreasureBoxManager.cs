using UnityEngine;

[DisallowMultipleComponent]
public sealed class TreasureBoxManager : MonoBehaviour
{
    [Header("Treasure Box Spawn")]
    [SerializeField] private TreasureBoxController treasureBoxPrefab;
    [SerializeField, Min(0.01f)] private float spawnCooldownSeconds = 30f;
    [SerializeField, Min(0f)] private float firstSpawnDelaySeconds = 10f;
    [SerializeField, Min(0)] private int maxActiveTreasureBoxes = 3;

    [Header("Scene References")]
    [SerializeField] private Transform player;
    [SerializeField] private Camera gameplayCamera;

    [Header("Spawn Area")]
    [SerializeField, Min(0.01f)] private float outsideViewPadding = 2f;
    [SerializeField, Min(0f)] private float randomDistanceRange = 2f;

    private float nextSpawnTime;

    private void Awake()
    {
        if (treasureBoxPrefab == null)
        {
            GameObject prefabObject = Resources.Load<GameObject>("Prefabs/TreasureBox");
            if (prefabObject != null)
                treasureBoxPrefab = prefabObject.GetComponent<TreasureBoxController>();
        }

        if (player == null)
        {
            PlayerMovement playerMovement = FindFirstObjectByType<PlayerMovement>();
            if (playerMovement != null)
                player = playerMovement.transform;
        }

        if (gameplayCamera == null)
            gameplayCamera = Camera.main;
    }

    private void Start()
    {
        nextSpawnTime = Time.time + firstSpawnDelaySeconds;

        if (treasureBoxPrefab == null || player == null || gameplayCamera == null)
        {
            Debug.LogError(
                "TreasureBoxManager needs a TreasureBox prefab, Player, and gameplay Camera.",
                this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused || Time.time < nextSpawnTime)
            return;

        nextSpawnTime = Time.time + spawnCooldownSeconds;
        if (TreasureBoxController.ActiveCount >= maxActiveTreasureBoxes)
            return;

        Instantiate(treasureBoxPrefab, GetSpawnPositionOutsideView(), Quaternion.identity);
    }

    private Vector3 GetSpawnPositionOutsideView()
    {
        Vector3 playerPosition = player.position;
        float playerDepth = gameplayCamera.WorldToViewportPoint(playerPosition).z;
        float farthestCornerDistance = 0f;

        for (int x = 0; x <= 1; x++)
        {
            for (int y = 0; y <= 1; y++)
            {
                Vector3 corner = gameplayCamera.ViewportToWorldPoint(
                    new Vector3(x, y, playerDepth));
                farthestCornerDistance = Mathf.Max(
                    farthestCornerDistance,
                    Vector2.Distance(playerPosition, corner));
            }
        }

        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float distance = farthestCornerDistance + outsideViewPadding +
            UnityEngine.Random.Range(0f, randomDistanceRange);
        Vector2 candidatePosition = (Vector2)playerPosition + direction * distance;

        return new Vector3(candidatePosition.x, candidatePosition.y, playerPosition.z);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        spawnCooldownSeconds = Mathf.Max(0.01f, spawnCooldownSeconds);
        firstSpawnDelaySeconds = Mathf.Max(0f, firstSpawnDelaySeconds);
        maxActiveTreasureBoxes = Mathf.Max(0, maxActiveTreasureBoxes);
        outsideViewPadding = Mathf.Max(0.01f, outsideViewPadding);
        randomDistanceRange = Mathf.Max(0f, randomDistanceRange);
    }
#endif
}
