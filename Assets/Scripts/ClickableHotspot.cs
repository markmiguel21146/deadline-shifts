using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Point-and-Click interaction hotspot for fixed 2D views.
/// Can be placed on UI elements (Images, Buttons) or 2D colliders.
/// Supports room navigation, environmental event resolution, audio cues, and hover cursor feedback.
/// </summary>
public class ClickableHotspot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static bool IsAnyHotspotHovered { get; private set; } = false;

    [Header("Hotspot Identification")]
    [Tooltip("Label displayed or used for player tooltip (e.g. 'Door to Breaker Room')")]
    public string hotspotName = "Interact";

    [Tooltip("Whether this hotspot is currently responsive to player clicks.")]
    public bool isInteractable = true;

    [Header("Actions")]
    [Tooltip("If specified, clicking transitions to this location (e.g. 'BreakerRoom', 'Office').")]
    public string targetLocationId = "";

    [Tooltip("If specified, clicking marks this environmental event as resolved (e.g. 'BreakerOutage').")]
    public string resolvesEventId = "";

    [Header("Audio & Feedback")]
    public AudioClip clickSound;
    public AudioSource audioSource;
    public Sprite hoverCursorSprite;

    [Header("Translucent / Hover Visuals (Optional)")]
    [Tooltip("If enabled, automatically manages a semi-transparent/translucent alpha on hover.")]
    public bool enableHoverAlpha = true;
    [Range(0f, 1f)] public float normalAlpha = 0.15f;
    [Range(0f, 1f)] public float hoverAlpha = 0.45f;
    public UnityEngine.UI.Graphic graphicTarget;

    [Header("Visual Cue / Pulsing Hazard (Blackout & Navigation Cues)")]
    [Tooltip("If enabled, pulses the graphic target and cue text with an unscaled sine oscillation.")]
    public bool enablePulse = false;
    public float pulseSpeed = 2.8f;
    [Range(0f, 1f)] public float pulseMinAlpha = 0.15f;
    [Range(0f, 1f)] public float pulseMaxAlpha = 0.42f;
    public Color pulseColor = Color.white; // FNAF 4 transparent white style
    public TMP_Text cueText;
    public string visualCueLabel = "";

    [Header("Font & Text Settings")]
    [Tooltip("Custom font asset for visual cue label text. If left unassigned, uses the scene's retro TMP font (HomeVideo SDF) or parent font.")]
    public TMP_FontAsset customFont;
    public int cueFontSize = 20;

    [Header("Border & Rounded Corner Styling")]
    [Tooltip("If enabled, automatically styles this hotspot with smooth rounded corners and a distinct border.")]
    public bool useRoundedBorder = true;
    public int cornerRadius = 24;
    public int borderThickness = 4;

    [Header("Custom Callbacks")]
    public UnityEvent onClicked = new UnityEvent();

    private void Awake()
    {
        if (onClicked == null)
        {
            onClicked = new UnityEvent();
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (graphicTarget == null)
        {
            graphicTarget = GetComponent<UnityEngine.UI.Graphic>();
        }

        // Auto-add transparent Image if neither Graphic nor Collider2D is attached
        if (graphicTarget == null && GetComponent<Collider2D>() == null)
        {
            var img = gameObject.AddComponent<UnityEngine.UI.Image>();
            img.color = new Color(1f, 1f, 1f, enableHoverAlpha ? normalAlpha : 0f);
            img.raycastTarget = true;
            graphicTarget = img;
        }
        else if (graphicTarget != null)
        {
            graphicTarget.raycastTarget = true;
        }

        // Hook into UI Button if present
        var btn = GetComponent<UnityEngine.UI.Button>();
        if (btn != null)
        {
            btn.onClick.RemoveListener(TriggerClick);
            btn.onClick.AddListener(TriggerClick);
        }

        // Ensure an EventSystem exists in the scene so UI raycasting functions properly
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // Ensure all parent Canvases have a GraphicRaycaster and reference camera assigned
        Canvas[] parentCanvases = GetComponentsInParent<Canvas>(true);
        foreach (var c in parentCanvases)
        {
            if (c.GetComponent<GraphicRaycaster>() == null)
            {
                c.gameObject.AddComponent<GraphicRaycaster>();
            }
            if (c.renderMode != RenderMode.ScreenSpaceOverlay && c.worldCamera == null)
            {
                c.worldCamera = Camera.main;
            }
        }

        ApplyRoundedBorderStyle();
    }

    private void OnEnable()
    {
        ApplyAlpha(normalAlpha);
    }

    private void ApplyAlpha(float alpha)
    {
        if (enablePulse) return;
        if (enableHoverAlpha && graphicTarget != null)
        {
            Color c = graphicTarget.color;
            c.a = alpha;
            graphicTarget.color = c;
        }
    }

    public void SetInteractable(bool interactable)
    {
        isInteractable = interactable;
        if (!interactable && enablePulse)
        {
            ClearVisualCue();
        }
    }

    private float lastClickTime = -1f;
    private bool wasHovered = false;
    private bool isPointerInside = false;

    public bool IsPointerOverHotspot()
    {
        if (!gameObject.activeInHierarchy || !enabled || !isInteractable) return false;
        RectTransform rt = GetComponent<RectTransform>();
        if (rt == null) return false;

        // 1. Direct EventSystem hover tracking
        if (isPointerInside) return true;

        // 2. Camera-aware screen point check
        Canvas c = GetComponentInParent<Canvas>();
        Camera cam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) 
            ? (c.worldCamera != null ? c.worldCamera : Camera.main) 
            : null;

        if (RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, cam))
        {
            return true;
        }

        // 3. Fallback without camera (overlay calculation)
        if (RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null))
        {
            return true;
        }

        // 4. Check all 4 world corners projected to screen points
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Camera screenCam = (cam != null) ? cam : Camera.main;

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < 4; i++)
        {
            Vector3 sp = (screenCam != null) ? screenCam.WorldToScreenPoint(corners[i]) : corners[i];
            if (sp.x < minX) minX = sp.x;
            if (sp.x > maxX) maxX = sp.x;
            if (sp.y < minY) minY = sp.y;
            if (sp.y > maxY) maxY = sp.y;
        }

        Rect screenRect = new Rect(minX, minY, Mathf.Max(1f, maxX - minX), Mathf.Max(1f, maxY - minY));
        if (screenRect.Contains(Input.mousePosition))
        {
            return true;
        }

        return false;
    }

    private void Update()
    {
        if (PauseMenuManager.IsPaused)
        {
            if (wasHovered)
            {
                wasHovered = false;
                IsAnyHotspotHovered = false;
                if (!enablePulse) ApplyAlpha(normalAlpha);
                if (CustomCursor.Instance != null) CustomCursor.Instance.SetHover(false);
            }
            return;
        }

        // Direct hover tracking (works regardless of EventSystem raycaster state)
        bool isHoveredNow = isInteractable && IsPointerOverHotspot();
        if (isHoveredNow != wasHovered)
        {
            wasHovered = isHoveredNow;
            if (isHoveredNow)
            {
                IsAnyHotspotHovered = true;
                if (!enablePulse) ApplyAlpha(hoverAlpha);
                if (CustomCursor.Instance != null) CustomCursor.Instance.SetHover(true, hoverCursorSprite);
            }
            else
            {
                IsAnyHotspotHovered = false;
                if (!enablePulse) ApplyAlpha(normalAlpha);
                if (CustomCursor.Instance != null) CustomCursor.Instance.SetHover(false);
            }
        }

        // Pulsing navigation visual cue animation
        if (enablePulse && isInteractable)
        {
            float t = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
            float currentAlpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, t);
            if (isHoveredNow)
            {
                currentAlpha = Mathf.Max(currentAlpha, hoverAlpha);
            }

            if (graphicTarget != null)
            {
                Color c = pulseColor;
                c.a = currentAlpha;
                graphicTarget.color = c;
            }
            if (cueText != null)
            {
                Color tc = pulseColor;
                tc.a = Mathf.Lerp(0.5f, 1f, t);
                cueText.color = tc;
            }
        }

        if (!isInteractable) return;
        if (LocationManager.Instance != null)
        {
            if (LocationManager.Instance.IsTransitioning) return;
            if (LocationManager.Instance.LastTransitionTime > 0f && (Time.unscaledTime - LocationManager.Instance.LastTransitionTime < 0.25f)) return;
        }

        // Direct click detection: fires on click if pointer is over hotspot
        if (Input.GetMouseButtonDown(0))
        {
            if (isHoveredNow || IsPointerOverHotspot())
            {
                TriggerClick();
            }
        }
    }

    public void TriggerClick()
    {
        if (!isInteractable || PauseMenuManager.IsPaused) return;
        if (Time.unscaledTime - lastClickTime < 0.25f) return;
        lastClickTime = Time.unscaledTime;

        if (LocationManager.Instance != null)
        {
            if (LocationManager.Instance.IsTransitioning) return;
            if (LocationManager.Instance.LastTransitionTime > 0f && (Time.unscaledTime - LocationManager.Instance.LastTransitionTime < 0.25f)) return;
        }

        string currentLoc = LocationManager.Instance != null ? LocationManager.Instance.CurrentLocationId : "Office";

        // Auto-resolve blank targetLocationId based on GameObject name
        if (string.IsNullOrEmpty(targetLocationId))
        {
            string n = gameObject.name.ToLowerInvariant();
            if (n.Contains("breaker") && !n.Contains("closeup"))
            {
                targetLocationId = currentLoc.Equals("BreakerRoom", System.StringComparison.OrdinalIgnoreCase) ? "BreakerCloseUp" : "BreakerRoom";
            }
            else if (n.Contains("closeup"))
            {
                targetLocationId = "BreakerCloseUp";
            }
            else if (n.Contains("office"))
            {
                targetLocationId = "Office";
            }
        }

        // If in Office, NEVER allow navigation to jump directly to Close-Up!
        if (currentLoc.Equals("Office", System.StringComparison.OrdinalIgnoreCase))
        {
            if (targetLocationId.Equals("BreakerCloseUp", System.StringComparison.OrdinalIgnoreCase) ||
                targetLocationId.IndexOf("closeup", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                targetLocationId.IndexOf("off", System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                targetLocationId = "BreakerRoom";
            }
        }

        Debug.Log($"[ClickableHotspot] TriggerClick on '{gameObject.name}' -> TargetLocation: '{targetLocationId}', Resolves: '{resolvesEventId}'");

        // 1. Play SFX
        if (clickSound != null)
        {
            if (audioSource != null)
            {
                audioSource.PlayOneShot(clickSound);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clickSound, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
            }
        }

        // 2. Resolve Environmental Event if specified
        if (!string.IsNullOrEmpty(resolvesEventId) && EnvironmentalEventManager.Instance != null)
        {
            EnvironmentalEventManager.Instance.ResolveEvent(resolvesEventId);
        }

        // 3. Custom Callbacks
        onClicked?.Invoke();

        // 4. Change Location if specified
        if (!string.IsNullOrEmpty(targetLocationId) && LocationManager.Instance != null)
        {
            LocationManager.Instance.TransitionToLocation(targetLocationId);
        }
    }

    // --- UI Pointer Handlers ---
    public void OnPointerClick(PointerEventData eventData)
    {
        TriggerClick();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractable) return;
        isPointerInside = true;
        IsAnyHotspotHovered = true;
        if (!enablePulse) ApplyAlpha(hoverAlpha);
        if (CustomCursor.Instance != null)
        {
            CustomCursor.Instance.SetHover(true, hoverCursorSprite);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        IsAnyHotspotHovered = false;
        if (!enablePulse) ApplyAlpha(normalAlpha);
        if (CustomCursor.Instance != null)
        {
            CustomCursor.Instance.SetHover(false);
        }
    }

    private void OnDisable()
    {
        isPointerInside = false;
        IsAnyHotspotHovered = false;
    }

    // --- 2D World Collider Handlers (if attached to 2D GameObject with Collider2D) ---
    private void OnMouseDown()
    {
        if (isInteractable)
        {
            TriggerClick();
        }
    }

    private void OnMouseEnter()
    {
        if (isInteractable)
        {
            isPointerInside = true;
            IsAnyHotspotHovered = true;
            if (!enablePulse) ApplyAlpha(hoverAlpha);
            if (CustomCursor.Instance != null)
            {
                CustomCursor.Instance.SetHover(true, hoverCursorSprite);
            }
        }
    }

    private void OnMouseExit()
    {
        isPointerInside = false;
        IsAnyHotspotHovered = false;
        if (!enablePulse) ApplyAlpha(normalAlpha);
        if (CustomCursor.Instance != null)
        {
            CustomCursor.Instance.SetHover(false);
        }
    }

    /// <summary>
    /// Configures a dynamic, visible pulsing cue and bold label on this hotspot (for blackouts and room navigation).
    /// </summary>
    public void SetupVisualCue(string label, Color color, bool pulse = true, float minAlpha = 0.14f, float maxAlpha = 0.40f)
    {
        visualCueLabel = label;
        pulseColor = color;
        enablePulse = pulse;
        pulseMinAlpha = minAlpha;
        pulseMaxAlpha = maxAlpha;

        if (graphicTarget == null)
        {
            graphicTarget = GetComponent<UnityEngine.UI.Graphic>();
            if (graphicTarget == null)
            {
                var img = gameObject.AddComponent<UnityEngine.UI.Image>();
                img.color = new Color(color.r, color.g, color.b, minAlpha);
                img.raycastTarget = true;
                graphicTarget = img;
            }
        }

        if (graphicTarget != null)
        {
            graphicTarget.raycastTarget = true;
            Color c = color;
            c.a = minAlpha;
            graphicTarget.color = c;
        }

        if (cueText == null)
        {
            Transform existing = transform.Find("VisualCueText");
            if (existing != null)
            {
                cueText = existing.GetComponent<TMP_Text>();
            }
        }

        if (cueText == null && !string.IsNullOrEmpty(label))
        {
            GameObject textObj = new GameObject("VisualCueText");
            textObj.transform.SetParent(transform, false);
            cueText = textObj.AddComponent<TextMeshProUGUI>();
            RectTransform rt = textObj.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        // Apply rounded corners and crisp border
        ApplyRoundedBorderStyle();

        if (cueText != null)
        {
            if (customFont != null)
            {
                cueText.font = customFont;
            }
            else
            {
                // Fallback to scene retro TMP font if available (e.g. HomeVideo SDF)
                TMP_Text[] sceneTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
                foreach (var st in sceneTexts)
                {
                    if (st != null && st != cueText && st.font != null)
                    {
                        cueText.font = st.font;
                        break;
                    }
                }
            }

            cueText.text = label;
            cueText.alignment = TextAlignmentOptions.Center;
            cueText.fontSize = cueFontSize;
            cueText.fontStyle = FontStyles.Bold;
            cueText.color = color;
            cueText.raycastTarget = false;
            cueText.textWrappingMode = TextWrappingModes.Normal;
            cueText.gameObject.SetActive(!string.IsNullOrEmpty(label));
        }
    }

    private Coroutine fadeInCoroutine;

    /// <summary>
    /// Smoothly fades this hotspot into existence via an attached CanvasGroup.
    /// </summary>
    public void FadeIn(float duration = 1.2f)
    {
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        if (fadeInCoroutine != null)
        {
            StopCoroutine(fadeInCoroutine);
        }
        fadeInCoroutine = StartCoroutine(FadeInRoutine(duration));
    }

    private IEnumerator FadeInRoutine(float duration)
    {
        CanvasGroup cg = GetComponent<CanvasGroup>();
        if (cg == null)
        {
            cg = gameObject.AddComponent<CanvasGroup>();
        }
        cg.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (this == null || cg == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        if (cg != null) cg.alpha = 1f;
        fadeInCoroutine = null;
    }

    private static Sprite cachedRoundedBorderSprite;

    /// <summary>
    /// Generates a clean 9-sliced rounded rectangle sprite with a crisp border and translucent interior.
    /// Scales seamlessly to any RectTransform size without corner distortion.
    /// </summary>
    public static Sprite GetOrCreateRoundedBorderSprite(int size = 128, int radius = 24, int thickness = 5)
    {
        if (cachedRoundedBorderSprite != null) return cachedRoundedBorderSprite;

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = "Procedural_RoundedBorder";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] colors = new Color[size * size];
        float r = radius;
        float b = thickness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                float cx = Mathf.Clamp(px, r, size - r);
                float cy = Mathf.Clamp(py, r, size - r);
                float dist = Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy));
                float dEdge = r - dist;

                if (dEdge < -0.5f)
                {
                    colors[y * size + x] = Color.clear;
                }
                else if (dEdge >= -0.5f && dEdge < 0.5f)
                {
                    float a = Mathf.Clamp01(dEdge + 0.5f);
                    colors[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                else if (dEdge >= 0.5f && dEdge < b - 0.5f)
                {
                    colors[y * size + x] = new Color(1f, 1f, 1f, 1f);
                }
                else if (dEdge >= b - 0.5f && dEdge < b + 0.5f)
                {
                    float t = Mathf.Clamp01(dEdge - (b - 0.5f));
                    colors[y * size + x] = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0.35f, t));
                }
                else
                {
                    colors[y * size + x] = new Color(1f, 1f, 1f, 0.35f);
                }
            }
        }

        tex.SetPixels(colors);
        tex.Apply();

        Vector4 borderPadding = new Vector4(r, r, r, r);
        cachedRoundedBorderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, borderPadding);
        return cachedRoundedBorderSprite;
    }

    /// <summary>
    /// Applies the 9-sliced rounded border sprite to this hotspot's graphicTarget.
    /// </summary>
    public void ApplyRoundedBorderStyle()
    {
        if (!useRoundedBorder) return;

        if (graphicTarget == null)
        {
            graphicTarget = GetComponent<UnityEngine.UI.Graphic>();
        }

        if (graphicTarget == null)
        {
            var img = gameObject.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = true;
            graphicTarget = img;
        }

        UnityEngine.UI.Image targetImage = graphicTarget as UnityEngine.UI.Image;
        if (targetImage != null)
        {
            targetImage.sprite = GetOrCreateRoundedBorderSprite(128, cornerRadius, borderThickness);
            targetImage.type = UnityEngine.UI.Image.Type.Sliced;
        }
    }

    /// <summary>
    /// Disables pulsing and hides any visual cue text.
    /// </summary>
    public void ClearVisualCue()
    {
        enablePulse = false;
        if (cueText != null)
        {
            cueText.gameObject.SetActive(false);
        }
        ApplyAlpha(normalAlpha);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (cueText != null)
        {
            if (customFont != null) cueText.font = customFont;
            cueText.fontSize = cueFontSize;
        }
        ApplyRoundedBorderStyle();
    }
#endif
}
