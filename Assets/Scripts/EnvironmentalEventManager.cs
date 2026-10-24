using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central registry and dispatcher for all environmental horror events in the game.
/// Supports expandable events: Breaker outage, phone ringing, printer malfunctions, entity encounters.
/// </summary>
public class EnvironmentalEventManager : MonoBehaviour
{
    private static EnvironmentalEventManager instance;
    public static EnvironmentalEventManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<EnvironmentalEventManager>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    GameObject go = new GameObject("EnvironmentalEventManager");
                    instance = go.AddComponent<EnvironmentalEventManager>();
                }
            }
            return instance;
        }
        private set => instance = value;
    }

    private readonly Dictionary<string, EnvironmentalEvent> registeredEvents = new Dictionary<string, EnvironmentalEvent>(StringComparer.OrdinalIgnoreCase);

    public event Action<string> OnEventStarted;
    public event Action<string> OnEventCompleted;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // Auto-discover existing events in the scene
        EnvironmentalEvent[] existingEvents = FindObjectsByType<EnvironmentalEvent>(FindObjectsInactive.Include);
        foreach (var ev in existingEvents)
        {
            RegisterEvent(ev);
        }
    }

    public void RegisterEvent(EnvironmentalEvent ev)
    {
        if (ev == null || string.IsNullOrEmpty(ev.eventId)) return;

        if (!registeredEvents.ContainsKey(ev.eventId))
        {
            registeredEvents.Add(ev.eventId, ev);
        }
    }

    public void TriggerEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;

        if (registeredEvents.TryGetValue(eventId, out EnvironmentalEvent ev))
        {
            Debug.Log($"[EnvironmentalEventManager] Triggering Event: {eventId}");
            ev.TriggerEvent();
            OnEventStarted?.Invoke(eventId);
        }
        else
        {
            Debug.LogWarning($"[EnvironmentalEventManager] Event '{eventId}' was requested, but no handler is registered!");
        }
    }

    public void ResolveEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return;

        if (registeredEvents.TryGetValue(eventId, out EnvironmentalEvent ev))
        {
            Debug.Log($"[EnvironmentalEventManager] Resolving Event: {eventId}");
            ev.ResolveEvent();
            OnEventCompleted?.Invoke(eventId);
        }
    }

    public bool IsEventActive(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return false;
        return registeredEvents.TryGetValue(eventId, out EnvironmentalEvent ev) && ev.IsActive;
    }

    public bool IsAnyEventActive()
    {
        foreach (var ev in registeredEvents.Values)
        {
            if (ev != null && ev.IsActive) return true;
        }
        return false;
    }

    public EnvironmentalEvent GetEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return null;
        registeredEvents.TryGetValue(eventId, out EnvironmentalEvent ev);
        return ev;
    }
}
