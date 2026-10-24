using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// First-person cinematic sequence showing the protagonist walking down
/// the dimly lit, atmospheric 1990s facility corridor at night toward Terminal 4.
/// Features camera sway, footsteps, fluorescent flickering, and smooth transition to office.
/// </summary>
public class HallwayArrivalSequence : MonoBehaviour
{
    public static HallwayArrivalSequence Instance { get; private set; }

    [Header("UI Elements")]
    public GameObject hallwayPanel;
    public CanvasGroup canvasGroup;
    public Image hallwayImage;
    public TextMeshProUGUI subtitleText;
    public CanvasGroup subtitleGroup;
    public Image vignetteOverlay;

    [Header("Visual Assets")]
    public Sprite hallwaySprite;

    [Header("Audio")]
    public AudioSource ambientAudioSource;
    public AudioSource sfxAudioSource;
    public AudioClip ambientVentClip;
    public AudioClip lightBuzzClip;
    public AudioClip footstepClip;
    public AudioClip doorLatchClip;

    [Header("Timing & Motion")]
    public float sequenceDuration = 8.5f;
    public float fadeDuration = 1.0f;
    public float walkBobFrequency = 4.8f;
    public float walkBobAmplitudeY = 12f;
    public float walkBobAmplitudeX = 5f;
    public float zoomTargetScale = 1.32f;

    [Header("Next Sequence")]
    public ShiftTitleCard shiftTitleCard;

