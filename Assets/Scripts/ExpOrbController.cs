using UnityEngine;

[DisallowMultipleComponent]
public sealed class ExpOrbController : MonoBehaviour
{
    [Header("Experience Orb")]
    [SerializeField, Min(1)] private int expValue = 1;
    [SerializeField, Min(0.01f)] private float absorbSpeed = 12f;
    [SerializeField, Min(0.01f)] private float collectDistance = 0.15f;

    public int ExpValue => expValue;

    private bool isBeingAbsorbed;
    private bool isCollected;

    private void Update()
    {
        ExpDropManager manager = ExpDropManager.Instance;
        if (manager == null || !manager.isActiveAndEnabled)
            return;

        Vector3 playerPosition = manager.PlayerTransform.position;
        Vector2 difference = playerPosition - transform.position;

        if (!isBeingAbsorbed)
        {
            float magnetRange = manager.MagnetRange;
            if (difference.sqrMagnitude > magnetRange * magnetRange)
                return;

            isBeingAbsorbed = true;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            playerPosition,
            absorbSpeed * Time.deltaTime);

        if (((Vector2)(playerPosition - transform.position)).sqrMagnitude <= collectDistance * collectDistance)
            Collect(manager);
    }

    private void Collect(ExpDropManager manager)
    {
        if (isCollected)
            return;

        isCollected = true;
        manager.AddExperience(expValue);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        expValue = Mathf.Max(1, expValue);
        absorbSpeed = Mathf.Max(0.01f, absorbSpeed);
        collectDistance = Mathf.Max(0.01f, collectDistance);
    }
#endif
}
