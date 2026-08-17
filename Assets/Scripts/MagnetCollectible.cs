using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MagnetCollectible : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float absorbSpeed = 12f;
    [SerializeField, Min(0.01f)] private float collectDistance = 0.15f;

    private bool isBeingAbsorbed;
    private bool isCollected;

    public event Action<PlayerMagnet> ReachedPlayer;

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused || isCollected)
            return;

        PlayerMagnet magnet = PlayerMagnet.Instance;
        if (magnet == null || !magnet.isActiveAndEnabled)
            return;

        Vector3 playerPosition = magnet.Target.position;
        Vector2 difference = playerPosition - transform.position;

        if (!isBeingAbsorbed)
        {
            float collectRadius = magnet.CurrentCollectRadius;
            if (difference.sqrMagnitude > collectRadius * collectRadius)
                return;

            isBeingAbsorbed = true;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            playerPosition,
            absorbSpeed * Time.deltaTime);

        if (((Vector2)(playerPosition - transform.position)).sqrMagnitude <= collectDistance * collectDistance)
        {
            isCollected = true;
            ReachedPlayer?.Invoke(magnet);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        absorbSpeed = Mathf.Max(0.01f, absorbSpeed);
        collectDistance = Mathf.Max(0.01f, collectDistance);
    }
#endif
}