    private bool isPlaying = false;
    private Coroutine sequenceCoroutine;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        AutoDiscoverAssets();
    }

    private void Start()
    {
        if (hallwayPanel != null && !isPlaying)
        {
            hallwayPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (isPlaying)
        {
            // Skip on Space, Enter, or Escape
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
            {
                SkipSequence();
            }
        }
    }

    public void PlayHallwaySequence()
    {
        if (isPlaying) return;

        EnsureHallwayUI();
        AutoDiscoverAssets();

        GameManager.isCutscenePlaying = true;
        isPlaying = true;

        if (hallwayPanel != null)
        {
            hallwayPanel.SetActive(true);
            hallwayPanel.transform.SetAsLastSibling();
        }

        sequenceCoroutine = StartCoroutine(HallwayRoutine());
    }

    public void SkipSequence()
    {
        if (!isPlaying) return;

        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
        }

        StartCoroutine(FastFadeToShiftCard());
    }

    private IEnumerator HallwayRoutine()
    {
        float elapsed = 0f;

        // Start ambient vent hum
        if (ambientAudioSource != null && ambientVentClip != null)
        {
            ambientAudioSource.clip = ambientVentClip;
            ambientAudioSource.loop = true;
            ambientAudioSource.volume = 0.6f;
            ambientAudioSource.Play();
        }

        // 1. Initial fade-in from black
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            float fadeInElapsed = 0f;
            while (fadeInElapsed < fadeDuration)
            {
                fadeInElapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(fadeInElapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
        }

        RectTransform imgRt = hallwayImage != null ? hallwayImage.rectTransform : null;
        Vector2 originalPos = Vector2.zero;

        float nextFootstepTime = 0.35f;
        float footstepInterval = 0.65f;
        float nextFlickerTime = 2.2f;

        // Subtitle dialogue segments
        string[] subtitles = new string[]
        {
            "02:47 AM. Facility Sub-Level 2.",
            "Another emergency night shift. Another backlog to clear.",
            "Terminal 4. Behind this door."
        };
        float[] subtitleStartTimes = new float[] { 0.4f, 3.2f, 6.0f };
        int currentSubtitleIndex = -1;

        while (elapsed < sequenceDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / sequenceDuration);

            // Camera Walking Sway & Zoom
            if (imgRt != null)
            {
                float currentScale = Mathf.Lerp(1.0f, zoomTargetScale, Mathf.SmoothStep(0f, 1f, progress));
                imgRt.localScale = new Vector3(currentScale, currentScale, 1f);

                float swayY = Mathf.Abs(Mathf.Sin(elapsed * walkBobFrequency)) * walkBobAmplitudeY;
                float swayX = Mathf.Sin(elapsed * (walkBobFrequency * 0.5f)) * walkBobAmplitudeX;
                imgRt.anchoredPosition = originalPos + new Vector2(swayX, -swayY);
            }

            // Periodic Footsteps
            if (elapsed >= nextFootstepTime && progress < 0.9f)
            {
                nextFootstepTime = elapsed + footstepInterval;
                PlayFootstepSound();
            }

            // Fluorescent Tube Flicker
            if (elapsed >= nextFlickerTime)
            {
                nextFlickerTime = elapsed + UnityEngine.Random.Range(2.8f, 4.0f);
                StartCoroutine(FluorescentFlicker());
            }

            // Subtitle sequencing
            for (int i = 0; i < subtitleStartTimes.Length; i++)
            {
                if (elapsed >= subtitleStartTimes[i] && currentSubtitleIndex < i)
                {
                    currentSubtitleIndex = i;
                    SetSubtitle(subtitles[i]);
                }
            }

            yield return null;
        }

        // Arrived at door - pause walking, sound of metallic latch
        if (imgRt != null)
        {
            imgRt.anchoredPosition = originalPos;
        }

        if (sfxAudioSource != null && doorLatchClip != null)
        {
            sfxAudioSource.pitch = 0.9f;
            sfxAudioSource.PlayOneShot(doorLatchClip);
        }

        yield return new WaitForSecondsRealtime(0.6f);

        // Smooth fade out to black
        yield return StartCoroutine(FastFadeToShiftCard());
    }

    private IEnumerator FastFadeToShiftCard()
    {
        isPlaying = false;

        float fadeElapsed = 0f;
        float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;

        while (fadeElapsed < fadeDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            float t = fadeElapsed / fadeDuration;

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            }

            if (ambientAudioSource != null)
            {
                ambientAudioSource.volume = Mathf.Lerp(0.6f, 0f, t);
            }

            yield return null;
        }

        if (ambientAudioSource != null)
        {
            ambientAudioSource.Stop();
        }

        if (hallwayPanel != null)
        {
            hallwayPanel.SetActive(false);
        }

        // Advance to ShiftTitleCard
        PlayCurrentShiftTitleCard();
    }

    private void PlayFootstepSound()
    {
        if (sfxAudioSource != null && footstepClip != null)
        {
            sfxAudioSource.pitch = UnityEngine.Random.Range(0.45f, 0.58f); // Deepened footstep tone
            sfxAudioSource.PlayOneShot(footstepClip, 0.45f);
        }
    }

    private IEnumerator FluorescentFlicker()
    {
        if (hallwayImage == null) yield break;

        if (sfxAudioSource != null && lightBuzzClip != null)
        {
            sfxAudioSource.pitch = UnityEngine.Random.Range(1.2f, 1.4f);
            sfxAudioSource.PlayOneShot(lightBuzzClip, 0.25f);
        }

        Color originalColor = hallwayImage.color;
        Color dimColor = new Color(originalColor.r * 0.45f, originalColor.g * 0.45f, originalColor.b * 0.5f, originalColor.a);

        for (int i = 0; i < 2; i++)
        {
            hallwayImage.color = dimColor;
            yield return new WaitForSecondsRealtime(0.04f);
            hallwayImage.color = originalColor;
            yield return new WaitForSecondsRealtime(0.05f);
        }

        hallwayImage.color = originalColor;
    }

    private void SetSubtitle(string text)
    {
        if (subtitleText != null)
        {
            subtitleText.text = text;
        }

        if (subtitleGroup != null)
        {
            StartCoroutine(FadeSubtitleInAndOut());
        }
    }

    private IEnumerator FadeSubtitleInAndOut()
    {
        if (subtitleGroup == null) yield break;

        // Fade in
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.unscaledDeltaTime;
            subtitleGroup.alpha = Mathf.Clamp01(t / 0.4f);
            yield return null;
        }
        subtitleGroup.alpha = 1f;

        yield return new WaitForSecondsRealtime(2.0f);

        // Fade out
        t = 0f;
        while (t < 0.5f)
        {
            t += Time.unscaledDeltaTime;
            subtitleGroup.alpha = Mathf.Clamp01(1f - (t / 0.5f));
            yield return null;
        }
        subtitleGroup.alpha = 0f;
    }

    private void PlayCurrentShiftTitleCard()
    {
        if (shiftTitleCard == null)
        {
            shiftTitleCard = FindAnyObjectByType<ShiftTitleCard>(FindObjectsInactive.Include);
        }

        if (shiftTitleCard != null)
        {
            int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
            string instruction = currentShift switch
            {
                1 => "Finish your quota. Leave when finished.",
                2 => "Archive status compromised. Type REBOOT to clear corrupted text.",
                3 => "Report any unapproved symbols immediately.",
                _ => "Finish your quota. Leave when finished."
            };

            shiftTitleCard.PlayCard(currentShift, instruction);
        }
        else
        {
            Debug.LogWarning("[HallwayArrivalSequence] ShiftTitleCard not found! Resuming gameplay directly.");
            GameManager.isCutscenePlaying = false;
        }
    }

    public void EnsureHallwayUI()
    {
        if (hallwayPanel != null) return;

        // Find existing or create under best Canvas
        Canvas targetCanvas = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
        foreach (var c in canvases)
        {
            if (c.gameObject.name.Contains("Workplace") || c.gameObject.name.Contains("Intro") || c.gameObject.name.Contains("Cutscene") || c.gameObject.name.Contains("HUD"))
            {
                targetCanvas = c;
                break;
            }
        }
        if (targetCanvas == null && canvases.Length > 0) targetCanvas = canvases[0];
        if (targetCanvas == null) return;

        hallwayPanel = new GameObject("HallwayArrivalPanel");
        hallwayPanel.transform.SetParent(targetCanvas.transform, false);

        RectTransform rootRt = hallwayPanel.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        canvasGroup = hallwayPanel.AddComponent<CanvasGroup>();

        // Hallway Background Image
        GameObject imgObj = new GameObject("HallwayImage");
        imgObj.transform.SetParent(hallwayPanel.transform, false);

        RectTransform imgRt = imgObj.AddComponent<RectTransform>();
        imgRt.anchorMin = Vector2.zero;
        imgRt.anchorMax = Vector2.one;
        imgRt.offsetMin = Vector2.zero;
        imgRt.offsetMax = Vector2.zero;

        hallwayImage = imgObj.AddComponent<Image>();
        hallwayImage.color = new Color(0.9f, 0.9f, 0.95f, 1f);

        if (hallwaySprite != null)
        {
            hallwayImage.sprite = hallwaySprite;
        }

        // Dark horror vignette overlay
        GameObject vigObj = new GameObject("VignetteOverlay");
        vigObj.transform.SetParent(hallwayPanel.transform, false);

        RectTransform vigRt = vigObj.AddComponent<RectTransform>();
        vigRt.anchorMin = Vector2.zero;
        vigRt.anchorMax = Vector2.one;
        vigRt.offsetMin = Vector2.zero;
        vigRt.offsetMax = Vector2.zero;

        vignetteOverlay = vigObj.AddComponent<Image>();
        vignetteOverlay.color = new Color(0.01f, 0.02f, 0.02f, 0.45f);

        // Subtitle Container
        GameObject subContainer = new GameObject("SubtitleContainer");
        subContainer.transform.SetParent(hallwayPanel.transform, false);

        RectTransform subRt = subContainer.AddComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 0f);
        subRt.anchorMax = new Vector2(0.5f, 0f);
        subRt.pivot = new Vector2(0.5f, 0f);
        subRt.anchoredPosition = new Vector2(0f, 60f);
        subRt.sizeDelta = new Vector2(900f, 50f);

        subtitleGroup = subContainer.AddComponent<CanvasGroup>();
        subtitleGroup.alpha = 0f;

        GameObject textObj = new GameObject("SubtitleText");
        textObj.transform.SetParent(subContainer.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        subtitleText = textObj.AddComponent<TextMeshProUGUI>();
        subtitleText.text = "";
        subtitleText.fontSize = 20;
        subtitleText.characterSpacing = 1.2f;
        subtitleText.lineSpacing = 14f;
        subtitleText.margin = new Vector4(24, 0, 24, 0);
        subtitleText.textWrappingMode = TextWrappingModes.Normal;
        subtitleText.alignment = TextAlignmentOptions.Center;
        subtitleText.color = new Color(0.75f, 0.95f, 0.8f, 1f);
        subtitleText.fontStyle = FontStyles.Italic;

        TMP_Text fontSample = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (fontSample != null && fontSample.font != null)
        {
            subtitleText.font = fontSample.font;
        }

        // Audio Sources
        if (ambientAudioSource == null)
        {
            ambientAudioSource = gameObject.AddComponent<AudioSource>();
        }
        if (sfxAudioSource == null)
        {
            sfxAudioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void AutoDiscoverAssets()
    {
        // 1. Hallway Sprite
        if (hallwaySprite == null)
        {
            Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            foreach (var s in sprites)
            {
                if (s != null && s.name.IndexOf("hallway", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    hallwaySprite = s;
                    break;
                }
            }
        }

        if (hallwayImage != null && hallwaySprite != null)
        {
            hallwayImage.sprite = hallwaySprite;
        }

        // 2. Audio Clips
        AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (var c in clips)
        {
            if (c == null) continue;
            string n = c.name.ToLowerInvariant();

            if (ambientVentClip == null && (n.Contains("vent") || n.Contains("ambient")))
            {
                ambientVentClip = c;
            }
            else if (lightBuzzClip == null && (n.Contains("buzz") || n.Contains("breaker")))
            {
                lightBuzzClip = c;
            }
            else if (footstepClip == null && n.Contains("keystroke"))
            {
                footstepClip = c;
            }
            else if (doorLatchClip == null && n.Contains("start"))
            {
                doorLatchClip = c;
            }
        }

        if (shiftTitleCard == null)
        {
            shiftTitleCard = FindAnyObjectByType<ShiftTitleCard>(FindObjectsInactive.Include);
        }
    }
}
