using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAttackStats : MonoBehaviour
{
    [SerializeField, Min(0f)] private float baseAttackPower = 10f;
    [SerializeField, Min(0)] private int attackPercent = 100;

    public float BaseAttackPower => baseAttackPower;
    public int AttackPercent => attackPercent;
    public float CurrentAttackPower => baseAttackPower * attackPercent / 100f;

    public void SetAttackPercent(int value)
    {
        attackPercent = Mathf.Max(0, value);
    }

    public float CalculateDamage(float attackPowerPercent)
    {
        return CurrentAttackPower * Mathf.Max(0f, attackPowerPercent) / 100f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        baseAttackPower = Mathf.Max(0f, baseAttackPower);
        attackPercent = Mathf.Max(0, attackPercent);
    }
#endif
}
