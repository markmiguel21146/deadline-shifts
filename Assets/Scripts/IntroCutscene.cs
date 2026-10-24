using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Atmospheric, Undertale-inspired opening sequence for DEADLINE-SHIFTS.
/// Features letter-by-letter gradual typewriter text, high-contrast retro illustrations,
/// unsettling 1994 analog horror lore, smooth crossfades, and responsive skip controls.
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    [System.Serializable]
    public struct CutsceneSlide
    {
        public Sprite slideImage;
        [TextArea(3, 5)] public string slideText;
        public AudioClip slideAudio;
    }

    [Header("UI References")]
    [SerializeField] private GameObject cutscenePanel;
    [SerializeField] private CanvasGroup cutsceneCanvasGroup;
    [SerializeField] private Image displayImage;
    [SerializeField] private TextMeshProUGUI displayText;
    [SerializeField] private TextMeshProUGUI skipPromptText;

    [Header("Cutscene Content")]
    [SerializeField] private CutsceneSlide[] slides;

    [Header("Typewriter Settings")]
    [SerializeField] private float charTypeSpeed = 0.038f;
    [SerializeField] private float punctuationPause = 0.45f;
    [SerializeField] private float autoAdvanceDelay = 3.2f;
    [SerializeField] private float fadeDuration = 0.8f;
    public float FadeDuration => fadeDuration;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip keystrokeSound;
    [SerializeField] private AudioClip slideTransitionSound;

    [Header("Next Sequences")]
    [SerializeField] private HallwayArrivalSequence hallwaySequence;
    [SerializeField] private ShiftTitleCard shiftTitleCard;

    [Header("Play Settings")]
    [Tooltip("If true, the Undertale intro sequence plays on first shift or when testing in editor.")]
    [SerializeField] private bool forcePlayIntro = true;

    private bool isSkipping = false;
    public bool isLineTyping { get; private set; } = false;
    private bool fastForwardRequested = false;
    private Coroutine cutsceneRoutine;

    private void Awake()
    {
        Time.timeScale = 1f;

        GameManager gm = GameManager.Instance;
        if (gm == null) gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

        int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
        if (gm != null && gm.currentShiftIndex > 0)
        {
            currentShift = Mathf.Max(currentShift, gm.currentShiftIndex + 1);
        }

        int shouldPlay = 0;
        // The intro cutscene should ONLY play on the first night / initial playthrough (Shift 1)
        if (currentShift <= 1)
        {
            shouldPlay = PlayerPrefs.GetInt("PlayIntroCutscene", forcePlayIntro ? 1 : 0);
            #if UNITY_EDITOR
            if (forcePlayIntro) shouldPlay = 1;
            #endif
        }

        if (shouldPlay == 1)
        {
            GameManager.isCutscenePlaying = true;
        }

        try
        {
            EnsureIntroUI();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IntroCutscene] EnsureIntroUI in Awake: {ex.Message}");
        }

        // CRITICAL: Do NOT deactivate cutscenePanel if this script is attached to it!
        // Deactivating the GameObject in Awake() cancels Start(), preventing PlayCurrentShiftTitleCard() from running!
        if (shouldPlay == 0)
        {
            if (cutsceneCanvasGroup != null)
            {
                cutsceneCanvasGroup.alpha = 0f;
                cutsceneCanvasGroup.blocksRaycasts = false;
                cutsceneCanvasGroup.interactable = false;
            }
        }

        try
        {
            AutoDiscoverDependencies();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[IntroCutscene] AutoDiscoverDependencies in Awake: {ex.Message}");
        }
    }

    private void Start()
    {
        Time.timeScale = 1f;

        GameManager gm = GameManager.Instance;
        if (gm == null) gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

        int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
        if (gm != null && gm.currentShiftIndex > 0)
        {
            currentShift = Mathf.Max(currentShift, gm.currentShiftIndex + 1);
        }

        int shouldPlay = 0;
        // The intro cutscene should ONLY play on the first night / initial playthrough (Shift 1)
        if (currentShift <= 1)
        {
            shouldPlay = PlayerPrefs.GetInt("PlayIntroCutscene", forcePlayIntro ? 1 : 0);
            #if UNITY_EDITOR
            if (forcePlayIntro) shouldPlay = 1;
            #endif
        }

        if (shouldPlay == 1)
        {
            PlayerPrefs.SetInt("PlayIntroCutscene", 0);
            PlayerPrefs.Save();

            EnsureIntroUI();
            InitializeDefaultSlidesIfEmpty();

            cutsceneRoutine = StartCoroutine(PlayUndertaleIntroRoutine());
        }
        else
        {
            PlayCurrentShiftTitleCard();
            if (cutscenePanel != null) cutscenePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (GameManager.isCutscenePlaying && cutscenePanel != null && cutscenePanel.activeSelf)
        {
            // Advance / fast-forward: Space, Enter, KeypadEnter, Z, Left Click
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Z) ||
                Input.GetMouseButtonDown(0))
            {
                fastForwardRequested = true;
            }

            // Skip entire intro: Escape
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SkipIntro();
            }
        }
    }

    private IEnumerator PlayUndertaleIntroRoutine()
    {
        GameManager.isCutscenePlaying = true;

        if (cutscenePanel != null)
        {
            cutscenePanel.SetActive(true);
            cutscenePanel.transform.SetAsLastSibling();
        }

        if (cutsceneCanvasGroup != null)
        {
            cutsceneCanvasGroup.alpha = 1f;
        }

        if (slides == null || slides.Length == 0)
        {
            InitializeDefaultSlidesIfEmpty();
        }

        Debug.Log($"[IntroCutscene] Playing Undertale intro with {slides?.Length ?? 0} slides.");

        if (slides != null && slides.Length > 0)
        {
            for (int i = 0; i < slides.Length; i++)
            {
                if (isSkipping) break;

                CutsceneSlide slide = slides[i];
                Debug.Log($"[IntroCutscene] Displaying slide {i + 1}/{slides.Length}");

                // 1. Setup Slide Illustration
                if (displayImage != null)
                {
                    if (slide.slideImage != null)
                    {
                        displayImage.gameObject.SetActive(true);
                        displayImage.sprite = slide.slideImage;
                        displayImage.color = new Color(0.96f, 0.96f, 0.98f, 1f);
                    }
                    else
                    {
                        displayImage.gameObject.SetActive(false);
                    }
                }

                // Optional slide sound
                if (slide.slideAudio != null && audioSource != null)
                {
                    audioSource.PlayOneShot(slide.slideAudio);
                }
                else if (slideTransitionSound != null && audioSource != null)
                {
                    audioSource.pitch = 0.95f;
                    audioSource.PlayOneShot(slideTransitionSound, 0.35f);
                }

                // 2. Undertale Typewriter gradual text appearance
                yield return StartCoroutine(TypewriterRoutine(slide.slideText));

                if (isSkipping) break;

                // Brief debounce so the keypress completing the line doesn't instantly skip the slide
                yield return new WaitForSecondsRealtime(0.12f);
                fastForwardRequested = false;

                // 3. Pause for reading or until player presses advance
                float waitTimer = 0f;
                while (waitTimer < autoAdvanceDelay && !isSkipping)
                {
                    if (fastForwardRequested)
                    {
                        fastForwardRequested = false;
                        break;
                    }
                    waitTimer += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (isSkipping) break;

                // 4. Subtle slide crossfade (fade out text & slight dip in image before next slide)
                if (i < slides.Length - 1)
                {
                    float fadeElapsed = 0f;
                    Color origTextCol = displayText != null ? displayText.color : Color.white;
                    Color origImgCol = displayImage != null ? displayImage.color : Color.white;

                    while (fadeElapsed < 0.25f && !isSkipping)
                    {
                        fadeElapsed += Time.unscaledDeltaTime;
                        float t = fadeElapsed / 0.25f;
                        if (displayText != null) displayText.color = Color.Lerp(origTextCol, Color.clear, t);
                        if (displayImage != null) displayImage.color = Color.Lerp(origImgCol, new Color(origImgCol.r, origImgCol.g, origImgCol.b, 0.25f), t);
                        yield return null;
                    }

                    if (displayText != null)
                    {
                        displayText.text = "";
                        displayText.color = origTextCol;
                    }

                    if (displayImage != null)
                    {
                        displayImage.color = origImgCol;
                    }
                }
            }
        }

        yield return StartCoroutine(CrossfadeToNextSequence());
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        isLineTyping = true;
        fastForwardRequested = false;

        if (displayText != null)
        {
            displayText.text = "";
        }

        if (string.IsNullOrEmpty(fullText))
        {
            isLineTyping = false;
            yield break;
        }

        for (int i = 0; i < fullText.Length; i++)
        {
            if (isSkipping) break;

            // If player presses advance while typing, display full line instantly
            if (fastForwardRequested)
            {
                fastForwardRequested = false;
                if (displayText != null) displayText.text = fullText;
                break;
            }

            char c = fullText[i];
            if (displayText != null)
            {
                displayText.text += c;
            }

            // Typing blip audio (only for non-whitespace)
            if (!char.IsWhiteSpace(c) && audioSource != null && keystrokeSound != null)
            {
                audioSource.pitch = UnityEngine.Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(keystrokeSound, 0.20f);
            }

            // Undertale dramatic punctuation cadence
            if (c == '.' || c == '!' || c == '?' || c == ':' || c == '-')
            {
                // If part of an ellipsis ("..."), use a shorter suspenseful pause
                bool isEllipsis = (c == '.' && ((i + 1 < fullText.Length && fullText[i + 1] == '.') || (i > 0 && fullText[i - 1] == '.')));
                float pause = isEllipsis ? 0.20f : punctuationPause;
                yield return new WaitForSecondsRealtime(pause);
            }
            else if (c == ',' || c == ';')
            {
                yield return new WaitForSecondsRealtime(punctuationPause * 0.5f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(charTypeSpeed);
            }
        }

        isLineTyping = false;
    }

    public void SkipIntro()
    {
        if (isSkipping) return;
        isSkipping = true;

        if (cutsceneRoutine != null)
        {
            StopCoroutine(cutsceneRoutine);
        }

        StartCoroutine(CrossfadeToNextSequence());
    }

    private IEnumerator CrossfadeToNextSequence()
    {
        // 1. Activate next sequence (ShiftTitleCard) FIRST while cutscene is still fully solid black
        TransitionToNextSequence();

        // 2. Smoothly fade cutscene out over a brief 0.25s. Because ShiftTitleCard is already active
        // and solid black underneath, the office is NEVER flashed or exposed!
        if (cutsceneCanvasGroup != null)
        {
            float elapsed = 0f;
            float startAlpha = cutsceneCanvasGroup.alpha;
            while (elapsed < 0.25f)
            {
                elapsed += Time.unscaledDeltaTime;
                cutsceneCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / 0.25f);
                yield return null;
            }
            cutsceneCanvasGroup.alpha = 0f;
        }

        if (cutscenePanel != null)
        {
            cutscenePanel.SetActive(false);
        }
    }

    private void TransitionToNextSequence()
    {
        Debug.Log("[IntroCutscene] TransitionToNextSequence started.");

        if (hallwaySequence == null)
        {
            hallwaySequence = FindAnyObjectByType<HallwayArrivalSequence>(FindObjectsInactive.Include);
        }

        if (hallwaySequence != null)
        {
            Debug.Log("[IntroCutscene] Transitioning to HallwayArrivalSequence.");
            hallwaySequence.PlayHallwaySequence();
        }
        else
        {
            Debug.Log("[IntroCutscene] No hallway sequence, advancing directly to ShiftTitleCard.");
            PlayCurrentShiftTitleCard();
        }
    }

    private void PlayCurrentShiftTitleCard()
    {
        if (shiftTitleCard == null)
        {
            shiftTitleCard = FindAnyObjectByType<ShiftTitleCard>(FindObjectsInactive.Include);
        }

        if (shiftTitleCard == null)
        {
            ShiftTitleCard[] allCards = Resources.FindObjectsOfTypeAll<ShiftTitleCard>();
            if (allCards != null && allCards.Length > 0)
            {
                shiftTitleCard = allCards[0];
            }
        }

        if (shiftTitleCard == null)
        {
            GameObject cardObj = GameObject.Find("ShiftTitleCardPanel") ?? GameObject.Find("ShiftTitleCard");
            if (cardObj == null)
            {
                Canvas targetCanvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
                cardObj = new GameObject("ShiftTitleCardPanel");
                if (targetCanvas != null) cardObj.transform.SetParent(targetCanvas.transform, false);
            }
            shiftTitleCard = cardObj.GetComponent<ShiftTitleCard>();
            if (shiftTitleCard == null) shiftTitleCard = cardObj.AddComponent<ShiftTitleCard>();
        }

        if (shiftTitleCard != null)
        {
            GameManager.isCutscenePlaying = true;

            GameManager gm = GameManager.Instance;
            if (gm == null) gm = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);

            int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
            if (gm != null && gm.currentShiftIndex > 0)
            {
                currentShift = Mathf.Max(currentShift, gm.currentShiftIndex + 1);
            }

            string instruction = currentShift switch
            {
                1 => "Finish your quota. Leave when finished.",
                2 => "Archive status compromised. Type REBOOT to clear corrupted text.",
                3 => "Report any unapproved symbols immediately.",
                _ => "Finish your quota. Leave when finished."
            };

            Debug.Log($"[IntroCutscene] Playing ShiftTitleCard for shift {currentShift}.");
            shiftTitleCard.PlayCard(currentShift, instruction);
        }
        else
        {
            Debug.LogWarning("[IntroCutscene] ShiftTitleCard not found! Falling back to EyeOpeningTransition.");
            EyeOpeningTransition eye = FindAnyObjectByType<EyeOpeningTransition>(FindObjectsInactive.Include);
            if (eye != null)
            {
                eye.gameObject.SetActive(true);
                eye.PlayTransition();
            }
            else
            {
                GameManager.isCutscenePlaying = false;
            }
        }
    }

    private void InitializeDefaultSlidesIfEmpty()
    {
        // Replace if null, empty, incomplete, or contains legacy placeholder slide
        bool isLegacySingleSlide = (slides != null && slides.Length == 1 && !string.IsNullOrEmpty(slides[0].slideText) && slides[0].slideText.IndexOf("Ugh", StringComparison.OrdinalIgnoreCase) >= 0);
        if (slides != null && slides.Length >= 4 && !isLegacySingleSlide) return;

        // Auto-discover suitable retro illustrations
        Sprite hallwaySprite = FindSpriteByKeywords("hallway");
        Sprite introSprite = FindSpriteByKeywords("intro");
        Sprite behindSprite = FindSpriteByKeywords("officebehind");
        if (behindSprite == null) behindSprite = FindSpriteByKeywords("behind");

        Sprite breakerSprite = FindSpriteByKeywords("breaker room on");
        if (breakerSprite == null) breakerSprite = FindSpriteByKeywords("breaker room");
        if (breakerSprite == null) breakerSprite = hallwaySprite;

        slides = new CutsceneSlide[]
        {
            new CutsceneSlide
            {
                slideImage = hallwaySprite,
                slideText = "OCTOBER 1994.\n\nDeep beneath Sector 4 lies the classified archives of the Department of Metaphysical Sciences."
            },
            new CutsceneSlide
            {
                slideImage = (introSprite != null) ? introSprite : hallwaySprite,
                slideText = "Miles of magnetic tape. Thousands of redacted incident recordings.\n\nNight-shift transcribers work in total isolation, forbidden from questioning what they log."
            },
            new CutsceneSlide
            {
                slideImage = (behindSprite != null) ? behindSprite : hallwaySprite,
                slideText = "Late at night, the circuits decay.\nMonitors hum with voices found in no official log.\n\nIf you look behind you... you might catch them watching."
            },
            new CutsceneSlide
            {
                slideImage = (breakerSprite != null) ? breakerSprite : hallwaySprite,
                slideText = "Your shift begins at midnight.\n\nMeet your quota. Keep your eyes on the screen.\nAnd above all... do not let the lights go out."
            }
        };
    }

    private Sprite FindSpriteByKeywords(string kw1, string kw2 = null)
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var s in sprites)
        {
            if (s == null) continue;
            string n1 = s.name.ToLowerInvariant();
            string n2 = (s.texture != null) ? s.texture.name.ToLowerInvariant() : "";
            string k1 = kw1.ToLowerInvariant();

            bool match1 = n1.Contains(k1) || n2.Contains(k1);
            if (!match1) continue;

            if (kw2 != null)
            {
                string k2 = kw2.ToLowerInvariant();
                bool match2 = n1.Contains(k2) || n2.Contains(k2);
                if (!match2) continue;
            }

            return s;
        }
        return null;
    }

    public void EnsureIntroUI()
    {
        // 1. Locate root canvas
        Canvas targetCanvas = null;
        if (cutscenePanel != null)
        {
            targetCanvas = cutscenePanel.GetComponentInParent<Canvas>();
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

        // 2. Locate or create cutscene panel
        if (cutscenePanel == null)
        {
            GameObject existing = GameObject.Find("IntroCutscenePanel");
            if (existing == null) existing = GameObject.Find("IntroCutscene");
            if (existing == null) existing = GameObject.Find("CutscenePanel");
            if (existing != null) cutscenePanel = existing;
        }

        if (cutscenePanel == null && targetCanvas != null)
        {
            cutscenePanel = new GameObject("IntroCutscenePanel");
            cutscenePanel.transform.SetParent(targetCanvas.transform, false);
        }

        if (cutscenePanel == null) return;

        // Ensure cutscenePanel is at root of targetCanvas and rendered on top of everything
        if (targetCanvas != null)
        {
            if (cutscenePanel.transform.parent != targetCanvas.transform)
            {
                cutscenePanel.transform.SetParent(targetCanvas.transform, false);
            }
            cutscenePanel.transform.SetAsLastSibling();
        }

        // Configure Panel RectTransform to full stretch
        RectTransform panelRt = cutscenePanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.localScale = Vector3.one;
        }

        // Fullscreen solid black backdrop
        Image bg = cutscenePanel.GetComponent<Image>();
        if (bg == null) bg = cutscenePanel.AddComponent<Image>();
        bg.color = Color.black;

        // CanvasGroup for smooth fades
        if (cutsceneCanvasGroup == null)
        {
            cutsceneCanvasGroup = cutscenePanel.GetComponent<CanvasGroup>();
            if (cutsceneCanvasGroup == null) cutsceneCanvasGroup = cutscenePanel.AddComponent<CanvasGroup>();
        }
        if (cutsceneCanvasGroup != null)
        {
            cutsceneCanvasGroup.alpha = 1f;
            cutsceneCanvasGroup.interactable = true;
            cutsceneCanvasGroup.blocksRaycasts = true;
        }

        // Dedicated Canvas with override sorting matching targetCanvas sortingLayer
        Canvas panelCanvas = cutscenePanel.GetComponent<Canvas>();
        if (panelCanvas == null) panelCanvas = cutscenePanel.AddComponent<Canvas>();
        if (panelCanvas != null)
        {
            panelCanvas.overrideSorting = true;
            if (targetCanvas != null)
            {
                panelCanvas.sortingLayerID = targetCanvas.sortingLayerID;
                panelCanvas.sortingLayerName = targetCanvas.sortingLayerName;
            }
            else
            {
                panelCanvas.sortingLayerName = "UI";
            }
            panelCanvas.sortingOrder = 999;
        }

        GraphicRaycaster raycaster = cutscenePanel.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = cutscenePanel.AddComponent<GraphicRaycaster>();

        // 3. Configure Illustration Frame
        if (displayImage == null)
        {
            Transform imgTr = cutscenePanel.transform.Find("CutsceneImage");
            if (imgTr == null) imgTr = cutscenePanel.transform.Find("IllustrationFrame");
            if (imgTr != null) displayImage = imgTr.GetComponent<Image>();
        }

        if (displayImage == null)
        {
            Image[] imgs = cutscenePanel.GetComponentsInChildren<Image>(true);
            foreach (var img in imgs)
            {
                if (img != null && img.gameObject != cutscenePanel)
                {
                    displayImage = img;
                    break;
                }
            }
        }

        if (displayImage == null)
        {
            GameObject imgFrame = new GameObject("IllustrationFrame");
            imgFrame.transform.SetParent(cutscenePanel.transform, false);
            displayImage = imgFrame.AddComponent<Image>();
        }

        if (displayImage != null)
        {
            RectTransform imgRt = displayImage.rectTransform;
            imgRt.anchorMin = new Vector2(0.5f, 0.5f);
            imgRt.anchorMax = new Vector2(0.5f, 0.5f);
            imgRt.pivot = new Vector2(0.5f, 0.5f);
            imgRt.anchoredPosition = new Vector2(0f, 130f);
            imgRt.sizeDelta = new Vector2(680f, 390f);
            imgRt.localScale = Vector3.one;

            displayImage.preserveAspect = true;
            displayImage.color = new Color(0.96f, 0.96f, 0.98f, 1f);

            // Remove artificial outlines to match Undertale's clean raw illustration against black void
            Outline existingOutline = displayImage.GetComponent<Outline>();
            if (existingOutline != null)
            {
                if (Application.isPlaying) Destroy(existingOutline);
                else DestroyImmediate(existingOutline);
            }

            if (displayImage.sprite == null)
            {
                displayImage.gameObject.SetActive(false);
            }
        }

        // 4. Configure Narrative Text Box
        if (displayText == null)
        {
            Transform textTr = cutscenePanel.transform.Find("CutsceneText");
            if (textTr == null) textTr = cutscenePanel.transform.Find("TypewriterText");
            if (textTr != null) displayText = textTr.GetComponent<TextMeshProUGUI>();
        }

        if (displayText == null)
        {
            TextMeshProUGUI[] texts = cutscenePanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in texts)
            {
                if (t != null && t != skipPromptText && (skipPromptText == null || t.gameObject != skipPromptText.gameObject))
                {
                    displayText = t;
                    break;
                }
            }
        }

        if (displayText == null)
        {
            GameObject textObj = new GameObject("TypewriterText");
            textObj.transform.SetParent(cutscenePanel.transform, false);
            displayText = textObj.AddComponent<TextMeshProUGUI>();
        }

        if (displayText != null)
        {
            RectTransform textRt = displayText.rectTransform;
            textRt.anchorMin = new Vector2(0.5f, 0.5f);
            textRt.anchorMax = new Vector2(0.5f, 0.5f);
            textRt.pivot = new Vector2(0.5f, 0.5f);
            textRt.anchoredPosition = new Vector2(0f, -240f);
            textRt.sizeDelta = new Vector2(880f, 180f);
            textRt.localScale = Vector3.one;

            displayText.text = ""; // Clear any default/editor placeholder
            displayText.fontSize = 22;
            displayText.lineSpacing = 18f;
            displayText.paragraphSpacing = 16f;
            displayText.characterSpacing = 1.5f;
            displayText.margin = Vector4.zero;
            displayText.textWrappingMode = TextWrappingModes.Normal;
            displayText.alignment = TextAlignmentOptions.TopLeft;
            displayText.color = new Color(0.96f, 0.96f, 0.98f, 1f);
        }

        // 5. Configure Skip Prompt
        if (skipPromptText == null)
        {
            Transform skipTr = cutscenePanel.transform.Find("SkipPrompt");
            if (skipTr != null) skipPromptText = skipTr.GetComponent<TextMeshProUGUI>();
        }

        if (skipPromptText == null)
        {
            GameObject skipObj = new GameObject("SkipPrompt");
            skipObj.transform.SetParent(cutscenePanel.transform, false);
            skipPromptText = skipObj.AddComponent<TextMeshProUGUI>();
        }

        if (skipPromptText != null)
        {
            RectTransform skipRt = skipPromptText.rectTransform;
            skipRt.anchorMin = new Vector2(0.5f, 0.5f);
            skipRt.anchorMax = new Vector2(0.5f, 0.5f);
            skipRt.pivot = new Vector2(0.5f, 0.5f);
            skipRt.anchoredPosition = new Vector2(0f, -485f);
            skipRt.sizeDelta = new Vector2(600f, 30f);
            skipRt.localScale = Vector3.one;

            skipPromptText.text = "[ SPACE / ENTER ] ADVANCE   •   [ ESC ] SKIP";
            skipPromptText.fontSize = 11;
            skipPromptText.characterSpacing = 2.2f;
            skipPromptText.alignment = TextAlignmentOptions.Center;
            skipPromptText.color = new Color(0.5f, 0.6f, 0.55f, 0.55f);
        }

        // Propagate scene retro font
        if (displayText != null && displayText.font == null)
        {
            TMP_Text fontSample = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
            if (fontSample != null && fontSample.font != null)
            {
                displayText.font = fontSample.font;
                if (skipPromptText != null) skipPromptText.font = fontSample.font;
            }
        }
    }

    private void AutoDiscoverDependencies()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        if (keystrokeSound == null)
        {
            TypingSystem typingSys = FindAnyObjectByType<TypingSystem>(FindObjectsInactive.Include);
            if (typingSys != null && typingSys.keypressSound != null)
            {
                keystrokeSound = typingSys.keypressSound;
            }
        }

        if (keystrokeSound == null)
        {
            AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (var c in clips)
            {
                if (c != null && (c.name.IndexOf("keystroke", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  c.name.IndexOf("keypress", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    keystrokeSound = c;
                    break;
                }
            }
        }

        if (hallwaySequence == null)
        {
            hallwaySequence = FindAnyObjectByType<HallwayArrivalSequence>(FindObjectsInactive.Include);
        }

        if (shiftTitleCard == null)
        {
            shiftTitleCard = FindAnyObjectByType<ShiftTitleCard>(FindObjectsInactive.Include);
        }
    }
}