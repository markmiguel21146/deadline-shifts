using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// First environmental horror event: Power Outage / Circuit Breaker Failure.
/// Flow:
/// 1. Office: Lights go out -> Click transparent hotspot -> Go to Breaker Room (Breaker Room.jpg)
/// 2. Breaker Room: Click breaker on wall -> Zoom in to Breaker Close-Up (Breaker OFF.jpg)
/// 3. Breaker Close-Up: Click switch -> Flips to Breaker ON.jpg & restores power -> Click bottom button back to Breaker Room
/// 4. Breaker Room: Click bottom button back to Office
/// </summary>
public class BreakerEvent : EnvironmentalEvent
{
    [Header("Lighting Controls")]
    [Tooltip("Primary Global Light 2D to shut off during the outage")]
    public Light2D globalLight;
    public Light2D[] additionalLightsToDim;
    [Range(0.05f, 0.5f)]
    [SerializeField] private float blackoutIntensity = 0.15f; // Dim dim light (darker, barely visible environment)
    [SerializeField] private Color blackoutColor = new Color(0.35f, 0.42f, 0.55f, 1f); // Dim cool ambiance

    [Header("Switch Shake & Snap Settings")]
    [SerializeField] private float switchShakeDuration = 0.18f;
    [SerializeField] private float switchShakeMagnitude = 12f;

    [Header("Navigation Hotspots")]
    [Tooltip("Transparent hotspot in Office leading to Breaker Room")]
    public ClickableHotspot officeToBreakerHotspot;

    [Tooltip("Hotspot on the wall in Breaker Room leading to Breaker Close-Up")]
    public ClickableHotspot breakerRoomToCloseUpHotspot;

    [Tooltip("Hotspot in Breaker Close-Up on the switch to flip power")]
    public ClickableHotspot breakerSwitchHotspot;

    [Tooltip("Button at bottom of Close-Up returning to Breaker Room")]
    public ClickableHotspot closeUpToBreakerRoomHotspot;

    [Tooltip("Button at bottom of Breaker Room returning to Office")]
    public ClickableHotspot breakerToOfficeHotspot;

    [Header("Breaker Room Visuals")]
    [Tooltip("Full-screen Image in Breaker Room panel")]
    public Image breakerRoomImage;
    public Sprite breakerRoomOffSprite;
    public Sprite breakerRoomOnSprite;

    [Header("Breaker Close-Up Visuals")]
    [Tooltip("Full-screen Image in Breaker Close-Up panel")]
    public Image breakerSwitchImage;
    public Sprite breakerOffSprite;
    public Sprite breakerOnSprite;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip blackoutSound;
    public AudioClip breakerFlipSound;
    public AudioClip powerRestoreSound;

    public bool IsPowerRestored => isPowerRestored;

    private struct SavedLightState
    {
        public Light2D light;
        public float intensity;
        public Color color;
    }

    private readonly List<SavedLightState> globalLightStates = new List<SavedLightState>();
    private bool isPowerRestored = true;

    private readonly List<Light2D> disabledLights = new List<Light2D>();
    private readonly List<MonoBehaviour> disabledFlickerScripts = new List<MonoBehaviour>();

