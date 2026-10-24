using System;
using UnityEngine;

/// <summary>
/// Base class for all point-and-click psychological horror environmental events
/// (Breaker power outage, phone ringing, locked doors, printer anomalous prints, etc.).
/// </summary>
public abstract class EnvironmentalEvent : MonoBehaviour
{
    [Header("Event Configuration")]
    [Tooltip("Unique ID matching StoryDocument.eventTriggerId (e.g. BreakerOutage)")]
    public string eventId = "BreakerOutage";

    public bool IsActive { get; protected set; } = false;
    public bool IsResolved { get; protected set; } = false;

    public event Action OnEventTriggered;
    public event Action OnEventResolved;

    protected virtual void Awake()
    {
        if (EnvironmentalEventManager.Instance != null)
        {
            EnvironmentalEventManager.Instance.RegisterEvent(this);
        }
    }

    /// <summary>
    /// Initiates the environmental disturbance (e.g. blackout, phone ringing, etc.).
    /// </summary>
    public virtual void TriggerEvent()
    {
        IsActive = true;
        IsResolved = false;
        OnEventTriggered?.Invoke();
    }

    /// <summary>
    /// Resolves the disturbance when the player completes the required point-and-click interaction.
    /// </summary>
    public virtual void ResolveEvent()
    {
        IsActive = false;
        IsResolved = true;
        OnEventResolved?.Invoke();
    }
}
