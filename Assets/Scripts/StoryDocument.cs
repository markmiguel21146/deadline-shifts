using UnityEngine;

/// <summary>
/// Represents an individual story document or beat in the psychological horror progression.
/// Progresses from normal records -> strange records -> predictive logs -> computer recognizing player.
/// </summary>
[System.Serializable]
public class StoryDocument
{
    public enum DocumentCategory
    {
        NormalRecord,
        StrangeRecord,
        PredictiveLog,
        DirectEntityCommunication
    }

    [Header("Identification & Category")]
    public string documentId = "DOC_001";
    public DocumentCategory category = DocumentCategory.NormalRecord;
    public int shiftNumber = 1;

    [Header("Document Content")]
    [Tooltip("Header displayed at the top of the terminal (e.g. DOCUMENT #086)")]
    public string headerTitle = "DOCUMENT #086";

    [TextArea(2, 6)]
    [Tooltip("The text the player must type out into the terminal")]
    public string documentText = "Routine maintenance logged for terminal sub-station 4.";

    [Header("Post-Typing Automatic Log")]
    [Tooltip("If true, the computer immediately displays a system LOG after the player finishes typing.")]
    public bool hasPostLog = false;

    [Tooltip("Header for the post-typing LOG (e.g. SYS_ALERT // 03:14)")]
    public string logHeader = "SYS_ALERT // 03:14";

    [TextArea(2, 4)]
    [Tooltip("The message displayed by the computer (e.g. THE LIGHTS WILL BE CLOSING. RESTART THE BREAKER.)")]
    public string logMessage = "THE LIGHTS WILL BE CLOSING.\nRESTART THE BREAKER.";

    [Tooltip("Seconds to display the log before the environmental event triggers")]
    public float logDisplayDelay = 2.0f;

    [Header("Environmental Event Trigger")]
    [Tooltip("Event ID to trigger when this document is completed (e.g. BreakerOutage). Leave empty for none.")]
    public string eventTriggerId = "";

    [Tooltip("If true, typing is paused and blocked until the player resolves the environmental event.")]
    public bool pauseTypingForEvent = true;

    [Header("Post-Event Resolution (Optional)")]
    [Tooltip("Message displayed on the terminal once the event is resolved before loading next document.")]
    public string postResolutionMessage = "[ POWER RESTORED // PROTOCOL RESUMED ]";
}