    protected override void Awake()
    {
        base.Awake();
        eventId = "BreakerOutage";

        // Auto-correct to dim dim light (darker, barely visible)
        if (blackoutIntensity > 0.22f || blackoutIntensity < 0.08f)
        {
            blackoutIntensity = 0.15f;
        }
        if (blackoutColor.r > 0.5f || blackoutColor.g > 0.5f || blackoutColor.r < 0.15f)
        {
            blackoutColor = new Color(0.35f, 0.42f, 0.55f, 1f);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        CacheAllGlobalLights();
        AutoDiscoverHotspots();
        AutoDiscoverSprites();

        // Hotspot initial states
        if (officeToBreakerHotspot != null)
        {
            officeToBreakerHotspot.gameObject.SetActive(false);
            officeToBreakerHotspot.targetLocationId = "BreakerRoom";
        }

        if (breakerRoomToCloseUpHotspot != null)
        {
            breakerRoomToCloseUpHotspot.targetLocationId = "BreakerCloseUp";
        }

        if (breakerSwitchHotspot != null)
        {
            breakerSwitchHotspot.onClicked.AddListener(OnBreakerSwitchClicked);
        }

        if (closeUpToBreakerRoomHotspot != null)
        {
            closeUpToBreakerRoomHotspot.targetLocationId = "BreakerRoom";
        }

        if (breakerToOfficeHotspot != null)
        {
            breakerToOfficeHotspot.targetLocationId = "Office";
        }
    }

    private void CacheAllGlobalLights()
    {
        globalLightStates.Clear();
        Light2D[] allLights = FindObjectsByType<Light2D>(FindObjectsInactive.Include);
        foreach (var lt in allLights)
        {
            if (lt == null) continue;
            bool isGlobal = lt.lightType == Light2D.LightType.Global || lt.gameObject.name.ToLowerInvariant().Contains("global");
            if (isGlobal || lt == globalLight)
            {
                globalLightStates.Add(new SavedLightState { light = lt, intensity = lt.intensity, color = lt.color });
                if (globalLight == null) globalLight = lt;
            }
        }

        // If no light was marked global, take the first light as global light
        if (globalLightStates.Count == 0 && allLights.Length > 0)
        {
            globalLight = allLights[0];
            globalLightStates.Add(new SavedLightState { light = globalLight, intensity = globalLight.intensity, color = globalLight.color });
        }
    }

    private void AutoDiscoverHotspots()
    {
        ClickableHotspot[] allHotspots = FindObjectsByType<ClickableHotspot>(FindObjectsInactive.Include);
        foreach (var h in allHotspots)
        {
            string name = h.gameObject.name.ToLowerInvariant();
            
            // 1. Office -> Breaker Room (e.g. ToBreakerHotspot, EnterBreaker, DoorToBreaker, GoToBreaker, Go to breaker)
            if (officeToBreakerHotspot == null && !name.Contains("wall") && !name.Contains("switch") && !name.Contains("closeup") && !name.Contains("back") && !name.Contains("return") &&
                (name.Contains("tobreaker") || name.Contains("enterbreaker") || name.Contains("gotobreaker") || name.Contains("go to breaker") || (name.Contains("breaker") && (name.Contains("office") || name.Contains("door") || name.Contains("button") || name.Contains("go")))))
            {
                officeToBreakerHotspot = h;
                officeToBreakerHotspot.targetLocationId = "BreakerRoom";
            }
            // 2. Breaker Room -> Close-Up (e.g. WallBreakerHotspot, InspectBreaker, BreakerBox)
            else if (breakerRoomToCloseUpHotspot == null && !name.Contains("switch") && !name.Contains("return") && !name.Contains("back") && !name.Contains("office") &&
                (name.Contains("wall") || name.Contains("inspect") || name.Contains("box") || (name.Contains("breaker") && name.Contains("closeup"))))
            {
                breakerRoomToCloseUpHotspot = h;
                breakerRoomToCloseUpHotspot.targetLocationId = "BreakerCloseUp";
            }
            // 3. Switch Lever in Close-Up
            else if (breakerSwitchHotspot == null && (name.Contains("switch") || name.Contains("lever") || name.Contains("flip")))
            {
                breakerSwitchHotspot = h;
            }
            // 4. Close-Up -> Back to Breaker Room (e.g. BackToRoomHotspot, ReturnToRoom)
            else if (closeUpToBreakerRoomHotspot == null && (name.Contains("back") || name.Contains("return")) && (name.Contains("room") || name.Contains("closeup")))
            {
                closeUpToBreakerRoomHotspot = h;
                closeUpToBreakerRoomHotspot.targetLocationId = "BreakerRoom";
            }
            // 5. Breaker Room -> Return to Office (e.g. ReturnToOfficeHotspot, BackToOffice, DoorToOffice)
            else if (breakerToOfficeHotspot == null && !name.Contains("tobreaker") && (name.Contains("tooffice") || (name.Contains("return") && name.Contains("office")) || name.Contains("door") || name.Contains("backtooffice")))
            {
                breakerToOfficeHotspot = h;
                breakerToOfficeHotspot.targetLocationId = "Office";
            }
        }
    }

    private void AutoDiscoverSprites()
    {
        if (breakerRoomImage == null)
        {
            GameObject roomObj = GameObject.Find("BreakerRoomPanel");
            if (roomObj != null) breakerRoomImage = roomObj.GetComponent<Image>();
        }

        if (breakerSwitchImage == null)
        {
            GameObject closeUpObj = GameObject.Find("BreakerCloseUpPanel");
            if (closeUpObj != null) breakerSwitchImage = closeUpObj.GetComponent<Image>();
        }

        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in sprites)
        {
            if (s == null) continue;
            string n = s.name.ToLowerInvariant();
            if (breakerRoomOnSprite == null && n.Contains("room") && n.Contains("on"))
            {
                breakerRoomOnSprite = s;
            }
            else if (breakerRoomOffSprite == null && n.Contains("breaker") && n.Contains("room") && !n.Contains("on"))
            {
                breakerRoomOffSprite = s;
            }
            else if (breakerOnSprite == null && n.Contains("breaker") && n.Contains("on") && !n.Contains("room"))
            {
                breakerOnSprite = s;
            }
            else if (breakerOffSprite == null && n.Contains("breaker") && n.Contains("off") && !n.Contains("room"))
            {
                breakerOffSprite = s;
            }
        }
    }

