using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Handles player Look Behind transitions via LocationManager.
/// Acts like the Breaker Room: button click or Tab key fades to black into OfficeBehind,
/// and returns with fade to black back to the desk.
/// Only available during Night 2 and Night 3 (completely disabled in Night 1).
/// </summary>
public class LookBehindController : MonoBehaviour
{
    [Header("Look Behind Settings")]
    [Tooltip("If unchecked, look-behind is only triggered via the Tab key.")]
    public bool enableEdgeLook = true;
    [Range(0.01f, 0.35f)] public float edgeLookThreshold = 0.20f;
    public CanvasGroup lookBehindCanvasGroup;
    public float lookTransitionSpeed = 8f;

    [Header("Bottom-Right Prompt UI")]
    public RectTransform lookBehindPromptUI;
    public TextMeshProUGUI promptLabel;
    private Image promptBackground;
    private CanvasGroup promptCanvasGroup;
    private bool isPromptHovered = false;
    private bool hasFadedIn = false;
    private Coroutine promptFadeCoroutine;

    private float lastToggleTime = 0f;
    private const float TOGGLE_COOLDOWN = 0.4f;

    [Header("Boss Surveillance UI")]
    public CanvasGroup bossMonitorGroup;
    public bool hasDiscoveredCamera { get; private set; } = false;

    [Header("Office Behind Visuals")]
    [Tooltip("UI Image rendering the backroom part of the office (OfficeBehind.jpg).")]
    public Image officeBehindImage;
    public Sprite officeBehindSprite;

    public bool IsLookingAway { get; private set; } = false;

    /// <summary>
    /// Event fired whenever the player begins or stops looking behind.
    /// Param: true if now looking behind, false if returning to monitor.
    /// </summary>
    public event Action<bool> OnLookBehindChanged;

    private void Awake()
    {
        EnsureLookBehindVisuals();
    }

    private void Start()
    {
        EnsureLookBehindVisuals();
        EnsureBottomRightPromptUI();
        if (bossMonitorGroup != null)
        {
            bossMonitorGroup.alpha = 0f;
            bossMonitorGroup.gameObject.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged -= HandleLocationChanged;
            LocationManager.Instance.OnLocationChanged += HandleLocationChanged;
        }
    }

