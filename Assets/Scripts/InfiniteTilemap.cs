using UnityEngine;
using UnityEngine.Tilemaps;

[DisallowMultipleComponent]
[RequireComponent(typeof(Grid))]
public sealed class InfiniteTilemap : MonoBehaviour
{
    private const int RepeatRadius = 1;
    private const int RepeatDiameter = RepeatRadius * 2 + 1;

    [Header("Map Source")]
    [SerializeField]
    private Tilemap sourceTilemap;

    [Header("Tracking")]
    [Tooltip("Usually the Main Camera. If empty, Camera.main is used automatically.")]
    [SerializeField]
    private Transform trackingTarget;

    [Header("Optional")]
    [Tooltip("Leave at zero to use the source TilemapRenderer's visual size.")]
    [SerializeField]
    private Vector2 repeatSizeOverride;

    private readonly Transform[,] repeatedTilemaps = new Transform[RepeatDiameter, RepeatDiameter];

    private Vector3 sourceLocalPosition;
    private Vector2 sourceVisualCenter;
    private Vector2 repeatSize;
    private Vector2Int currentCenter = new Vector2Int(int.MinValue, int.MinValue);

    private void Awake()
    {
        if (sourceTilemap == null)
        {
            sourceTilemap = GetComponentInChildren<Tilemap>();
        }

        if (sourceTilemap == null)
        {
            Debug.LogError("InfiniteTilemap needs a source Tilemap below the Grid.", this);
            enabled = false;
            return;
        }

        if (sourceTilemap.transform.parent != transform)
        {
            Debug.LogError("The source Tilemap must be a direct child of the InfiniteTilemap Grid.", this);
            enabled = false;
            return;
        }

        if (trackingTarget == null && Camera.main != null)
        {
            trackingTarget = Camera.main.transform;
        }

        TilemapRenderer sourceRenderer = sourceTilemap.GetComponent<TilemapRenderer>();
        if (sourceRenderer == null)
        {
            Debug.LogError("The source Tilemap needs a TilemapRenderer.", sourceTilemap);
            enabled = false;
            return;
        }

        sourceTilemap.RefreshAllTiles();
        sourceLocalPosition = sourceTilemap.transform.localPosition;

        Bounds visualBounds = sourceRenderer.localBounds;
        sourceVisualCenter = (Vector2)(sourceLocalPosition + visualBounds.center);
        repeatSize = repeatSizeOverride;

        if (repeatSize.x <= 0f)
        {
            repeatSize.x = visualBounds.size.x * Mathf.Abs(sourceTilemap.transform.localScale.x);
        }

        if (repeatSize.y <= 0f)
        {
            repeatSize.y = visualBounds.size.y * Mathf.Abs(sourceTilemap.transform.localScale.y);
        }

        if (repeatSize.x <= Mathf.Epsilon || repeatSize.y <= Mathf.Epsilon)
        {
            Debug.LogError("The source Tilemap must contain tiles with a non-zero visual size.", sourceTilemap);
            enabled = false;
            return;
        }

        CreateRepeatedTilemaps();
        UpdateRepeatedTilemapPositions(true);
    }

    private void LateUpdate()
    {
        if (trackingTarget == null)
        {
            if (Camera.main == null)
            {
                return;
            }

            trackingTarget = Camera.main.transform;
        }

        UpdateRepeatedTilemapPositions(false);
    }

    private void CreateRepeatedTilemaps()
    {
        for (int y = 0; y < RepeatDiameter; y++)
        {
            for (int x = 0; x < RepeatDiameter; x++)
            {
                if (x == RepeatRadius && y == RepeatRadius)
                {
                    repeatedTilemaps[x, y] = sourceTilemap.transform;
                    continue;
                }

                GameObject copy = Instantiate(sourceTilemap.gameObject, transform);
                copy.name = $"{sourceTilemap.name} (Runtime Copy {x - RepeatRadius}, {y - RepeatRadius})";
                copy.hideFlags = HideFlags.DontSave;
                repeatedTilemaps[x, y] = copy.transform;
            }
        }
    }

    private void UpdateRepeatedTilemapPositions(bool force)
    {
        Vector2 targetLocalPosition = sourceVisualCenter;
        if (trackingTarget != null)
        {
            targetLocalPosition = transform.InverseTransformPoint(trackingTarget.position);
        }

        Vector2 offsetFromSource = targetLocalPosition - sourceVisualCenter;
        Vector2Int nextCenter = new Vector2Int(
            Mathf.RoundToInt(offsetFromSource.x / repeatSize.x),
            Mathf.RoundToInt(offsetFromSource.y / repeatSize.y));

        if (!force && nextCenter == currentCenter)
        {
            return;
        }

        currentCenter = nextCenter;

        for (int y = 0; y < RepeatDiameter; y++)
        {
            for (int x = 0; x < RepeatDiameter; x++)
            {
                Vector2Int mapIndex = currentCenter + new Vector2Int(x - RepeatRadius, y - RepeatRadius);
                repeatedTilemaps[x, y].localPosition = sourceLocalPosition + new Vector3(
                    mapIndex.x * repeatSize.x,
                    mapIndex.y * repeatSize.y,
                    0f);
            }
        }
    }
}