    private DigitalClock FindOrGetClock()
    {
        DigitalClock clock = FindAnyObjectByType<DigitalClock>(FindObjectsInactive.Include);
        if (clock == null)
        {
            TMP_Text[] allTexts = FindObjectsByType<TMP_Text>(FindObjectsInactive.Include);
            foreach (var t in allTexts)
            {
                if (t.gameObject.name.IndexOf("clock", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.gameObject.name.IndexOf("time", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clock = t.GetComponent<DigitalClock>();
                    if (clock == null) clock = t.gameObject.AddComponent<DigitalClock>();
                    break;
                }
            }
        }
        return clock;
    }

    public override void TriggerEvent()
    {
        base.TriggerEvent();
        isPowerRestored = false;

        // Ensure hotspots are found
        AutoDiscoverHotspots();

        // 1. Play blackout sound
        if (blackoutSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(blackoutSound);
        }

        // 2. Dim all global lights & stop non-global lights
        disabledLights.Clear();
        disabledFlickerScripts.Clear();

        CacheAllGlobalLights();

        // Enforce dim dim lighting (darker, barely visible)
        if (blackoutIntensity > 0.22f || blackoutIntensity < 0.08f)
        {
            blackoutIntensity = 0.15f;
        }
        if (blackoutColor.r > 0.5f || blackoutColor.g > 0.5f || blackoutColor.r < 0.15f)
        {
            blackoutColor = new Color(0.35f, 0.42f, 0.55f, 1f);
        }

        foreach (var gl in globalLightStates)
        {
            if (gl.light != null)
            {
                gl.light.intensity = blackoutIntensity;
                gl.light.color = blackoutColor;
                gl.light.enabled = true;
            }
        }

        // Disable ALL flicker scripts so they stop overriding light intensities every frame!
        MonoBehaviour[] allScripts = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        foreach (var mb in allScripts)
        {
            if (mb != null && mb != this)
            {
                string scriptName = mb.GetType().Name.ToLowerInvariant();
                if (scriptName.Contains("flicker") || scriptName.Contains("brokenlight"))
                {
                    if (mb.enabled)
                    {
                        mb.enabled = false;
                        disabledFlickerScripts.Add(mb);
                    }
                }
            }
        }

        // Disable non-global lights (fixtures, lamps, monitors)
        Light2D[] allLights = FindObjectsByType<Light2D>(FindObjectsInactive.Include);
        foreach (var lt in allLights)
        {
            if (lt == null) continue;
            bool isGlobal = false;
            foreach (var gl in globalLightStates)
            {
                if (gl.light == lt) { isGlobal = true; break; }
            }
            if (!isGlobal)
            {
                if (lt.enabled)
                {
                    lt.enabled = false;
                    disabledLights.Add(lt);
                }
            }
        }

        if (additionalLightsToDim != null)
        {
            foreach (var lt in additionalLightsToDim)
            {
                if (lt != null) lt.enabled = false;
            }
        }

        // 3. Set breaker room and close-up images to OFF state
        AutoDiscoverSprites();
        if (breakerRoomImage != null && breakerRoomOffSprite != null)
        {
            breakerRoomImage.sprite = breakerRoomOffSprite;
        }

        if (breakerSwitchImage != null && breakerOffSprite != null)
        {
            breakerSwitchImage.sprite = breakerOffSprite;
        }

        // Shut off digital clock display
        DigitalClock clock = FindOrGetClock();
        if (clock != null) clock.SetPower(false);

        // 4. Activate Office-to-Breaker navigation hotspot
        if (officeToBreakerHotspot != null)
        {
            officeToBreakerHotspot.gameObject.SetActive(true);
            officeToBreakerHotspot.SetInteractable(true);
            officeToBreakerHotspot.targetLocationId = "BreakerRoom";
            officeToBreakerHotspot.transform.SetAsLastSibling();

            // Position prominently over the office doorway on the left side
            RectTransform rt = officeToBreakerHotspot.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(-780f, 0f);
                rt.sizeDelta = new Vector2(260f, 800f);
            }

            // Remove any added Canvas that broke UI scaling in the world space/camera canvas
            Canvas addedCanvas = officeToBreakerHotspot.GetComponent<Canvas>();
            if (addedCanvas != null)
            {
                Destroy(addedCanvas);
            }

            // Remove rogue Canvas on parent (1_GameplayHUD) so it renders uniformly on WorkplaceCanvas
            if (officeToBreakerHotspot.transform.parent != null)
            {
                Canvas parentCanvas = officeToBreakerHotspot.transform.parent.GetComponent<Canvas>();
                if (parentCanvas != null && parentCanvas.gameObject.name != "WorkplaceCanvas")
                {
                    Destroy(parentCanvas);
                }
            }

            // FNAF 4 transparent white pulsing cue with rounded borders on the office door during blackout
            officeToBreakerHotspot.hotspotName = "Go to Breaker Room";
            officeToBreakerHotspot.SetupVisualCue("◄\nBREAKER\nROOM", Color.white, true, 0.15f, 0.42f);

            // Smoothly fade into existence upon blackout
            officeToBreakerHotspot.FadeIn(1.2f);
        }

        // Setup cues for breaker room and close-up views (FNAF 4 transparent white styling)
        if (breakerRoomToCloseUpHotspot != null)
        {
            breakerRoomToCloseUpHotspot.SetupVisualCue("[ INSPECT BREAKER ]", Color.white, true, 0.15f, 0.42f);
        }

        if (breakerSwitchHotspot != null)
        {
            breakerSwitchHotspot.SetupVisualCue("[ FLIP SWITCH ]", Color.white, true, 0.18f, 0.45f);
        }

        if (closeUpToBreakerRoomHotspot != null)
        {
            closeUpToBreakerRoomHotspot.SetupVisualCue("▼ [ BACK TO ROOM ]", Color.white, false, 0.15f, 0.15f);
        }

        // Return camera to monitor if player was looking behind when blackout hit
        LookBehindController lbc = FindAnyObjectByType<LookBehindController>(FindObjectsInactive.Include);
        if (lbc != null && lbc.IsLookingAway)
        {
            lbc.LookBackAtMonitor();
        }

        // Ensure ReturnToOfficeHotspot is active and interactable
        if (breakerToOfficeHotspot != null)
        {
            breakerToOfficeHotspot.gameObject.SetActive(true);
            breakerToOfficeHotspot.SetInteractable(true);
            breakerToOfficeHotspot.targetLocationId = "Office";
        }
    }

    public void OnBreakerSwitchClicked()
    {
        if (isPowerRestored) return;

        StartCoroutine(RestorePowerSequence());
    }

    private IEnumerator RestorePowerSequence()
    {
        isPowerRestored = true;

        // 1. Play heavy switch throw sound
        if (breakerFlipSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(breakerFlipSound);
        }

        // 2. Mechanical shake on the breaker switch image
        RectTransform imageRect = breakerSwitchImage != null ? breakerSwitchImage.rectTransform : null;
        Vector2 originalPos = imageRect != null ? imageRect.anchoredPosition : Vector2.zero;

        float shakeElapsed = 0f;
        bool spriteSwapped = false;

        while (shakeElapsed < switchShakeDuration)
        {
            shakeElapsed += Time.unscaledDeltaTime;
            float progress = shakeElapsed / switchShakeDuration;

            // Instant mechanical switch snap midway through the shake (~0.06s in)
            if (!spriteSwapped && progress >= 0.35f)
            {
                spriteSwapped = true;
                if (breakerSwitchImage != null && breakerOnSprite != null)
                {
                    breakerSwitchImage.sprite = breakerOnSprite;
                }
            }

            // Punchy vibration shake
            if (imageRect != null)
            {
                float currentMag = Mathf.Lerp(switchShakeMagnitude, 0f, progress);
                float xOffset = UnityEngine.Random.Range(-currentMag, currentMag);
                float yOffset = UnityEngine.Random.Range(-currentMag, currentMag);
                imageRect.anchoredPosition = originalPos + new Vector2(xOffset, yOffset);
            }

            yield return null;
        }

        if (imageRect != null)
        {
            imageRect.anchoredPosition = originalPos;
        }

        if (!spriteSwapped && breakerSwitchImage != null && breakerOnSprite != null)
        {
            breakerSwitchImage.sprite = breakerOnSprite;
        }

        // 3. Play power hum / electricity surge sound
        if (powerRestoreSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(powerRestoreSound);
        }

        // 4. Snappy fluorescent light flicker restore (0.04s per flash)
        for (int i = 0; i < 3; i++)
        {
            foreach (var gl in globalLightStates)
            {
                if (gl.light != null)
                {
                    gl.light.intensity = (i % 2 == 0) ? gl.intensity : blackoutIntensity;
                }
            }
            yield return new WaitForSecondsRealtime(0.04f);
        }

        foreach (var gl in globalLightStates)
        {
            if (gl.light != null)
            {
                gl.light.intensity = gl.intensity;
                gl.light.color = gl.color;
            }
        }

        // Restore all non-global lights
        foreach (var lt in disabledLights)
        {
            if (lt != null) lt.enabled = true;
        }
        disabledLights.Clear();

        // Restore all flicker scripts
        foreach (var mb in disabledFlickerScripts)
        {
            if (mb != null) mb.enabled = true;
        }
        disabledFlickerScripts.Clear();

        // Restore digital clock display
        DigitalClock clock = FindOrGetClock();
        if (clock != null) clock.SetPower(true);

        // Swap Breaker Room view to Breaker Room ON
        AutoDiscoverSprites();
        if (breakerRoomImage != null && breakerRoomOnSprite != null)
        {
            breakerRoomImage.sprite = breakerRoomOnSprite;
        }

        if (additionalLightsToDim != null)
        {
            foreach (var lt in additionalLightsToDim)
            {
                if (lt != null) lt.enabled = true;
            }
        }

        // 5. Unlock Return button in Breaker Room with FNAF 4 transparent white cue
        if (breakerToOfficeHotspot != null)
        {
            breakerToOfficeHotspot.gameObject.SetActive(true);
            breakerToOfficeHotspot.SetInteractable(true);
            breakerToOfficeHotspot.SetupVisualCue("[ RETURN TO OFFICE ] ►", Color.white, true, 0.18f, 0.45f);
            breakerToOfficeHotspot.onClicked.RemoveListener(OnReturnToOfficeClicked);
            breakerToOfficeHotspot.onClicked.AddListener(OnReturnToOfficeClicked);
        }

        if (breakerSwitchHotspot != null)
        {
            breakerSwitchHotspot.ClearVisualCue();
            breakerSwitchHotspot.SetInteractable(false);
        }

        // 6. Complete this environmental event
        ResolveEvent();
    }

    private void OnReturnToOfficeClicked()
    {
        if (breakerToOfficeHotspot != null)
        {
            breakerToOfficeHotspot.ClearVisualCue();
        }
    }

    public override void ResolveEvent()
    {
        base.ResolveEvent();

        if (officeToBreakerHotspot != null)
        {
            officeToBreakerHotspot.ClearVisualCue();
            officeToBreakerHotspot.SetInteractable(false);
            officeToBreakerHotspot.gameObject.SetActive(false);
        }

        if (breakerRoomToCloseUpHotspot != null)
        {
            breakerRoomToCloseUpHotspot.ClearVisualCue();
        }
    }
}
