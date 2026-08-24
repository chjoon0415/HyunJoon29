using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class KillWaveController : MonoBehaviour
{
    [Header("Damage")]
    [Tooltip("Damage as a percentage of the player's current attack power.")]
    [SerializeField, Min(0f)] private float attackPowerPercent = 30f;
    [SerializeField] private LayerMask damageLayers;

    [Header("Wave")]
    [SerializeField, Min(0.01f)] private float expandSpeed = 15f;
    [SerializeField, Min(0.01f)] private float ringThickness = 1.5f;
    [SerializeField, Min(0.01f)] private float maxOuterRadius = 60f;
    [SerializeField, Min(0.01f)] private float maxDuration = 5f;

    [Header("Visual Ring")]
    [SerializeField, Range(16, 256)] private int visualSegments = 128;
    [SerializeField] private Color coreColor = new Color(0.4f, 0.95f, 1f, 1f);
    [SerializeField] private Color glowColor = new Color(0.05f, 0.35f, 1f, 0.35f);
    [SerializeField, Range(0.05f, 1f)] private float coreWidthRatio = 0.28f;

    private readonly HashSet<int> damagedTargetIds = new HashSet<int>();
    private readonly List<Collider2D> hitBuffer = new List<Collider2D>(64);
    private PlayerAttackStats attackStats;
    private PlayerDamageService damageService;
    private SpriteRenderer spriteRenderer;
    private LineRenderer glowRenderer;
    private LineRenderer coreRenderer;
    private Material runtimeLineMaterial;
    private ContactFilter2D damageFilter;
    private float outerRadius;
    private float elapsedTime;

    public float AttackPowerPercent => attackPowerPercent;
    public float ExpandSpeed => expandSpeed;
    public float RingThickness => ringThickness;
    public float OuterRadius => outerRadius;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.enabled = false;
        transform.localScale = Vector3.one;
        CreateVisualRing();

        // The supplied prefab collider is kept as an authoring aid only. Damage uses
        // the explicit annulus test below, never physics contact callbacks.
        Collider2D attachedCollider = GetComponent<Collider2D>();
        if (attachedCollider != null)
            attachedCollider.enabled = false;

        Rigidbody2D attachedBody = GetComponent<Rigidbody2D>();
        if (attachedBody != null)
            attachedBody.simulated = false;

        damageFilter = new ContactFilter2D();
        damageFilter.SetLayerMask(damageLayers);
        damageFilter.useTriggers = true;
    }

    public void Initialize(PlayerAttackStats playerAttackStats, PlayerDamageService playerDamageService)
    {
        attackStats = playerAttackStats;
        damageService = playerDamageService;
        outerRadius = 0f;
        elapsedTime = 0f;
        damagedTargetIds.Clear();
        UpdateVisualRing();
    }

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused)
            return;

        elapsedTime += Time.deltaTime;
        float previousOuterRadius = outerRadius;
        outerRadius = Mathf.Min(maxOuterRadius, outerRadius + expandSpeed * Time.deltaTime);

        UpdateVisualRing();
        DamageTargetsInSweptRing(previousOuterRadius);

        if (outerRadius >= maxOuterRadius || elapsedTime >= maxDuration)
            Destroy(gameObject);
    }

    private void DamageTargetsInSweptRing(float previousOuterRadius)
    {
        if (attackStats == null || damageService == null || outerRadius <= 0f)
            return;

        float damage = attackStats.CalculateDamage(attackPowerPercent);
        if (damage <= 0f)
            return;

        // Test the union of every ring position between the previous and current
        // frame. Using only the current annulus can skip monsters whenever a frame
        // takes long enough for the wave to advance farther than ringThickness.
        // This is especially visible when several waves update simultaneously.
        float innerRadius = Mathf.Max(0f, previousOuterRadius - ringThickness);
        float innerRadiusSquared = innerRadius * innerRadius;
        float outerRadiusSquared = outerRadius * outerRadius;
        Vector2 center = transform.position;

        hitBuffer.Clear();
        Physics2D.OverlapCircle(center, outerRadius, damageFilter, hitBuffer);
        foreach (Collider2D hit in hitBuffer)
        {
            IPlayerAttackTarget target = PlayerAttackTargetUtility.FindInParents(hit);
            if (target == null || target.IsDead || damagedTargetIds.Contains(target.AttackTargetId))
                continue;

            MonoBehaviour targetBehaviour = target as MonoBehaviour;
            if (targetBehaviour == null)
                continue;

            float distanceSquared = ((Vector2)targetBehaviour.transform.position - center).sqrMagnitude;
            if (distanceSquared < innerRadiusSquared || distanceSquared > outerRadiusSquared)
                continue;

            damagedTargetIds.Add(target.AttackTargetId);
            damageService.DealDamage(target, damage);
        }
    }

    private void CreateVisualRing()
    {
        Shader lineShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (lineShader == null)
            lineShader = Shader.Find("Sprites/Default");

        runtimeLineMaterial = lineShader != null
            ? new Material(lineShader)
            : new Material(spriteRenderer.sharedMaterial);

        if (runtimeLineMaterial.HasProperty("_MainTex"))
            runtimeLineMaterial.mainTexture = Texture2D.whiteTexture;

        glowRenderer = CreateLineRenderer("Glow", spriteRenderer.sortingOrder, glowColor);
        coreRenderer = CreateLineRenderer("Core", spriteRenderer.sortingOrder + 1, coreColor);
    }

    private LineRenderer CreateLineRenderer(string objectName, int sortingOrder, Color color)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.layer = gameObject.layer;
        lineObject.transform.SetParent(transform, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = visualSegments;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCapVertices = 0;
        line.numCornerVertices = 2;
        line.sharedMaterial = runtimeLineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerID = spriteRenderer.sortingLayerID;
        line.sortingOrder = sortingOrder;
        return line;
    }

    private void UpdateVisualRing()
    {
        if (glowRenderer == null || coreRenderer == null)
            return;

        float visibleWidth = Mathf.Min(ringThickness, outerRadius);
        float centerRadius = Mathf.Max(0f, outerRadius - visibleWidth * 0.5f);
        bool isVisible = centerRadius > Mathf.Epsilon && visibleWidth > Mathf.Epsilon;

        glowRenderer.enabled = isVisible;
        coreRenderer.enabled = isVisible;
        if (!isVisible)
            return;

        glowRenderer.widthMultiplier = visibleWidth;
        coreRenderer.widthMultiplier = visibleWidth * coreWidthRatio;

        for (int index = 0; index < visualSegments; index++)
        {
            float angle = index * Mathf.PI * 2f / visualSegments;
            Vector3 point = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * centerRadius;
            glowRenderer.SetPosition(index, point);
            coreRenderer.SetPosition(index, point);
        }
    }

    private void OnDestroy()
    {
        if (runtimeLineMaterial != null)
            Destroy(runtimeLineMaterial);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        attackPowerPercent = Mathf.Max(0f, attackPowerPercent);
        expandSpeed = Mathf.Max(0.01f, expandSpeed);
        ringThickness = Mathf.Max(0.01f, ringThickness);
        maxOuterRadius = Mathf.Max(0.01f, maxOuterRadius);
        maxDuration = Mathf.Max(0.01f, maxDuration);
        visualSegments = Mathf.Clamp(visualSegments, 16, 256);
        coreWidthRatio = Mathf.Clamp(coreWidthRatio, 0.05f, 1f);
    }

    private void OnDrawGizmosSelected()
    {
        float previewOuterRadius = Application.isPlaying ? outerRadius : ringThickness;
        float previewInnerRadius = Mathf.Max(0f, previewOuterRadius - ringThickness);
        Gizmos.color = new Color(0.1f, 0.8f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, previewOuterRadius);
        Gizmos.DrawWireSphere(transform.position, previewInnerRadius);
    }
#endif
}
