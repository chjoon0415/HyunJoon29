using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerExperienceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ExpDropManager experienceManager;
    [SerializeField] private Image expBarFill;
    [SerializeField] private TMP_Text levelText;

    [Header("Animation")]
    [SerializeField, Min(0.01f)] private float baseFillDuration = 0.25f;
    [SerializeField, Min(0f)] private float durationPerLevelUp = 0.12f;
    [SerializeField, Min(1f)] private float fillPunchScale = 1.12f;

    private Coroutine fillRoutine;
    private float displayedExperience;
    private int displayedLevel;
    private Sprite runtimeFillSprite;

    private void Awake()
    {
        if (experienceManager == null)
            experienceManager = FindFirstObjectByType<ExpDropManager>();

        FindMissingUIReferences();
    }

    private void OnEnable()
    {
        if (experienceManager != null)
            experienceManager.ExperienceChanged += OnExperienceChanged;
    }

    private void Start()
    {
        if (experienceManager == null || expBarFill == null || levelText == null)
        {
            Debug.LogError("PlayerExperienceUI needs an ExpDropManager, ExpBar_Fill Image, and LevelText TMP.", this);
            enabled = false;
            return;
        }

        ConfigureFillImage();
        displayedExperience = experienceManager.CurrentExperience;
        ApplyDisplayedExperience(displayedExperience, false);
    }

    private void OnDisable()
    {
        if (experienceManager != null)
            experienceManager.ExperienceChanged -= OnExperienceChanged;

        if (fillRoutine != null)
        {
            StopCoroutine(fillRoutine);
            fillRoutine = null;
        }
    }

    private void OnExperienceChanged(int totalExperience)
    {
        if (!isActiveAndEnabled)
            return;

        if (fillRoutine != null)
            StopCoroutine(fillRoutine);

        fillRoutine = StartCoroutine(AnimateToExperience(totalExperience));
    }

    private IEnumerator AnimateToExperience(float targetExperience)
    {
        float startExperience = displayedExperience;
        experienceManager.EvaluateExperience(startExperience, out int startLevel, out _);
        experienceManager.EvaluateExperience(targetExperience, out int targetLevel, out _);
        float duration = baseFillDuration + Mathf.Max(0, targetLevel - startLevel) * durationPerLevelUp;
        float elapsed = 0f;
        int previousLevel = displayedLevel;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = 1f - Mathf.Pow(1f - t, 3f);
            displayedExperience = Mathf.Lerp(startExperience, targetExperience, easedT);

            experienceManager.EvaluateExperience(displayedExperience, out int evaluatedLevel, out _);
            if (evaluatedLevel != previousLevel)
            {
                previousLevel = evaluatedLevel;
                displayedLevel = evaluatedLevel;
                levelText.SetText("Level : {0}", displayedLevel);
                SetFillAmount(0f);
                yield return null;
            }

            ApplyDisplayedExperience(displayedExperience, true);
            yield return null;
        }

        displayedExperience = targetExperience;
        ApplyDisplayedExperience(displayedExperience, false);
        fillRoutine = null;
    }

    private void ApplyDisplayedExperience(float totalExperience, bool animatePunch)
    {
        experienceManager.EvaluateExperience(totalExperience, out displayedLevel, out float progress);
        SetFillAmount(progress);
        levelText.SetText("Level : {0}", displayedLevel);

        float scale = 1f;
        if (animatePunch)
            scale = 1f + Mathf.Sin(progress * Mathf.PI) * (fillPunchScale - 1f);

        expBarFill.rectTransform.localScale = new Vector3(1f, scale, 1f);
    }

    private void ConfigureFillImage()
    {
        // Image.fillAmount is ignored by Unity's generated mesh when an Image has no sprite.
        // Supply a neutral white sprite at runtime so the existing Inspector color is preserved.
        if (expBarFill.sprite == null)
        {
            runtimeFillSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                100f);
            runtimeFillSprite.name = "Runtime Exp Bar Fill";
            expBarFill.sprite = runtimeFillSprite;
        }

        expBarFill.type = Image.Type.Filled;
        expBarFill.fillMethod = Image.FillMethod.Horizontal;
        expBarFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        expBarFill.fillClockwise = true;
    }

    private void SetFillAmount(float progress)
    {
        expBarFill.fillAmount = Mathf.Clamp01(progress);
    }

    private void OnDestroy()
    {
        if (runtimeFillSprite != null)
            Destroy(runtimeFillSprite);
    }

    private void FindMissingUIReferences()
    {
        Image[] images = GetComponentsInChildren<Image>(true);
        foreach (Image image in images)
        {
            if (expBarFill == null && image.gameObject.name == "ExpBar_Fill")
                expBarFill = image;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (levelText == null && text.gameObject.name == "LevelText")
                levelText = text;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        baseFillDuration = Mathf.Max(0.01f, baseFillDuration);
        durationPerLevelUp = Mathf.Max(0f, durationPerLevelUp);
        fillPunchScale = Mathf.Max(1f, fillPunchScale);
    }
#endif
}
