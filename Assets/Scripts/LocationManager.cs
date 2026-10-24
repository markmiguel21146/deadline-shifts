using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages fixed 2D location views and handles smooth fade-to-black transitions between rooms.
/// Example: Office -> Fade to Black -> Breaker Room -> Restore Power -> Fade to Black -> Office.
/// </summary>
public class LocationManager : MonoBehaviour
{
    private static LocationManager instance;
    public static LocationManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<LocationManager>(FindObjectsInactive.Include);
                if (instance == null)
                {
                    GameObject go = new GameObject("LocationManager");
                    instance = go.AddComponent<LocationManager>();
                }
            }
            return instance;
        }
        private set => instance = value;
    }

    [Header("Transition Settings")]
    [Tooltip("CanvasGroup used for full-screen fade to black. Auto-created if left unassigned.")]
    [SerializeField] private CanvasGroup transitionFadeCanvasGroup;
    [SerializeField] private float defaultFadeDuration = 0.2f; // Quick, snappy 0.2s transition
    [SerializeField] private float blackHoldDuration = 0.05f;

    [Header("Audio (Optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip transitionSound;

    [Header("Dependencies")]
    [SerializeField] private TypingSystem typingSystem;

    private readonly Dictionary<string, LocationView> registeredLocations = new Dictionary<string, LocationView>(StringComparer.OrdinalIgnoreCase);
    public string CurrentLocationId { get; private set; } = "Office";
    public bool IsTransitioning { get; private set; } = false;
    public float LastTransitionTime { get; private set; } = -1f;

    public event Action<string> OnLocationChanged;

    private Image officeBehindSlackerFill;
    private TMPro.TextMeshProUGUI officeBehindSlackerLabel;

    private GameObject officeDeskMeterObj;
    private Image officeDeskSlackerFill;
    private TMPro.TextMeshProUGUI officeDeskSlackerLabel;

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

        if (typingSystem == null)
        {
            typingSystem = FindAnyObjectByType<TypingSystem>(FindObjectsInactive.Include);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        EnsureFadeCanvasExists();
        DiscoverAllLocations();
        EnsurePanelOpacityAndSprites();
    }

    private void Start()
    {
        EnsurePanelOpacityAndSprites();

        // Enforce starting at the Office desk view
        SwitchViews("Office");
    }

    public void RegisterLocation(LocationView view)
    {
        if (view == null || string.IsNullOrEmpty(view.locationId)) return;

        if (!registeredLocations.ContainsKey(view.locationId))
        {
            registeredLocations.Add(view.locationId, view);
        }
    }

    private void DiscoverAllLocations()
    {
        LocationView[] views = FindObjectsByType<LocationView>(FindObjectsInactive.Include);
        foreach (var view in views)
        {
            RegisterLocation(view);
        }

        EnsurePanelOpacityAndSprites();
    }

    public void EnsurePanelOpacityAndSprites()
    {
        // 1. Resolve and fix BreakerRoomPanel (Guarantees Breaker Room art is 100% opaque, not transparent!)
        GameObject roomObj = GameObject.Find("BreakerRoomPanel");
        if (roomObj == null && registeredLocations.TryGetValue("BreakerRoom", out LocationView rv) && rv != null)
        {
            roomObj = rv.gameObject;
        }

        if (roomObj != null)
        {
            LocationView lv = roomObj.GetComponent<LocationView>();
            if (lv == null) lv = roomObj.AddComponent<LocationView>();
            lv.locationId = "BreakerRoom";
            RegisterLocation(lv);

            Image img = roomObj.GetComponent<Image>();
            if (img == null) img = roomObj.AddComponent<Image>();
            
            // Fix transparency: Ensure full opacity and pure white tint
            img.color = Color.white;

            // Auto-assign Breaker Room sprite depending on power status
            BreakerEvent breaker = FindAnyObjectByType<BreakerEvent>(FindObjectsInactive.Include);
            bool isPowerRestored = breaker == null || breaker.IsPowerRestored;

            if (isPowerRestored)
            {
                Sprite onSprite = (breaker != null && breaker.breakerRoomOnSprite != null) ?
                    breaker.breakerRoomOnSprite :
                    FindSpecificSprite("breaker room on");
                if (onSprite != null)
                {
                    img.sprite = onSprite;
                }
            }
            else
            {
                Sprite offSprite = (breaker != null && breaker.breakerRoomOffSprite != null) ?
                    breaker.breakerRoomOffSprite :
                    FindSpecificSprite("breaker room", "on");
                if (offSprite != null)
                {
                    img.sprite = offSprite;
                }
            }

            // Ensure RectTransform stretches across the entire screen
            RectTransform rt = roomObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            CanvasGroup cg = roomObj.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.9f)
            {
                cg.alpha = 1f;
            }
        }

        // 2. Resolve and fix BreakerCloseUpPanel
        GameObject closeUpObj = GameObject.Find("BreakerCloseUpPanel");
        if (closeUpObj == null && registeredLocations.TryGetValue("BreakerCloseUp", out LocationView cv) && cv != null)
        {
            closeUpObj = cv.gameObject;
        }

        if (closeUpObj != null)
        {
            LocationView lv = closeUpObj.GetComponent<LocationView>();
            if (lv == null) lv = closeUpObj.AddComponent<LocationView>();
            lv.locationId = "BreakerCloseUp";
            RegisterLocation(lv);

            Image img = closeUpObj.GetComponent<Image>();
            if (img == null) img = closeUpObj.AddComponent<Image>();
            img.color = Color.white;

            BreakerEvent breaker = FindAnyObjectByType<BreakerEvent>(FindObjectsInactive.Include);
            bool isPowerRestored = breaker == null || breaker.IsPowerRestored;

            if (isPowerRestored)
            {
                Sprite onSprite = (breaker != null && breaker.breakerOnSprite != null) ?
                    breaker.breakerOnSprite :
                    FindSpecificSprite("breaker on", "room");
                if (onSprite != null)
                {
                    img.sprite = onSprite;
                }
            }
            else
            {
                Sprite offSprite = (breaker != null && breaker.breakerOffSprite != null) ?
                    breaker.breakerOffSprite :
                    FindSpecificSprite("breaker off", "room");
                if (offSprite != null)
                {
                    img.sprite = offSprite;
                }
            }

            RectTransform rt = closeUpObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            CanvasGroup cg = closeUpObj.GetComponent<CanvasGroup>();
            if (cg != null && cg.alpha < 0.9f)
            {
                cg.alpha = 1f;
            }
        }

        // 3. Resolve and fix OfficeBehind / LookBehindView (backroom part of the office)
        Canvas targetCanvas = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var c in canvases)
        {
            if (c.gameObject.name.Contains("Workplace") || c.gameObject.name.Contains("HUD"))
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null && canvases.Length > 0) targetCanvas = canvases[0];

        GameObject behindObj = GameObject.Find("OfficeBehindPanel");
        if (behindObj == null) behindObj = GameObject.Find("LookBehindView");
        if (behindObj == null) behindObj = GameObject.Find("OfficeBehind");

        if (behindObj == null && registeredLocations.TryGetValue("OfficeBehind", out LocationView registeredBehindLv) && registeredBehindLv != null)
        {
            behindObj = registeredBehindLv.gameObject;
        }

        if (behindObj == null)
        {
            Transform[] allSceneTransforms = Resources.FindObjectsOfTypeAll<Transform>();
            foreach (var t in allSceneTransforms)
            {
                if (t != null && (t.name == "OfficeBehindPanel" || t.name == "LookBehindView" || t.name == "OfficeBehind"))
                {
                    if (t.GetComponentInParent<Canvas>() != null || t.GetComponent<RectTransform>() != null)
                    {
                        behindObj = t.gameObject;
                        break;
                    }
                }
            }
        }

        if (behindObj != null)
        {
            behindObj.name = "OfficeBehindPanel";

            // Reparent to root targetCanvas so it is at the same tier as BreakerRoomPanel
            if (targetCanvas != null && behindObj.transform.parent != targetCanvas.transform)
            {
                behindObj.transform.SetParent(targetCanvas.transform, false);
            }

            // Remove rogue overrideSorting = 800 so it does not occlude the desk HUD
            Canvas lbCanvas = behindObj.GetComponent<Canvas>();
            if (lbCanvas != null)
            {
                lbCanvas.overrideSorting = false;
            }

            // Ensure CanvasGroup has full alpha
            CanvasGroup cg = behindObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = behindObj.AddComponent<CanvasGroup>();
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;

            if (behindObj.GetComponent<GraphicRaycaster>() == null)
            {
                behindObj.AddComponent<GraphicRaycaster>();
            }

            RectTransform rt = behindObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            // Ensure LocationView component
            LocationView lv = behindObj.GetComponent<LocationView>();
            if (lv == null)
            {
                lv = behindObj.AddComponent<LocationView>();
            }
            lv.locationId = "OfficeBehind";
            lv.isInitialLocation = false;
            lv.allowsTyping = false;
            RegisterLocation(lv);

            Image img = behindObj.GetComponent<Image>();
            if (img == null)
            {
                img = behindObj.GetComponentInChildren<Image>(true);
            }
            if (img == null)
            {
                img = behindObj.AddComponent<Image>();
            }

            if (img != null)
            {
                img.color = Color.white;
                img.preserveAspect = false;
                if (img.sprite == null)
                {
                    img.sprite = FindSpriteByNameKeyword("officebehind") ?? FindSpriteByNameKeyword("behind");
                }

                // If UILitMaterial exists, assign it so 2D flashlight illuminates the room in the dark
                Material[] allMats = Resources.FindObjectsOfTypeAll<Material>();
                foreach (var m in allMats)
                {
                    if (m != null && m.name.Equals("UILitMaterial", StringComparison.OrdinalIgnoreCase))
                    {
                        img.material = m;
                        break;
                    }
                }
            }

            // Ensure ReturnToDesk button
            EnsureOfficeBehindReturnButton(behindObj.transform);

            // Ensure Productivity Meter in OfficeBehind
            EnsureOfficeBehindProductivityMeter(behindObj.transform);

            // Attach interactive point-and-click hotspots for Night 2 (Cabinet) safely
            try
            {
                OfficeBehindInteractions interactions = behindObj.GetComponent<OfficeBehindInteractions>();
                if (interactions == null)
                {
                    interactions = behindObj.AddComponent<OfficeBehindInteractions>();
                }
                if (interactions != null)
                {
                    interactions.SetupInteractions();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[LocationManager] Non-critical warning setting up OfficeBehindInteractions: {ex.Message}");
            }

            // CRITICAL: Guarantee OfficeBehindPanel is INACTIVE unless player is explicitly in OfficeBehind!
            if (!CurrentLocationId.Equals("OfficeBehind", StringComparison.OrdinalIgnoreCase))
            {
                behindObj.SetActive(false);
            }
        }
    }

    private void EnsureOfficeBehindReturnButton(Transform parent)
    {
        Transform existing = parent.Find("ReturnToDeskHotspot");
        GameObject btnObj = (existing != null) ? existing.gameObject : new GameObject("ReturnToDeskHotspot");
        if (existing == null)
        {
            btnObj.transform.SetParent(parent, false);
        }

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        if (rt == null) rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 25f);
        rt.sizeDelta = new Vector2(360f, 44f); // Expanded width to fit text without overflow

        Image bg = btnObj.GetComponent<Image>();
        if (bg == null) bg = btnObj.AddComponent<Image>();
        bg.color = new Color(0.10f, 0.11f, 0.13f, 0.45f); // Translucent retro dark slate

        Outline outline = btnObj.GetComponent<Outline>();
        if (outline == null) outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.60f, 0.65f, 0.55f); // Clean retro steel outline
        outline.effectDistance = new Vector2(1f, 1f); // Balanced symmetrical border

        Transform textChild = btnObj.transform.Find("Text");
        GameObject textObj = (textChild != null) ? textChild.gameObject : new GameObject("Text");
        if (textChild == null) textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        if (textRt == null) textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TMPro.TextMeshProUGUI tmp = textObj.GetComponent<TMPro.TextMeshProUGUI>();
        if (tmp == null) tmp = textObj.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = "[ ▲ RETURN TO DESK (TAB) ]";
        tmp.fontSize = 14;
        tmp.characterSpacing = 1.0f;
        tmp.alignment = TMPro.TextAlignmentOptions.Center;
        tmp.color = new Color(0.85f, 0.95f, 0.88f, 1f); // Retro phosphor white-green
        tmp.fontStyle = TMPro.FontStyles.Bold;
        tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
        tmp.overflowMode = TMPro.TextOverflowModes.Ellipsis;

        TMPro.TMP_Text existingText = FindAnyObjectByType<TMPro.TMP_Text>(FindObjectsInactive.Include);
        if (existingText != null && existingText.font != null) tmp.font = existingText.font;

        Button btn = btnObj.GetComponent<Button>();
        if (btn == null) btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.22f, 0.25f, 0.28f, 0.70f);
        cb.pressedColor = new Color(0.08f, 0.09f, 0.10f, 0.85f);
        btn.colors = cb;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            TransitionToLocation("Office");
        });
    }

    private void EnsureOfficeBehindProductivityMeter(Transform parent)
    {
        Transform existing = parent.Find("OfficeBehindProductivityMeter");
        GameObject meterObj = (existing != null) ? existing.gameObject : new GameObject("OfficeBehindProductivityMeter");
        if (existing == null)
        {
            meterObj.transform.SetParent(parent, false);
        }

        RectTransform rt = meterObj.GetComponent<RectTransform>();
        if (rt == null) rt = meterObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-30f, -25f);
        rt.sizeDelta = new Vector2(330f, 68f);

        Image bg = meterObj.GetComponent<Image>();
        if (bg == null) bg = meterObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.11f, 0.92f);

        Outline outline = meterObj.GetComponent<Outline>();
        if (outline == null) outline = meterObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.50f, 0.54f, 0.58f, 0.80f);
        outline.effectDistance = new Vector2(1f, 1f);

        // Header Label
        Transform labelChild = meterObj.transform.Find("Label");
        GameObject labelObj = (labelChild != null) ? labelChild.gameObject : new GameObject("Label");
        if (labelChild == null) labelObj.transform.SetParent(meterObj.transform, false);

        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        if (labelRt == null) labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0f, -8f);
        labelRt.sizeDelta = new Vector2(-20f, 20f);

        officeBehindSlackerLabel = labelObj.GetComponent<TMPro.TextMeshProUGUI>();
        if (officeBehindSlackerLabel == null) officeBehindSlackerLabel = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        officeBehindSlackerLabel.fontSize = 15;
        officeBehindSlackerLabel.characterSpacing = 1.0f;
        officeBehindSlackerLabel.fontStyle = TMPro.FontStyles.Bold;
        officeBehindSlackerLabel.alignment = TMPro.TextAlignmentOptions.Left;
        officeBehindSlackerLabel.color = new Color(0.75f, 0.80f, 0.85f, 1f);
        officeBehindSlackerLabel.text = "PRODUCTIVITY RISK: 0%";

        TMPro.TMP_Text fontSample = FindAnyObjectByType<TMPro.TMP_Text>(FindObjectsInactive.Include);
        if (fontSample != null && fontSample.font != null) officeBehindSlackerLabel.font = fontSample.font;

        // Progress Bar Slot
        Transform slotChild = meterObj.transform.Find("Slot");
        GameObject slotObj = (slotChild != null) ? slotChild.gameObject : new GameObject("Slot");
        if (slotChild == null) slotObj.transform.SetParent(meterObj.transform, false);

        RectTransform slotRt = slotObj.GetComponent<RectTransform>();
        if (slotRt == null) slotRt = slotObj.AddComponent<RectTransform>();
        slotRt.anchorMin = new Vector2(0f, 0f);
        slotRt.anchorMax = new Vector2(1f, 0f);
        slotRt.pivot = new Vector2(0.5f, 0f);
        slotRt.anchoredPosition = new Vector2(0f, 10f);
        slotRt.sizeDelta = new Vector2(-20f, 18f);

        Image slotBg = slotObj.GetComponent<Image>();
        if (slotBg == null) slotBg = slotObj.AddComponent<Image>();
        slotBg.color = new Color(0.03f, 0.04f, 0.05f, 0.95f);

        // Progress Bar Fill
        Transform fillChild = slotObj.transform.Find("Fill");
        GameObject fillObj = (fillChild != null) ? fillChild.gameObject : new GameObject("Fill");
        if (fillChild == null) fillObj.transform.SetParent(slotObj.transform, false);

        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        if (fillRt == null) fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(2f, -2f);

        officeBehindSlackerFill = fillObj.GetComponent<Image>();
        if (officeBehindSlackerFill == null) officeBehindSlackerFill = fillObj.AddComponent<Image>();
        officeBehindSlackerFill.color = new Color(0.3f, 0.85f, 0.45f, 0.95f);
    }

    public void UpdateOfficeBehindProductivityMeter(float slackerMeter)
    {
        if (officeBehindSlackerLabel == null || officeBehindSlackerFill == null) return;

        int percent = Mathf.RoundToInt(Mathf.Clamp(slackerMeter, 0f, 100f));
        float normalized = percent / 100f;

        RectTransform fillRt = officeBehindSlackerFill.rectTransform;
        RectTransform slotRt = fillRt.parent as RectTransform;
        if (slotRt != null)
        {
            float maxW = slotRt.rect.width - 4f;
            fillRt.sizeDelta = new Vector2(maxW * normalized, 0f);
        }

        // Color gradient from green to amber to flashing red
        if (percent < 40)
        {
            officeBehindSlackerFill.color = new Color(0.3f, 0.85f, 0.45f, 0.95f);
            officeBehindSlackerLabel.text = $"PRODUCTIVITY RISK: {percent}% [STABLE]";
            officeBehindSlackerLabel.color = new Color(0.75f, 0.82f, 0.88f, 1f);
        }
        else if (percent < 75)
        {
            officeBehindSlackerFill.color = new Color(0.95f, 0.75f, 0.2f, 0.95f);
            officeBehindSlackerLabel.text = $"<color=#FFCC00>IDLE WARNING: {percent}% [EVALUATING]</color>";
            officeBehindSlackerLabel.color = new Color(1f, 0.85f, 0.3f, 1f);
        }
        else
        {
            float flash = (Mathf.Sin(Time.unscaledTime * 12f) + 1f) * 0.5f;
            officeBehindSlackerFill.color = Color.Lerp(new Color(0.9f, 0.15f, 0.15f, 0.95f), new Color(1f, 0.4f, 0.4f, 1f), flash);
            officeBehindSlackerLabel.text = $"<color=#FF3333>IDLE CRITICAL: {percent}% [TERMINATION RISK]</color>";
            officeBehindSlackerLabel.color = new Color(1f, 0.35f, 0.35f, 1f);
        }
    }

    public void EnsureOfficeDeskProductivityMeter()
    {
        // Find WorkplaceCanvas or fullscreen root Canvas to host the desk productivity meter in true top-right
        GameObject hud = GameObject.Find("1_GameplayHUD") ?? GameObject.Find("GameplayHUD");
        Canvas targetCanvas = null;
        if (hud != null)
        {
            targetCanvas = hud.GetComponentInParent<Canvas>();
        }
        if (targetCanvas == null)
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                if (c.gameObject.name.Contains("Workplace") || c.gameObject.name.Contains("HUD"))
                {
                    targetCanvas = c;
                    break;
                }
            }
            if (targetCanvas == null && canvases.Length > 0) targetCanvas = canvases[0];
        }

        Transform parent = targetCanvas != null ? targetCanvas.transform : (hud != null ? hud.transform : transform);

        if (officeDeskMeterObj != null)
        {
            if (officeDeskMeterObj.transform.parent != parent)
            {
                officeDeskMeterObj.transform.SetParent(parent, false);
            }
            RectTransform existingRt = officeDeskMeterObj.GetComponent<RectTransform>();
            if (existingRt != null)
            {
                existingRt.anchorMin = new Vector2(1f, 1f);
                existingRt.anchorMax = new Vector2(1f, 1f);
                existingRt.pivot = new Vector2(1f, 1f);
                existingRt.anchoredPosition = new Vector2(-30f, -25f);
                existingRt.sizeDelta = new Vector2(290f, 68f);
            }
            return;
        }

        // Permanently deactivate old BossMonitorGroup / WarningText / CAM 04 elements so only the styled card meter is used
        GameObject bossGroup = GameObject.Find("BossMonitorGroup");
        if (bossGroup != null)
        {
            bossGroup.SetActive(false);
        }
        GameObject warningText = GameObject.Find("WarningText");
        if (warningText != null)
        {
            warningText.SetActive(false);
        }
        GameObject slackerBar = GameObject.Find("SlackerBar");
        if (slackerBar != null)
        {
            slackerBar.SetActive(false);
        }

        Transform existing = parent.Find("OfficeDeskProductivityMeter");
        officeDeskMeterObj = (existing != null) ? existing.gameObject : new GameObject("OfficeDeskProductivityMeter");
        if (existing == null)
        {
            officeDeskMeterObj.transform.SetParent(parent, false);
        }
        else if (officeDeskMeterObj.transform.parent != parent)
        {
            officeDeskMeterObj.transform.SetParent(parent, false);
        }

        RectTransform rt = officeDeskMeterObj.GetComponent<RectTransform>();
        if (rt == null) rt = officeDeskMeterObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-30f, -25f);
        rt.sizeDelta = new Vector2(330f, 68f);

        Image bg = officeDeskMeterObj.GetComponent<Image>();
        if (bg == null) bg = officeDeskMeterObj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.09f, 0.11f, 0.92f);

        Outline outline = officeDeskMeterObj.GetComponent<Outline>();
        if (outline == null) outline = officeDeskMeterObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.50f, 0.54f, 0.58f, 0.80f);
        outline.effectDistance = new Vector2(1f, 1f);

        // Header Label
        Transform labelChild = officeDeskMeterObj.transform.Find("Label");
        GameObject labelObj = (labelChild != null) ? labelChild.gameObject : new GameObject("Label");
        if (labelChild == null) labelObj.transform.SetParent(officeDeskMeterObj.transform, false);

        RectTransform labelRt = labelObj.GetComponent<RectTransform>();
        if (labelRt == null) labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, 1f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0.5f, 1f);
        labelRt.anchoredPosition = new Vector2(0f, -8f);
        labelRt.sizeDelta = new Vector2(-20f, 20f);

        officeDeskSlackerLabel = labelObj.GetComponent<TMPro.TextMeshProUGUI>();
        if (officeDeskSlackerLabel == null) officeDeskSlackerLabel = labelObj.AddComponent<TMPro.TextMeshProUGUI>();
        officeDeskSlackerLabel.fontSize = 15;
        officeDeskSlackerLabel.characterSpacing = 1.0f;
        officeDeskSlackerLabel.fontStyle = TMPro.FontStyles.Bold;
        officeDeskSlackerLabel.alignment = TMPro.TextAlignmentOptions.Left;
        officeDeskSlackerLabel.color = new Color(0.75f, 0.80f, 0.85f, 1f);
        officeDeskSlackerLabel.text = "PRODUCTIVITY RISK: 0%";

        TMPro.TMP_Text fontSample = FindAnyObjectByType<TMPro.TMP_Text>(FindObjectsInactive.Include);
        if (fontSample != null && fontSample.font != null) officeDeskSlackerLabel.font = fontSample.font;

        // Progress Bar Slot
        Transform slotChild = officeDeskMeterObj.transform.Find("Slot");
        GameObject slotObj = (slotChild != null) ? slotChild.gameObject : new GameObject("Slot");
        if (slotChild == null) slotObj.transform.SetParent(officeDeskMeterObj.transform, false);

        RectTransform slotRt = slotObj.GetComponent<RectTransform>();
        if (slotRt == null) slotRt = slotObj.AddComponent<RectTransform>();
        slotRt.anchorMin = new Vector2(0f, 0f);
        slotRt.anchorMax = new Vector2(1f, 0f);
        slotRt.pivot = new Vector2(0.5f, 0f);
        slotRt.anchoredPosition = new Vector2(0f, 10f);
        slotRt.sizeDelta = new Vector2(-20f, 18f);

        Image slotBg = slotObj.GetComponent<Image>();
        if (slotBg == null) slotBg = slotObj.AddComponent<Image>();
        slotBg.color = new Color(0.03f, 0.04f, 0.05f, 0.95f);

        // Progress Bar Fill
        Transform fillChild = slotObj.transform.Find("Fill");
        GameObject fillObj = (fillChild != null) ? fillChild.gameObject : new GameObject("Fill");
        if (fillChild == null) fillObj.transform.SetParent(slotObj.transform, false);

        RectTransform fillRt = fillObj.GetComponent<RectTransform>();
        if (fillRt == null) fillRt = fillObj.AddComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = new Vector2(0f, 1f);
        fillRt.pivot = new Vector2(0f, 0.5f);
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(2f, -2f);

        officeDeskSlackerFill = fillObj.GetComponent<Image>();
        if (officeDeskSlackerFill == null) officeDeskSlackerFill = fillObj.AddComponent<Image>();
        officeDeskSlackerFill.color = new Color(0.3f, 0.85f, 0.45f, 0.95f);

        officeDeskMeterObj.transform.SetAsLastSibling();
    }

    public void UpdateOfficeDeskProductivityMeter(float slackerMeter)
    {
        if (officeDeskSlackerLabel == null || officeDeskSlackerFill == null)
        {
            EnsureOfficeDeskProductivityMeter();
            if (officeDeskSlackerLabel == null || officeDeskSlackerFill == null) return;
        }

        bool atDesk = CurrentLocationId.Equals("Office", StringComparison.OrdinalIgnoreCase);
        if (officeDeskMeterObj != null && officeDeskMeterObj.activeSelf != atDesk)
        {
            officeDeskMeterObj.SetActive(atDesk);
        }

        if (!atDesk) return;

        int percent = Mathf.RoundToInt(Mathf.Clamp(slackerMeter, 0f, 100f));
        float normalized = percent / 100f;

        RectTransform fillRt = officeDeskSlackerFill.rectTransform;
        RectTransform slotRt = fillRt.parent as RectTransform;
        if (slotRt != null)
        {
            float maxW = slotRt.rect.width - 4f;
            fillRt.sizeDelta = new Vector2(maxW * normalized, 0f);
        }

        if (percent < 40)
        {
            officeDeskSlackerFill.color = new Color(0.3f, 0.85f, 0.45f, 0.95f);
            officeDeskSlackerLabel.text = $"PRODUCTIVITY RISK: {percent}% [STABLE]";
            officeDeskSlackerLabel.color = new Color(0.75f, 0.82f, 0.88f, 1f);
        }
        else if (percent < 75)
        {
            officeDeskSlackerFill.color = new Color(0.95f, 0.75f, 0.2f, 0.95f);
            officeDeskSlackerLabel.text = $"<color=#FFCC00>IDLE WARNING: {percent}% [EVALUATING]</color>";
            officeDeskSlackerLabel.color = new Color(1f, 0.85f, 0.3f, 1f);
        }
        else
        {
            float flash = (Mathf.Sin(Time.unscaledTime * 12f) + 1f) * 0.5f;
            officeDeskSlackerFill.color = Color.Lerp(new Color(0.9f, 0.15f, 0.15f, 0.95f), new Color(1f, 0.4f, 0.4f, 1f), flash);
            officeDeskSlackerLabel.text = $"<color=#FF3333>IDLE CRITICAL: {percent}% [TERMINATION RISK]</color>";
            officeDeskSlackerLabel.color = new Color(1f, 0.35f, 0.35f, 1f);
        }
    }

    public void HideAllHotspots()
    {
        if (officeDeskMeterObj != null) officeDeskMeterObj.SetActive(false);
        ClickableHotspot[] hotspots = FindObjectsByType<ClickableHotspot>(FindObjectsInactive.Include);
        foreach (var h in hotspots)
        {
            if (h != null) h.gameObject.SetActive(false);
        }
    }

    private Sprite FindSpecificSprite(string requiredKeywords, string excludeKeyword = null)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        string[] reqs = requiredKeywords.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var s in sprites)
        {
            if (s == null) continue;
            string n = s.name.ToLowerInvariant();
            bool match = true;
            foreach (var r in reqs)
            {
                if (!n.Contains(r)) { match = false; break; }
            }
            if (match && !string.IsNullOrEmpty(excludeKeyword) && n.Contains(excludeKeyword.ToLowerInvariant()))
            {
                match = false;
            }
            if (match) return s;
        }
        return null;
    }

    private Sprite FindSpriteByNameKeyword(string required1, string required2 = null)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in sprites)
        {
            if (s == null) continue;
            string n = s.name.ToLowerInvariant();
            if (n.Contains(required1.ToLowerInvariant()) && (required2 == null || n.Contains(required2.ToLowerInvariant())))
            {
                return s;
            }
        }
        return null;
    }

    /// <summary>
    /// Executes a smooth Fade-to-Black point-and-click transition to another room.
    /// </summary>
    public void TransitionToLocation(string targetLocationId, float fadeDuration = -1f, Action onComplete = null)
    {
        if (IsTransitioning) return;

        // CRITICAL SAFETY: If the player is in the Office, ANY attempt to go to the breaker MUST route to BreakerRoom first!
        if (CurrentLocationId.Equals("Office", StringComparison.OrdinalIgnoreCase))
        {
            if (!targetLocationId.Equals("OfficeBehind", StringComparison.OrdinalIgnoreCase) &&
                targetLocationId.IndexOf("behind", StringComparison.OrdinalIgnoreCase) < 0)
            {
                if (targetLocationId.IndexOf("closeup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    targetLocationId.IndexOf("breakeroff", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    targetLocationId.IndexOf("breaker_off", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    targetLocationId.Equals("Breaker", StringComparison.OrdinalIgnoreCase) ||
                    targetLocationId.Equals("BreakerCloseUp", StringComparison.OrdinalIgnoreCase))
                {
                    Debug.Log($"[LocationManager] Routing office navigation from '{targetLocationId}' to 'BreakerRoom'.");
                    targetLocationId = "BreakerRoom";
                }
            }
        }

        // Auto-re-discover locations in case any were enabled or instantiated dynamically
        if (!registeredLocations.ContainsKey(targetLocationId))
        {
            DiscoverAllLocations();
        }

        // Fallback: Search hierarchy for GameObject named or sprite matching targetLocationId
        if (!registeredLocations.ContainsKey(targetLocationId))
        {
            FindAndRegisterFallbackLocation(targetLocationId);
        }

        if (!registeredLocations.ContainsKey(targetLocationId))
        {
            Debug.LogWarning($"[LocationManager] Target location '{targetLocationId}' is not registered and could not be auto-discovered!");
            return;
        }

        if (fadeDuration <= 0f) fadeDuration = defaultFadeDuration;

        StartCoroutine(TransitionRoutine(targetLocationId, fadeDuration, onComplete));
    }

    private void FindAndRegisterFallbackLocation(string targetLocationId)
    {
        if (string.IsNullOrEmpty(targetLocationId)) return;
        string cleanTarget = targetLocationId.Replace(" ", "").Replace("_", "").ToLowerInvariant();

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isLoaded)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (var root in roots)
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    string nameClean = t.name.Replace(" ", "").Replace("_", "").ToLowerInvariant();
                    if (nameClean.Contains("hotspot") || nameClean.Contains("button") || nameClean.Contains("event") || nameClean.Contains("light") || nameClean.Contains("sound"))
                        continue;

                    bool isMatch = nameClean == cleanTarget ||
                                   nameClean == cleanTarget + "panel" ||
                                   nameClean == cleanTarget + "view";

                    if (!isMatch)
                    {
                        // Breaker Room: Match name or sprite (MUST NOT match Close-Up or OFF panels)
                        if (cleanTarget.Contains("breakerroom") || cleanTarget == "breaker")
                        {
                            if ((nameClean.Contains("breakerroom") || (nameClean.Contains("breaker") && nameClean.Contains("room")) || nameClean == "breakerpanel") &&
                                !nameClean.Contains("off") && !nameClean.Contains("closeup") && !nameClean.Contains("switch"))
                            {
                                isMatch = true;
                            }
                        }
                        // Breaker Close-Up: Matches Close-Up, OFF, or ON panels
                        else if (cleanTarget.Contains("closeup"))
                        {
                            if (nameClean.Contains("closeup") || nameClean.Contains("off") || nameClean.Contains("switch"))
                            {
                                isMatch = true;
                            }
                        }
                        // Office Behind
                        else if (cleanTarget.Contains("behind"))
                        {
                            if (nameClean.Contains("behind"))
                            {
                                isMatch = true;
                            }
                        }
                        // Office / HUD
                        else if (!cleanTarget.Contains("behind") && cleanTarget.Contains("office") && (nameClean.Contains("office") || nameClean.Contains("hud")) && !nameClean.Contains("behind"))
                        {
                            isMatch = true;
                        }
                    }

                    // Check by sprite if it has an Image component
                    if (!isMatch)
                    {
                        Image img = t.GetComponent<Image>();
                        if (img != null && img.sprite != null)
                        {
                            string sName = img.sprite.name.ToLowerInvariant();
                            if ((cleanTarget.Contains("breakerroom") || cleanTarget == "breaker") && (sName.Contains("room") || (sName.Contains("breaker") && !sName.Contains("off") && !sName.Contains("on"))))
                            {
                                isMatch = true;
                            }
                            else if (cleanTarget.Contains("closeup") && (sName.Contains("off") || sName.Contains("on") || sName.Contains("closeup")))
                            {
                                isMatch = true;
                            }
                        }
                    }

                    if (isMatch)
                    {
                        LocationView lv = t.GetComponent<LocationView>();
                        if (lv == null) lv = t.gameObject.AddComponent<LocationView>();
                        lv.locationId = targetLocationId;
                        RegisterLocation(lv);
                        Debug.Log($"[LocationManager] Auto-discovered and registered location '{targetLocationId}' on GameObject '{t.name}'!");
                        return;
                    }
                }
            }
        }
    }

    private IEnumerator TransitionRoutine(string targetLocationId, float duration, Action onComplete)
    {
        IsTransitioning = true;

        if (transitionSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(transitionSound);
        }

        // 1. Fade Out (Alpha 0 -> 1)
        float elapsed = 0f;
        if (transitionFadeCanvasGroup != null)
        {
            transitionFadeCanvasGroup.blocksRaycasts = true;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                transitionFadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
                yield return null;
            }
            transitionFadeCanvasGroup.alpha = 1f;
        }

        // 2. Suspenseful Blackout Pause
        if (blackHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(blackHoldDuration);
        }

        // 3. Swap Active Room Views (Safe Execution)
        try
        {
            SwitchViews(targetLocationId);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocationManager] Error switching views to '{targetLocationId}': {ex}");
        }

        // 4. Fade In (Alpha 1 -> 0) - Guaranteed to run so screen never freezes black
        elapsed = 0f;
        if (transitionFadeCanvasGroup != null)
        {
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                transitionFadeCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / duration));
                yield return null;
            }
            transitionFadeCanvasGroup.alpha = 0f;
            transitionFadeCanvasGroup.blocksRaycasts = false;
        }

        IsTransitioning = false;
        LastTransitionTime = Time.unscaledTime;
        try
        {
            onComplete?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[LocationManager] Error in onComplete callback: {ex}");
        }
    }

    private void SwitchViews(string targetLocationId)
    {
        try
        {
            EnsurePanelOpacityAndSprites();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LocationManager] Warning in EnsurePanelOpacityAndSprites: {ex.Message}");
        }

        bool isGoingToOffice = targetLocationId.Equals("Office", StringComparison.OrdinalIgnoreCase);
        bool isGoingToOfficeBehind = targetLocationId.Equals("OfficeBehind", StringComparison.OrdinalIgnoreCase);
        bool isGoingToRoom = targetLocationId.Equals("BreakerRoom", StringComparison.OrdinalIgnoreCase) ||
                             targetLocationId.Equals("Breaker", StringComparison.OrdinalIgnoreCase);
        bool isGoingToCloseUp = targetLocationId.Equals("BreakerCloseUp", StringComparison.OrdinalIgnoreCase);

        foreach (var kvp in registeredLocations)
        {
            bool isActive = kvp.Key.Equals(targetLocationId, StringComparison.OrdinalIgnoreCase);
            if (kvp.Value != null)
            {
                kvp.Value.SetActiveLocation(isActive);
            }
        }

        if (isGoingToOffice)
        {
            if (registeredLocations.TryGetValue("Office", out LocationView officeView) && officeView != null)
            {
                officeView.gameObject.SetActive(true);
                officeView.SetActiveLocation(true);

                Transform officePanel = officeView.transform.Find("OfficePanel");
                if (officePanel != null) officePanel.gameObject.SetActive(true);
            }

            if (registeredLocations.TryGetValue("OfficeBehind", out LocationView obView) && obView != null)
            {
                obView.SetActiveLocation(false);
                obView.gameObject.SetActive(false);
            }
            if (registeredLocations.TryGetValue("BreakerRoom", out LocationView brView) && brView != null)
            {
                brView.SetActiveLocation(false);
                brView.gameObject.SetActive(false);
            }
            if (registeredLocations.TryGetValue("BreakerCloseUp", out LocationView bcuView) && bcuView != null)
            {
                bcuView.SetActiveLocation(false);
                bcuView.gameObject.SetActive(false);
            }

            GameObject behindPanel = GameObject.Find("OfficeBehindPanel") ?? GameObject.Find("OfficeBehind");
            if (behindPanel != null) behindPanel.SetActive(false);

            if (typingSystem != null)
            {
                typingSystem.SetTypingActive(true);
                typingSystem.RestoreTypingInput();
                typingSystem.AutoRefocusInput(true);
            }
        }
        else if (isGoingToOfficeBehind)
        {
            if (registeredLocations.TryGetValue("OfficeBehind", out LocationView obView) && obView != null)
            {
                obView.gameObject.SetActive(true);
                obView.SetActiveLocation(true);
                CanvasGroup cg = obView.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }
            if (registeredLocations.TryGetValue("Office", out LocationView officeView) && officeView != null)
            {
                officeView.SetActiveLocation(false);
                officeView.gameObject.SetActive(false);
            }
            if (registeredLocations.TryGetValue("BreakerRoom", out LocationView brView) && brView != null)
            {
                brView.SetActiveLocation(false);
                brView.gameObject.SetActive(false);
            }
            if (registeredLocations.TryGetValue("BreakerCloseUp", out LocationView bcuView) && bcuView != null)
            {
                bcuView.SetActiveLocation(false);
                bcuView.gameObject.SetActive(false);
            }

            // Fallback: search by name to guarantee Breaker Room never shows through:
            GameObject brObj = GameObject.Find("BreakerRoomPanel");
            if (brObj != null) brObj.SetActive(false);
            GameObject bcuObj = GameObject.Find("BreakerCloseUpPanel");
            if (bcuObj != null) bcuObj.SetActive(false);

            // Guarantee OfficeBehindPanel is active and visible
            GameObject behindPanel = GameObject.Find("OfficeBehindPanel") ?? GameObject.Find("OfficeBehind");
            if (behindPanel != null)
            {
                behindPanel.SetActive(true);
                CanvasGroup cg = behindPanel.GetComponent<CanvasGroup>();
                if (cg != null)
                {
                    cg.alpha = 1f;
                    cg.blocksRaycasts = true;
                    cg.interactable = true;
                }
            }

            if (typingSystem != null)
            {
                typingSystem.SetTypingActive(false);
            }
        }
        else if (isGoingToRoom)
        {
            if (registeredLocations.TryGetValue("OfficeBehind", out LocationView obView) && obView != null)
            {
                obView.SetActiveLocation(false);
                obView.gameObject.SetActive(false);
            }
            GameObject behindPanel = GameObject.Find("OfficeBehindPanel") ?? GameObject.Find("OfficeBehind");
            if (behindPanel != null) behindPanel.SetActive(false);
            // When in Breaker Room: Hide Close-Up explicitly!
            if (registeredLocations.TryGetValue("BreakerCloseUp", out LocationView closeUpView) && closeUpView != null)
            {
                closeUpView.SetActiveLocation(false);
            }

            // Also check children of BreakerRoom in case Close-Up was parented inside it
            if (registeredLocations.TryGetValue("BreakerRoom", out LocationView roomView) && roomView != null)
            {
                Image[] childImages = roomView.GetComponentsInChildren<Image>(true);
                foreach (var img in childImages)
                {
                    if (img != null && img.gameObject != roomView.gameObject)
                    {
                        string sName = (img.sprite != null ? img.sprite.name : "").ToLowerInvariant();
                        string oName = img.gameObject.name.ToLowerInvariant();
                        if (sName.Contains("off") || sName.Contains("on") || oName.Contains("closeup") || oName.Contains("off"))
                        {
                            img.gameObject.SetActive(false);
                        }
                    }
                }

                // Ensure ReturnToOfficeHotspot (the door) is active, interactable, and visibly highlighted
                ClickableHotspot[] roomHotspots = roomView.GetComponentsInChildren<ClickableHotspot>(true);
                foreach (var h in roomHotspots)
                {
                    string hName = h.gameObject.name.ToLowerInvariant();
                    if (hName.Contains("office") || hName.Contains("return") || hName.Contains("door"))
                    {
                        h.gameObject.SetActive(true);
                        h.SetInteractable(true);
                        h.targetLocationId = "Office";

                        Image hImg = h.GetComponent<Image>();
                        if (hImg == null) hImg = h.gameObject.AddComponent<Image>();
                        Color c = hImg.color;
                        if (c.a < 0.12f)
                        {
                            c = new Color(1f, 1f, 1f, 0.22f);
                            hImg.color = c;
                        }
                        hImg.raycastTarget = true;
                        h.enableHoverAlpha = true;
                        h.normalAlpha = 0.22f;
                        h.hoverAlpha = 0.55f;
                    }
                }
            }
        }
        else if (isGoingToCloseUp)
        {
            if (registeredLocations.TryGetValue("OfficeBehind", out LocationView obView) && obView != null)
            {
                obView.SetActiveLocation(false);
                obView.gameObject.SetActive(false);
            }
            GameObject behindPanel = GameObject.Find("OfficeBehindPanel") ?? GameObject.Find("OfficeBehind");
            if (behindPanel != null) behindPanel.SetActive(false);

            // When in Close-Up: Show Close-Up!
            if (registeredLocations.TryGetValue("BreakerCloseUp", out LocationView closeUpView) && closeUpView != null)
            {
                // If it is a child of BreakerRoom, ensure parent is active
                Transform p = closeUpView.transform.parent;
                while (p != null)
                {
                    LocationView pLv = p.GetComponent<LocationView>();
                    if (pLv != null)
                    {
                        pLv.gameObject.SetActive(true);
                        break;
                    }
                    p = p.parent;
                }
                closeUpView.SetActiveLocation(true);

                // Ensure BackToRoomHotspot is active, interactable, and visible
                ClickableHotspot[] closeUpHotspots = closeUpView.GetComponentsInChildren<ClickableHotspot>(true);
                foreach (var h in closeUpHotspots)
                {
                    string hName = h.gameObject.name.ToLowerInvariant();
                    if (hName.Contains("room") || hName.Contains("back") || hName.Contains("return"))
                    {
                        h.gameObject.SetActive(true);
                        h.SetInteractable(true);
                        h.targetLocationId = "BreakerRoom";

                        Image hImg = h.GetComponent<Image>();
                        if (hImg == null) hImg = h.gameObject.AddComponent<Image>();
                        Color c = hImg.color;
                        if (c.a < 0.12f)
                        {
                            c = new Color(1f, 1f, 1f, 0.22f);
                            hImg.color = c;
                        }
                        hImg.raycastTarget = true;
                        h.enableHoverAlpha = true;
                        h.normalAlpha = 0.22f;
                        h.hoverAlpha = 0.55f;
                    }
                }
            }
        }

        CurrentLocationId = targetLocationId;
        OnLocationChanged?.Invoke(targetLocationId);

        // Manage typing availability based on location
        if (registeredLocations.TryGetValue(targetLocationId, out LocationView currentView))
        {
            if (typingSystem != null)
            {
                typingSystem.SetTypingActive(currentView.allowsTyping);
            }
        }
    }

    private void EnsureFadeCanvasExists()
    {
        if (transitionFadeCanvasGroup != null) return;

        // Auto-create transition canvas overlay if not wired in Inspector
        GameObject canvasObj = new GameObject("TransitionFadeOverlayCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Always on top of everything

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        transitionFadeCanvasGroup = canvasObj.AddComponent<CanvasGroup>();
        transitionFadeCanvasGroup.alpha = 0f;
        transitionFadeCanvasGroup.blocksRaycasts = false;
        transitionFadeCanvasGroup.interactable = false;

        GameObject blackPanel = new GameObject("BlackOverlayImage");
        blackPanel.transform.SetParent(canvasObj.transform, false);
        Image img = blackPanel.AddComponent<Image>();
        img.color = Color.black;

        RectTransform rt = blackPanel.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
