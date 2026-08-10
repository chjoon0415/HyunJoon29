using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerHealth : MonoBehaviour
{
    [Header("Player Health")]
    [SerializeField, Min(1f)] private float maxHP = 100f;
    [SerializeField, Min(0f)] private float invincibleTime = 1f;

    [Header("References")]
    [Tooltip("HPImage의 Image 컴포넌트. 비어 있으면 자식에서 자동으로 찾습니다.")]
    [SerializeField] private Image hpImage;
    [SerializeField, Min(0.02f)] private float blinkInterval = 0.1f;

    private SpriteRenderer playerSprite;
    private Coroutine invincibilityCoroutine;
    private float currentHP;
    private bool isInvincible;
    private bool isDead;

    public float MaxHP => maxHP;
    public float CurrentHP => currentHP;
    public bool IsInvincible => isInvincible;

    /// <summary>게임 오버 시스템이 추가되면 이 이벤트를 구독합니다.</summary>
    public event Action Died;

    private void Awake()
    {
        playerSprite = GetComponentInChildren<SpriteRenderer>();

        if (hpImage == null)
        {
            Image[] childImages = GetComponentsInChildren<Image>(true);
            foreach (Image childImage in childImages)
            {
                if (childImage.gameObject.name == "HPImage")
                {
                    hpImage = childImage;
                    break;
                }
            }
        }

        currentHP = maxHP;
        UpdateHealthUI();
    }

    /// <summary>무적 상태가 아닐 때 피해를 적용합니다. 실제로 피해를 받았으면 true를 반환합니다.</summary>
    public bool TakeDamage(float damage)
    {
        if (damage <= 0f || isDead || isInvincible)
        {
            return false;
        }

        currentHP = Mathf.Max(0f, currentHP - damage);
        UpdateHealthUI();

        if (currentHP <= 0f)
        {
            Die();
            return true;
        }

        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
        }

        invincibilityCoroutine = StartCoroutine(InvincibilityRoutine());
        return true;
    }

    /// <summary>체력을 회복하되 최대 체력을 넘지 않게 제한합니다.</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f || isDead)
        {
            return;
        }

        currentHP = Mathf.Min(maxHP, currentHP + amount);
        UpdateHealthUI();
    }

    /// <summary>최대 체력을 증가시킵니다. increaseCurrentHP가 true면 증가량만큼 현재 체력도 회복합니다.</summary>
    public void IncreaseMaxHP(float amount, bool increaseCurrentHP = true)
    {
        if (amount <= 0f)
        {
            return;
        }

        maxHP += amount;
        if (increaseCurrentHP && !isDead)
        {
            currentHP = Mathf.Min(maxHP, currentHP + amount);
        }

        UpdateHealthUI();
    }

    private IEnumerator InvincibilityRoutine()
    {
        isInvincible = true;
        float elapsed = 0f;

        while (elapsed < invincibleTime)
        {
            if (playerSprite != null)
            {
                playerSprite.enabled = !playerSprite.enabled;
            }

            yield return new WaitForSeconds(blinkInterval);
            elapsed += blinkInterval;
        }

        if (playerSprite != null)
        {
            playerSprite.enabled = true;
        }

        isInvincible = false;
        invincibilityCoroutine = null;
    }

    private void UpdateHealthUI()
    {
        if (hpImage != null)
        {
            hpImage.fillAmount = maxHP > 0f ? currentHP / maxHP : 0f;
        }
    }

    private void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        isInvincible = false;
        Died?.Invoke();
        // TODO: 추후 게임 오버 처리를 이곳에서 호출합니다.
    }

    private void OnDisable()
    {
        if (playerSprite != null)
        {
            playerSprite.enabled = true;
        }

        isInvincible = false;
        invincibilityCoroutine = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        maxHP = Mathf.Max(1f, maxHP);
        invincibleTime = Mathf.Max(0f, invincibleTime);
        blinkInterval = Mathf.Max(0.02f, blinkInterval);
    }
#endif
}
