using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MagnetCollectible : MonoBehaviour
{
    private static readonly HashSet<MagnetCollectible> ActiveCollectibles =
        new HashSet<MagnetCollectible>();

    [SerializeField, Min(0.01f)] private float absorbSpeed = 12f;
    [SerializeField, Min(0.01f)] private float collectDistance = 0.15f;

    private bool isBeingAbsorbed;
    private bool isCollected;
    private bool wasPulledBySuperMagnet;
    private float currentSpeed;

    public event Action<PlayerMagnet> ReachedPlayer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveCollectibles()
    {
        ActiveCollectibles.Clear();
    }

    private void OnEnable()
    {
        isBeingAbsorbed = false;
        isCollected = false;
        wasPulledBySuperMagnet = false;
        currentSpeed = absorbSpeed;
        ActiveCollectibles.Add(this);

        if (PlayerMagnet.Instance != null && PlayerMagnet.Instance.IsSuperMagnetActive)
            ForceSuperMagnetPull();
    }

    private void OnDisable()
    {
        ActiveCollectibles.Remove(this);
    }

    public static void PullAllActiveCollectibles()
    {
        foreach (MagnetCollectible collectible in ActiveCollectibles)
        {
            if (collectible != null && collectible.CompareTag("MagnetCollectible"))
                collectible.ForceSuperMagnetPull();
        }
    }

    private void ForceSuperMagnetPull()
    {
        if (isCollected)
            return;

        isBeingAbsorbed = true;
        wasPulledBySuperMagnet = true;
    }

    private void Update()
    {
        if (LevelUpPanelController.IsGamePaused || isCollected)
            return;

        PlayerMagnet magnet = PlayerMagnet.Instance;
        if (magnet == null || !magnet.isActiveAndEnabled)
            return;

        Vector3 playerPosition = magnet.Target.position;
        Vector2 difference = playerPosition - transform.position;
        float collectRadius = magnet.CurrentCollectRadius;
        bool isWithinNormalRange = difference.sqrMagnitude <= collectRadius * collectRadius;
        bool superMagnetActive = magnet.IsSuperMagnetActive &&
            CompareTag("MagnetCollectible");

        if (superMagnetActive)
        {
            isBeingAbsorbed = true;
            wasPulledBySuperMagnet = true;
            currentSpeed = Mathf.MoveTowards(
                currentSpeed,
                magnet.SuperMagnetMaxSpeed,
                magnet.SuperMagnetAcceleration * Time.deltaTime);
        }
        else
        {
            if (wasPulledBySuperMagnet)
            {
                wasPulledBySuperMagnet = false;
                isBeingAbsorbed = isWithinNormalRange;
            }

            currentSpeed = absorbSpeed;
            if (!isBeingAbsorbed && !isWithinNormalRange)
                return;

            isBeingAbsorbed = true;
        }

        transform.position = Vector3.MoveTowards(
            transform.position,
            playerPosition,
            currentSpeed * Time.deltaTime);

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
