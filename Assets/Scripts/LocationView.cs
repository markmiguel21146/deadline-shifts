using UnityEngine;

/// <summary>
/// Represents a fixed 2D point-and-click location/screen (e.g. Office, BreakerRoom).
/// </summary>
public class LocationView : MonoBehaviour
{
    [Header("Location Info")]
    [Tooltip("Unique ID matching transition calls (e.g. 'Office', 'BreakerRoom')")]
    public string locationId = "";

    [Tooltip("Check true if the game begins in this location.")]
    public bool isInitialLocation = false;

    [Tooltip("Whether typing input at the computer terminal is permitted in this location.")]
    public bool allowsTyping = true;

    [Header("UI & Display (Optional)")]
    public CanvasGroup canvasGroup;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (LocationManager.Instance != null && !string.IsNullOrEmpty(locationId))
        {
            LocationManager.Instance.RegisterLocation(this);
        }
    }

    public void SetActiveLocation(bool active)
    {
        gameObject.SetActive(active);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = active ? 1f : 0f;
            canvasGroup.interactable = active;
            canvasGroup.blocksRaycasts = active;
        }
    }
}