    private void OnDisable()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.OnLocationChanged -= HandleLocationChanged;
        }
    }

    private void HandleLocationChanged(string newLocation)
    {
        if (newLocation.Equals("OfficeBehind", StringComparison.OrdinalIgnoreCase))
        {
            IsLookingAway = true;
            OnLookBehindChanged?.Invoke(true);

            if (!hasDiscoveredCamera)
            {
                hasDiscoveredCamera = true;
            }
        }
        else if (newLocation.Equals("Office", StringComparison.OrdinalIgnoreCase))
        {
            IsLookingAway = false;
            OnLookBehindChanged?.Invoke(false);
        }
        else
        {
            IsLookingAway = false;
        }
    }

    /// <summary>
    /// Evaluates input and triggers fade-to-black room transitions via LocationManager.
    /// Only active during Night 2 and Night 3!
    /// </summary>
    public void HandleLookBehindInput(bool canLook)
    {
        // 1. Shift requirement check:
        // Night 1 (shift 0 / CurrentShift 1): Look Behind is COMPLETELY disabled & hidden.
        // Night 2 & 3 (shift >= 1 / CurrentShift >= 2): Look Behind is unlocked!
        int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.currentShiftIndex >= 0)
        {
            currentShift = gm.currentShiftIndex + 1;
        }

        if (currentShift < 2 || (gm != null && (gm.IsShiftCompleted || gm.IsVictory || gm.IsGameOver)))
        {
            if (lookBehindPromptUI != null && lookBehindPromptUI.gameObject.activeSelf)
            {
                lookBehindPromptUI.gameObject.SetActive(false);
            }
            return;
        }

        if (LocationManager.Instance == null) return;
        string currentLoc = LocationManager.Instance.CurrentLocationId;

        // If in transition, ignore inputs
        if (LocationManager.Instance.IsTransitioning) return;

        // If cannot look (event active, rebooting, or game over)
        if (!canLook)
        {
            if (lookBehindPromptUI != null && lookBehindPromptUI.gameObject.activeSelf)
            {
                lookBehindPromptUI.gameObject.SetActive(false);
            }
            return;
        }

        // If player is in the Office (desk view)
        if (currentLoc.Equals("Office", StringComparison.OrdinalIgnoreCase))
        {
            if (lookBehindPromptUI == null)
            {
                EnsureBottomRightPromptUI();
            }

            if (lookBehindPromptUI != null)
            {
                if (!hasFadedIn)
                {
                    hasFadedIn = true;
                    if (promptFadeCoroutine != null) StopCoroutine(promptFadeCoroutine);
                    promptFadeCoroutine = StartCoroutine(FadeInPromptInitial(1.0f, 1.5f));
                }
                else if (!lookBehindPromptUI.gameObject.activeSelf)
                {
                    lookBehindPromptUI.gameObject.SetActive(true);
                    if (promptCanvasGroup != null) promptCanvasGroup.alpha = 1f;
                }

                UpdatePromptHover();
            }

            float timeSinceToggle = Time.unscaledTime - lastToggleTime;
            bool tabPressed = Input.GetKeyDown(KeyCode.Tab);
            bool clickTriggered = isPromptHovered && Input.GetMouseButtonDown(0);

            if ((tabPressed || clickTriggered) && timeSinceToggle >= TOGGLE_COOLDOWN)
            {
                lastToggleTime = Time.unscaledTime;
                LookBehind();
            }
        }
        else if (currentLoc.Equals("OfficeBehind", StringComparison.OrdinalIgnoreCase))
        {
            // In OfficeBehind: Hide desk prompt; return button is inside OfficeBehind
            if (lookBehindPromptUI != null && lookBehindPromptUI.gameObject.activeSelf)
            {
                lookBehindPromptUI.gameObject.SetActive(false);
            }

            OfficeBehindInteractions interactions = FindAnyObjectByType<OfficeBehindInteractions>(FindObjectsInactive.Include);
            if (interactions != null && interactions.IsModalOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
                {
                    interactions.CloseModal();
                }
                return;
            }

            float timeSinceToggle = Time.unscaledTime - lastToggleTime;
            bool tabPressed = Input.GetKeyDown(KeyCode.Tab);
            bool rightClick = Input.GetMouseButtonDown(1);

            if ((tabPressed || rightClick) && timeSinceToggle >= TOGGLE_COOLDOWN)
            {
                lastToggleTime = Time.unscaledTime;
                LookBackAtMonitor();
            }
        }
        else
        {
            // In Breaker Room or other screen
            if (lookBehindPromptUI != null && lookBehindPromptUI.gameObject.activeSelf)
            {
                lookBehindPromptUI.gameObject.SetActive(false);
            }
        }
    }

    private void UpdatePromptHover()
    {
        if (lookBehindPromptUI == null) return;

        bool mouseOver = RectTransformUtility.RectangleContainsScreenPoint(lookBehindPromptUI, Input.mousePosition, null);
        if (mouseOver != isPromptHovered)
        {
            isPromptHovered = mouseOver;
            if (promptBackground != null)
            {
                promptBackground.color = isPromptHovered ?
                    new Color(0.22f, 0.25f, 0.28f, 0.70f) :
                    new Color(0.10f, 0.11f, 0.13f, 0.45f);
            }
        }
    }

    public void EnsureBottomRightPromptUI()
    {
        Canvas targetCanvas = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var c in canvases)
        {
            if (c.gameObject.name.Contains("Workplace") || c.gameObject.name.Contains("Gameplay") || c.gameObject.name.Contains("HUD"))
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null && canvases.Length > 0) targetCanvas = canvases[0];

        if (lookBehindPromptUI == null)
        {
            GameObject existing = GameObject.Find("LookBehindPrompt");
            if (existing != null)
            {
                lookBehindPromptUI = existing.GetComponent<RectTransform>();
                promptLabel = existing.GetComponentInChildren<TextMeshProUGUI>();
                promptBackground = existing.GetComponent<Image>();
            }
        }

        if (lookBehindPromptUI == null && targetCanvas != null)
        {
            GameObject promptObj = new GameObject("LookBehindPrompt");
            promptObj.transform.SetParent(targetCanvas.transform, false);

            lookBehindPromptUI = promptObj.AddComponent<RectTransform>();
            promptCanvasGroup = promptObj.AddComponent<CanvasGroup>();
            promptCanvasGroup.alpha = 0f;
            promptCanvasGroup.blocksRaycasts = false;

            promptBackground = promptObj.AddComponent<Image>();
            promptBackground.color = new Color(0.10f, 0.11f, 0.13f, 0.85f);

            Outline outline = promptObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.60f, 0.65f, 0.85f);
            outline.effectDistance = new Vector2(1f, 1f); // Balanced symmetrical border

            GameObject labelObj = new GameObject("PromptLabel");
            labelObj.transform.SetParent(promptObj.transform, false);

            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            promptLabel = labelObj.AddComponent<TextMeshProUGUI>();
            promptLabel.text = "[ ▼ LOOK BEHIND (TAB) ]";
            promptLabel.fontSize = 14;
            promptLabel.characterSpacing = 1.0f;
            promptLabel.margin = Vector4.zero;
            promptLabel.alignment = TextAlignmentOptions.Center;
            promptLabel.color = new Color(0.3f, 1f, 0.5f, 1f);
            promptLabel.fontStyle = FontStyles.Bold;
            promptLabel.textWrappingMode = TextWrappingModes.NoWrap;
            promptLabel.overflowMode = TextOverflowModes.Ellipsis;

            TMP_Text existingText = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
            if (existingText != null && existingText.font != null)
            {
                promptLabel.font = existingText.font;
            }

            promptObj.SetActive(false);
        }

        if (lookBehindPromptUI != null)
        {
            Canvas promptCanvas = lookBehindPromptUI.GetComponent<Canvas>();
            if (promptCanvas == null)
            {
                promptCanvas = lookBehindPromptUI.gameObject.AddComponent<Canvas>();
            }
            if (promptCanvas != null)
            {
                promptCanvas.overrideSorting = true;
                promptCanvas.sortingLayerName = targetCanvas != null ? targetCanvas.sortingLayerName : "UI";
                promptCanvas.sortingOrder = 850;
            }

            if (lookBehindPromptUI.GetComponent<GraphicRaycaster>() == null)
            {
                lookBehindPromptUI.gameObject.AddComponent<GraphicRaycaster>();
            }

            PositionPromptBottomCenter();
        }
    }

    public void PositionPromptBottomRight() => PositionPromptBottomCenter();

    public void PositionPromptBottomCenter()
    {
        if (lookBehindPromptUI == null) return;

        // Centered at bottom of screen
        lookBehindPromptUI.anchorMin = new Vector2(0.5f, 0f);
        lookBehindPromptUI.anchorMax = new Vector2(0.5f, 0f);
        lookBehindPromptUI.pivot = new Vector2(0.5f, 0f);
        lookBehindPromptUI.anchoredPosition = new Vector2(0f, 25f);
        lookBehindPromptUI.sizeDelta = new Vector2(360f, 46f);
        lookBehindPromptUI.transform.SetAsLastSibling();

        if (promptLabel != null)
        {
            promptLabel.margin = Vector4.zero;
            promptLabel.alignment = TextAlignmentOptions.Center;
            promptLabel.fontSize = 17f;
            promptLabel.fontStyle = FontStyles.Bold;
            promptLabel.textWrappingMode = TextWrappingModes.NoWrap;
            promptLabel.color = new Color(0.85f, 0.95f, 0.88f, 1f);
        }

        if (promptBackground != null)
        {
            promptBackground.color = new Color(0.10f, 0.11f, 0.13f, 0.45f);
        }

        Outline outline = lookBehindPromptUI.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = new Color(0.55f, 0.60f, 0.65f, 0.55f);
            outline.effectDistance = new Vector2(1f, 1f);
        }
    }

    private IEnumerator FadeInPromptInitial(float delayBeforeFade, float fadeDuration)
    {
        if (lookBehindPromptUI == null) yield break;

        lookBehindPromptUI.gameObject.SetActive(true);
        if (promptCanvasGroup == null)
        {
            promptCanvasGroup = lookBehindPromptUI.GetComponent<CanvasGroup>();
            if (promptCanvasGroup == null)
            {
                promptCanvasGroup = lookBehindPromptUI.gameObject.AddComponent<CanvasGroup>();
            }
        }

        promptCanvasGroup.alpha = 0f;
        promptCanvasGroup.blocksRaycasts = false;

        if (delayBeforeFade > 0f)
        {
            yield return new WaitForSecondsRealtime(delayBeforeFade);
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            }
            yield return null;
        }

        if (promptCanvasGroup != null)
        {
            promptCanvasGroup.alpha = 1f;
            promptCanvasGroup.blocksRaycasts = true;
        }
    }

    public void LookBehind()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.TransitionToLocation("OfficeBehind");
        }
    }

    public void LookBackAtMonitor()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.TransitionToLocation("Office");
        }
    }

    public IEnumerator FadeInCanvasGroup(CanvasGroup group)
    {
        if (group == null) yield break;

        float duration = 1.5f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(0f, 1f, elapsed / duration);
            yield return null;
        }
        group.alpha = 1f;
    }

    /// <summary>
    /// Ensures OfficeBehind panel is registered and styled under WorkplaceCanvas.
    /// Also ensures the Flashlight GameObject has FlashlightController attached.
    /// </summary>
    public void EnsureLookBehindVisuals()
    {
        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.EnsurePanelOpacityAndSprites();
        }

        // Auto-ensure Flashlight GameObject has FlashlightController component
        GameObject flashObj = GameObject.Find("Flashlight");
        if (flashObj != null)
        {
            FlashlightController fc = flashObj.GetComponent<FlashlightController>();
            if (fc == null) fc = flashObj.AddComponent<FlashlightController>();
        }
    }

    public void SetPromptVisible(bool visible)
    {
        if (lookBehindPromptUI != null)
        {
            lookBehindPromptUI.gameObject.SetActive(visible);
        }
    }
}
