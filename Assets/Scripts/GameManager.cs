using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using TMPro;

/// <summary>
/// Central game coordinator for DEADLINE-SHIFTS.
/// Manages high-level game state, shift quotas, game over/victory sequences, and coordinates specialized subsystems.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static bool isCutscenePlaying = false;

    [Header("Subsystems (Auto-assigned if left empty)")]
    public TypingSystem typingSystem;
    public AtmosphereController atmosphereController;
    public LookBehindController lookBehindController;
    public StoryManager storyManager;

    [Header("Shift Management")]
    public ShiftCompletionManager shiftManager;
    public int currentShiftIndex = 0;
    private bool isShiftCompleted = false;

    [Header("Game Parameters")]
    [Tooltip("Target words for each night shift: Shift 1 = 90, Shift 2 = 130, Shift 3 = 150.")]
    public int[] shiftQuotas = new int[] { 90, 130, 150 };
    public int targetQuotaWords = 90;
    private int currentWordsTyped = 0;
    public float corruption = 0f;
    public float slackerMeter = 0f;
    private float gameTimer = 0f;

    [HideInInspector] public bool isLookingAway = false;
    private bool isRebooting = false;
    private bool isTextGlitched = false;
    private bool hasDiscoveredCorruption = false;

    [Header("UI - Quota & Stats Displays")]
    public TextMeshProUGUI quotaDisplay;
    public TMP_Text quotaDisplayText;
    public TMP_Text statsDisplay;
    public CanvasGroup corruptionGroup;
    public Slider slackerSlider;
    public GameObject rebootOverlay;

    [Header("UI - Victory / Shift Complete")]
    public CanvasGroup shiftCompleteCanvasGroup;
    public TMP_Text shiftCompleteText;
    public float victoryFadeDuration = 1.0f;
    [HideInInspector] public bool isVictory = false;

    [Header("UI - Game Over")]
    public GameObject gameOverPanel;
    public CanvasGroup gameOverCanvasGroup;
    public TMP_Text gameOverDialogueText;
    public Button gameOverMainMenuButton;
    public string mainMenuSceneName = "MainMenuScene";
    private bool isGameOver = false;

    [Header("Legacy/Inspector Passthrough Fields")]
    public TMP_Text headerDisplay;
    public TMP_Text promptDisplay;
    public TMP_InputField typingInput;
    public TextAsset phraseFile;
    public AudioSource typingAudioSource;
    public AudioClip keypressSound;
    public Light2D monitorLight;
    public Image vignetteImage;
    public AudioSource whisperAudioSource;
    [Range(0f, 100f)] public float vignetteStartThreshold = 30f;
    [Range(0f, 100f)] public float whisperStartThreshold = 50f;
    [Range(0f, 100f)] public float edgeLookThreshold = 0.15f;
    public CanvasGroup lookBehindCanvasGroup;
    public float lookTransitionSpeed = 8f;
    public CanvasGroup bossMonitorGroup;

    public static GameManager Instance { get; private set; }

    public bool IsGameOver => isGameOver;
    public bool IsVictory => isVictory;
    public bool IsShiftCompleted => isShiftCompleted;

    public float GetGameTimer() => gameTimer;
    public int GetCurrentWordsTyped() => currentWordsTyped;

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

        int savedShift = PlayerPrefs.GetInt("CurrentShift", 1);
#if UNITY_EDITOR
        // If currentShiftIndex was explicitly set in Inspector for testing (>0) and no progress was saved, sync it; otherwise respect savedShift
        if (currentShiftIndex > 0 && savedShift <= 1)
        {
            PlayerPrefs.SetInt("CurrentShift", currentShiftIndex + 1);
            PlayerPrefs.Save();
        }
        else
        {
            currentShiftIndex = Mathf.Max(0, savedShift - 1);
        }
#else
        currentShiftIndex = Mathf.Max(0, savedShift - 1);
#endif

        InitializeSubsystems();
    }

    private void InitializeSubsystems()
    {
        // 1. TypingSystem
        if (typingSystem == null)
        {
            typingSystem = GetComponent<TypingSystem>();
            if (typingSystem == null) typingSystem = gameObject.AddComponent<TypingSystem>();
        }
        if (typingSystem.headerDisplay == null) typingSystem.headerDisplay = headerDisplay;
        if (typingSystem.promptDisplay == null) typingSystem.promptDisplay = promptDisplay;
        if (typingSystem.typingInput == null) typingSystem.typingInput = typingInput;
        if (typingSystem.phraseFile == null) typingSystem.phraseFile = phraseFile;
        if (typingSystem.typingAudioSource == null) typingSystem.typingAudioSource = typingAudioSource;
        if (typingSystem.keypressSound == null) typingSystem.keypressSound = keypressSound;

        typingSystem.OnWordsCompleted += HandleWordsCompleted;
        typingSystem.OnRebootRequested += HandleRebootRequested;

        // 2. AtmosphereController
        if (atmosphereController == null)
        {
            atmosphereController = GetComponent<AtmosphereController>();
            if (atmosphereController == null) atmosphereController = gameObject.AddComponent<AtmosphereController>();
        }
        if (atmosphereController.monitorLight == null) atmosphereController.monitorLight = monitorLight;
        if (atmosphereController.vignetteImage == null) atmosphereController.vignetteImage = vignetteImage;
        if (atmosphereController.whisperAudioSource == null) atmosphereController.whisperAudioSource = whisperAudioSource;
        atmosphereController.vignetteStartThreshold = vignetteStartThreshold;
        atmosphereController.whisperStartThreshold = whisperStartThreshold;

        // 3. LookBehindController
        if (lookBehindController == null)
        {
            lookBehindController = GetComponent<LookBehindController>();
            if (lookBehindController == null) lookBehindController = gameObject.AddComponent<LookBehindController>();
        }
        if (lookBehindController.lookBehindCanvasGroup == null) lookBehindController.lookBehindCanvasGroup = lookBehindCanvasGroup;
        if (lookBehindController.bossMonitorGroup == null) lookBehindController.bossMonitorGroup = bossMonitorGroup;
        lookBehindController.edgeLookThreshold = edgeLookThreshold;
        lookBehindController.lookTransitionSpeed = lookTransitionSpeed;

        lookBehindController.OnLookBehindChanged += HandleLookBehindChanged;

        // 4. ShiftCompletionManager (find even if GameObject is inactive in hierarchy)
        if (shiftManager == null)
        {
            shiftManager = FindAnyObjectByType<ShiftCompletionManager>(FindObjectsInactive.Include);
        }

        // 5. StoryManager (auto-discover if present)
        if (storyManager == null)
        {
            storyManager = FindAnyObjectByType<StoryManager>(FindObjectsInactive.Include);
        }

        // 6. PauseMenuManager (auto-discover or instantiate)
        if (PauseMenuManager.Instance == null && FindAnyObjectByType<PauseMenuManager>(FindObjectsInactive.Include) == null)
        {
            GameObject pmm = new GameObject("PauseMenuManager");
            pmm.AddComponent<PauseMenuManager>();
        }
    }

    private void Start()
    {
        int savedShift = PlayerPrefs.GetInt("CurrentShift", 1);
        if (currentShiftIndex > 0 && savedShift == 1)
        {
            // Inspector was explicitly set for testing Shift 2 or later; sync PlayerPrefs
            PlayerPrefs.SetInt("CurrentShift", currentShiftIndex + 1);
            PlayerPrefs.Save();
        }
        else
        {
            currentShiftIndex = Mathf.Max(0, savedShift - 1);
        }

        if (shiftQuotas != null && shiftQuotas.Length > 0)
        {
            int quotaIndex = Mathf.Clamp(currentShiftIndex, 0, shiftQuotas.Length - 1);
            targetQuotaWords = shiftQuotas[quotaIndex];
        }

        EnsureBottomHUDLayout();
        UpdateQuotaDisplay();
        UpdateStatsDisplay();

        if (slackerSlider != null)
        {
            slackerSlider.value = 0f;
        }

        if (bossMonitorGroup != null)
        {
            bossMonitorGroup.alpha = 0f;
            bossMonitorGroup.gameObject.SetActive(false);
        }

        if (isCutscenePlaying && typingSystem != null)
        {
            typingSystem.DeactivateInputCaret();
        }
    }

    private void Update()
    {
        if (isCutscenePlaying || isRebooting || isShiftCompleted || isGameOver || PauseMenuManager.IsPaused) return;

        bool isBlackout = (EnvironmentalEventManager.Instance != null && EnvironmentalEventManager.Instance.IsAnyEventActive());
        string currentLoc = LocationManager.Instance != null ? LocationManager.Instance.CurrentLocationId : "Office";
        bool inOfficeBehind = currentLoc.Equals("OfficeBehind", System.StringComparison.OrdinalIgnoreCase);
        bool inBreaker = currentLoc.Equals("BreakerRoom", System.StringComparison.OrdinalIgnoreCase) || currentLoc.Equals("BreakerCloseUp", System.StringComparison.OrdinalIgnoreCase);
        bool isAtDesk = currentLoc.Equals("Office", System.StringComparison.OrdinalIgnoreCase);

        // 1. Advance Game Time (only when actively at desk and not engaged in environmental investigation)
        if (isAtDesk && !isBlackout)
        {
            gameTimer += Time.deltaTime;
        }

        // 2. Handle Look-Behind Input & State (allowed at desk and in OfficeBehind, blocked during blackout/breaker room)
        bool canLookBehind = !isGameOver && !isRebooting && !isBlackout && !inBreaker;
        lookBehindController.HandleLookBehindInput(canLookBehind);
        isLookingAway = lookBehindController.IsLookingAway || inOfficeBehind;

        // 3. Update Corruption & Slacker Meters
        if (isBlackout || inBreaker)
        {
            // Do NOT penalize the player with the slacker meter during blackouts or puzzle investigation!
            slackerMeter = Mathf.Max(0f, slackerMeter - (Time.deltaTime * 5f));
        }
        else if (isLookingAway)
        {
            corruption = Mathf.Max(0f, corruption - (Time.deltaTime * 15f));
            slackerMeter += Time.deltaTime * 6f;
        }
        else
        {
            slackerMeter = Mathf.Max(0f, slackerMeter - (Time.deltaTime * 5f));
            float multiplier = (corruption >= 40f) ? 2.5f : 1.0f;
            corruption += Time.deltaTime * 0.3f * multiplier;
        }

        corruption = Mathf.Clamp(corruption, 0f, 100f);
        slackerMeter = Mathf.Clamp(slackerMeter, 0f, 100f);

        if (slackerSlider != null)
        {
            slackerSlider.value = slackerMeter / 100f;
        }

        if (LocationManager.Instance != null)
        {
            LocationManager.Instance.UpdateOfficeBehindProductivityMeter(slackerMeter);
            LocationManager.Instance.UpdateOfficeDeskProductivityMeter(slackerMeter);
        }

        // 4. Trigger Visual Glitch at 40% Corruption
        if (corruption >= 40f && !isTextGlitched)
        {
            isTextGlitched = true;
            typingSystem.GenerateGlitchedVersion(corruption);

            if (!hasDiscoveredCorruption)
            {
                hasDiscoveredCorruption = true;
                if (corruptionGroup != null)
                {
                    StartCoroutine(FadeInCanvasGroup(corruptionGroup));
                }
            }
        }

        // 5. Update Atmosphere FX
        if (atmosphereController != null)
        {
            atmosphereController.UpdateAtmosphere(corruption);
        }

        // 6. Update HUD Stats & Quota in Real Time
        UpdateStatsDisplay();
        UpdateQuotaDisplay();

        // 7. Auto Refocus Typing Caret
        if (typingSystem != null)
        {
            typingSystem.AutoRefocusInput(!isLookingAway && !isRebooting && !isGameOver);
        }

        // 8. Check Defeat & Victory States
        CheckGameStates();
    }

    private void HandleWordsCompleted(int wordsInPhrase)
    {
        currentWordsTyped += wordsInPhrase;
        UpdateQuotaDisplay();
        UpdateStatsDisplay();

        if (currentWordsTyped >= targetQuotaWords)
        {
            TriggerVictory();
        }
        else
        {
            isTextGlitched = false;

            if (storyManager != null && storyManager.IsStoryActive)
            {
                storyManager.OnDocumentCompleted();
            }
            else
            {
                typingSystem.GenerateNewPhrase();
            }
        }
    }

    private void HandleRebootRequested()
    {
        StartCoroutine(RebootTerminal());
    }

    private void HandleLookBehindChanged(bool lookingAway)
    {
        isLookingAway = lookingAway;
        if (typingSystem != null)
        {
            typingSystem.ShowLookingAwayPrompt(lookingAway);
        }
    }

    public float GetWPM()
    {
        if (gameTimer < 1.0f) return 0f;
        float elapsedMin = gameTimer / 60f;

        // Real-time calculation: full completed words PLUS real-time characters typed in active document
        float partialWords = 0f;
        if (typingSystem != null && typingSystem.typingInput != null)
        {
            // Standard typing standard: 5 characters = 1 word
            partialWords = typingSystem.typingInput.text.Length / 5.0f;
        }

        float totalWords = currentWordsTyped + partialWords;
        return totalWords / elapsedMin;
    }

    public string GetFormattedShiftDuration()
    {
        int hours = Mathf.FloorToInt(gameTimer / 3600f);
        int minutes = Mathf.FloorToInt((gameTimer % 3600f) / 60f);
        int seconds = Mathf.FloorToInt(gameTimer % 60f);
        return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
    }

    public void EnsureBottomHUDLayout()
    {
        // 1. Quota Display (Bottom-Left)
        TMP_Text qText = quotaDisplayText ?? quotaDisplay;
        if (qText != null)
        {
            RectTransform qRt = qText.rectTransform;
            Canvas parentCanvas = qRt.GetComponentInParent<Canvas>();
            bool isDirectCanvasChild = qRt.parent == (parentCanvas != null ? parentCanvas.transform : null);

            if (isDirectCanvasChild)
            {
                qRt.anchorMin = new Vector2(0f, 0f);
                qRt.anchorMax = new Vector2(0f, 0f);
                qRt.pivot = new Vector2(0f, 0f);
                qRt.anchoredPosition = new Vector2(25f, 25f);
            }
            else
            {
                // Parented to 1_GameplayHUD (centered at 0, 0 in reference 1920x1080 canvas)
                qRt.anchorMin = new Vector2(0.5f, 0.5f);
                qRt.anchorMax = new Vector2(0.5f, 0.5f);
                qRt.pivot = new Vector2(0f, 0f);
                qRt.anchoredPosition = new Vector2(-935f, -515f);
            }
            qRt.sizeDelta = new Vector2(560f, 52f);

            qText.alignment = TextAlignmentOptions.BottomLeft;
            qText.fontSize = 24f;
            qText.characterSpacing = 0.8f;
            qText.fontStyle = FontStyles.Bold;
            qText.textWrappingMode = TextWrappingModes.NoWrap;
            qText.overflowMode = TextOverflowModes.Ellipsis;
        }

        // 2. Stats & Corruption Display (Bottom-Right)
        if (statsDisplay != null)
        {
            RectTransform sRt = statsDisplay.rectTransform;
            Canvas parentCanvas = sRt.GetComponentInParent<Canvas>();
            bool isDirectCanvasChild = sRt.parent == (parentCanvas != null ? parentCanvas.transform : null);

            if (isDirectCanvasChild)
            {
                sRt.anchorMin = new Vector2(1f, 0f);
                sRt.anchorMax = new Vector2(1f, 0f);
                sRt.pivot = new Vector2(1f, 0f);
                sRt.anchoredPosition = new Vector2(-35f, 25f);
            }
            else
            {
                // Parented to CorruptionGroup or 1_GameplayHUD (centered at 0, 0 in reference 1920x1080 canvas)
                sRt.anchorMin = new Vector2(0.5f, 0.5f);
                sRt.anchorMax = new Vector2(0.5f, 0.5f);
                sRt.pivot = new Vector2(1f, 0f);
                sRt.anchoredPosition = new Vector2(925f, -515f);
            }
            sRt.sizeDelta = new Vector2(560f, 52f);

            statsDisplay.alignment = TextAlignmentOptions.BottomRight;
            statsDisplay.fontSize = 24f;
            statsDisplay.characterSpacing = 0.8f;
            statsDisplay.fontStyle = FontStyles.Bold;
            statsDisplay.textWrappingMode = TextWrappingModes.NoWrap;
            statsDisplay.overflowMode = TextOverflowModes.Overflow;
        }

        // 3. Look Behind Prompt (Bottom-Center)
        if (lookBehindController != null)
        {
            lookBehindController.PositionPromptBottomCenter();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) EnsureBottomHUDLayout();
        };
    }
#endif

    public void UpdateQuotaDisplay()
    {
        int wpm = Mathf.RoundToInt(GetWPM());
        string text = (wpm > 0 || gameTimer >= 2f)
            ? $"QUOTA: {currentWordsTyped}/{targetQuotaWords} WORDS | {wpm} WPM"
            : $"QUOTA: {currentWordsTyped}/{targetQuotaWords} WORDS";
        if (quotaDisplay != null) quotaDisplay.text = text;
        if (quotaDisplayText != null) quotaDisplayText.text = text;
    }

    private void UpdateStatsDisplay()
    {
        if (statsDisplay == null) return;

        int rounded = Mathf.RoundToInt(corruption);
        if (corruption >= 75f)
        {
            statsDisplay.text = $"<color=#FF4444>CORRUPTION: {rounded}% [CRITICAL // TYPE REBOOT]</color>";
        }
        else if (corruption >= 40f)
        {
            statsDisplay.text = $"<color=#FFFF33>CORRUPTION: {rounded}% [TYPE REBOOT]</color>";
        }
        else if (corruption >= 20f)
        {
            statsDisplay.text = $"<color=#FFCC00>CORRUPTION: {rounded}%</color>";
        }
        else
        {
            statsDisplay.text = $"<color=#A0B0A8>CORRUPTION: {rounded}%</color>";
        }
    }

    private IEnumerator RebootTerminal()
    {
        isRebooting = true;
        isLookingAway = false;

        if (typingSystem != null)
        {
            typingSystem.SetTypingActive(false);
        }

        if (rebootOverlay != null)
        {
            rebootOverlay.SetActive(true);
        }

        float elapsed = 0f;
        while (elapsed < 4f)
        {
            elapsed += Time.deltaTime;
            slackerMeter = Mathf.Clamp(slackerMeter + (Time.deltaTime * 5f), 0f, 100f);
            if (slackerSlider != null) slackerSlider.value = slackerMeter / 100f;
            yield return null;
        }

        corruption = 0f;
        isTextGlitched = false;

        if (atmosphereController != null)
        {
            atmosphereController.ResetAtmosphere();
        }

        if (rebootOverlay != null)
        {
            rebootOverlay.SetActive(false);
        }

        if (typingSystem != null)
        {
            typingSystem.SetTypingActive(true);
            typingSystem.ResetTypingTextColor();

            // Fix reboot: if StoryManager is active, restore the current story document!
            // Do NOT call GenerateNewPhrase(), which overrides with random Shift 1 phrases!
            if (storyManager != null && storyManager.IsStoryActive && storyManager.CurrentDocument != null)
            {
                typingSystem.LoadSpecificPhrase(storyManager.CurrentDocument.headerTitle, storyManager.CurrentDocument.documentText);
            }
            else
            {
                typingSystem.GenerateNewPhrase();
            }
        }

        isRebooting = false;
    }

    private void CheckGameStates()
    {
        if (isGameOver || isVictory) return;

        if (currentWordsTyped >= targetQuotaWords)
        {
            TriggerVictory();
            return;
        }

        // Slacker Timeout Game Over
        if (slackerMeter >= 100f)
        {
            TriggerGameOver("\"Camera feed 04 confirms prolonged idle behavior. Your personal effects have already been incinerated. Turn in your clearance badge to security immediately. You are terminated.\"");
        }
        // 6:00 AM Shift Time Limit Game Over (360s)
        else if (gameTimer >= 360f && currentWordsTyped < targetQuotaWords)
        {
            TriggerGameOver("\"Shift complete: 06:00. Production quota failed. Department margins do not accommodate dead weight. Do not return on tomorrow. Your final deposit has been withheld for operational losses.\"");
        }
    }

    public void TriggerVictory()
    {
        if (isVictory || isGameOver) return;
        isVictory = true;
        isShiftCompleted = true;

        if (typingSystem != null)
        {
            typingSystem.SetTypingActive(false);
        }

        if (lookBehindController != null)
        {
            if (lookBehindController.IsLookingAway)
            {
                lookBehindController.LookBackAtMonitor();
            }
            lookBehindController.SetPromptVisible(false);
        }

        if (shiftManager == null)
        {
            shiftManager = FindAnyObjectByType<ShiftCompletionManager>(FindObjectsInactive.Include);
        }

        if (shiftManager != null)
        {
            // Activate GameObject first so coroutines can run
            shiftManager.gameObject.SetActive(true);
            shiftManager.CompleteShift(currentShiftIndex);
        }
        else
        {
            StartCoroutine(FadeInVictory());
        }
    }

    private IEnumerator FadeInVictory()
    {
        if (shiftCompleteCanvasGroup == null)
        {
            shiftCompleteCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
            if (shiftCompleteCanvasGroup == null) yield break;
        }

        shiftCompleteCanvasGroup.gameObject.SetActive(true);
        shiftCompleteCanvasGroup.blocksRaycasts = true;
        shiftCompleteCanvasGroup.interactable = true;

        float elapsed = 0f;
        float startAlpha = shiftCompleteCanvasGroup.alpha;

        while (elapsed < victoryFadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            shiftCompleteCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / victoryFadeDuration);
            yield return null;
        }

        shiftCompleteCanvasGroup.alpha = 1f;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void TriggerGameOver(string dialogue)
    {
        isGameOver = true;

        if (typingSystem != null)
        {
            typingSystem.SetTypingActive(false);
        }

        if (lookBehindController != null)
        {
            lookBehindController.SetPromptVisible(false);
        }

        if (gameOverPanel != null)
        {
            Canvas goCanvas = gameOverPanel.GetComponent<Canvas>();
            if (goCanvas == null) goCanvas = gameOverPanel.AddComponent<Canvas>();
            goCanvas.overrideSorting = true;
            Canvas rootCanvas = GetComponentInParent<Canvas>() ?? FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
            if (rootCanvas != null)
            {
                goCanvas.sortingLayerID = rootCanvas.sortingLayerID;
                goCanvas.sortingLayerName = rootCanvas.sortingLayerName;
            }
            else
            {
                goCanvas.sortingLayerName = "UI";
            }
            goCanvas.sortingOrder = 999;

            if (gameOverPanel.GetComponent<GraphicRaycaster>() == null)
            {
                gameOverPanel.AddComponent<GraphicRaycaster>();
            }

            gameOverPanel.SetActive(true);
            gameOverPanel.transform.SetAsLastSibling();
            if (gameOverPanel.transform.parent != null)
            {
                gameOverPanel.transform.parent.SetAsLastSibling();
            }
        }

        if (gameOverDialogueText != null)
        {
            gameOverDialogueText.text = dialogue;
        }

        // Restore mouse cursor for lose screen
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureGameOverMainMenuButton();

        StartCoroutine(FadeInGameOver());
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        UnityEngine.SceneManagement.SceneManager.LoadScene(mainMenuSceneName);
    }

    private void EnsureGameOverMainMenuButton()
    {
        if (gameOverPanel == null) return;

        if (gameOverMainMenuButton == null)
        {
            Button[] buttons = gameOverPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                string n = b.gameObject.name.ToLower();
                if (n.Contains("menu") || n.Contains("quit") || n.Contains("exit") || n.Contains("title"))
                {
                    gameOverMainMenuButton = b;
                    break;
                }
            }
        }

        if (gameOverMainMenuButton == null)
        {
            // Dynamically create a prominent, retro-styled Main Menu button at bottom center of the game over screen
            GameObject btnObj = new GameObject("GameOverMainMenuButton");
            btnObj.transform.SetParent(gameOverPanel.transform, false);
            btnObj.transform.SetAsLastSibling();

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 50f);
            rt.sizeDelta = new Vector2(340f, 48f);

            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
            img.raycastTarget = true;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.58f, 0.62f, 0.85f);
            outline.effectDistance = new Vector2(1f, 1f);

            gameOverMainMenuButton = btnObj.AddComponent<Button>();
            ColorBlock cb = gameOverMainMenuButton.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.85f, 0.88f, 0.92f, 1f);
            cb.pressedColor = new Color(0.5f, 0.52f, 0.55f, 1f);
            gameOverMainMenuButton.colors = cb;
            gameOverMainMenuButton.interactable = true;

            GameObject textObj = new GameObject("ButtonText");
            textObj.transform.SetParent(btnObj.transform, false);
            RectTransform trt = textObj.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "[ RETURN TO MAIN MENU ]";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 18;
            tmp.characterSpacing = 1.0f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = new Color(0.92f, 0.94f, 0.96f, 1f);
            tmp.raycastTarget = false;
            if (gameOverDialogueText != null && gameOverDialogueText.font != null)
            {
                tmp.font = gameOverDialogueText.font;
            }
        }

        if (gameOverMainMenuButton != null)
        {
            RectTransform rt = gameOverMainMenuButton.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0f);
                rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, 50f);
                rt.sizeDelta = new Vector2(340f, 48f);
            }
            gameOverMainMenuButton.transform.SetAsLastSibling();
            gameOverMainMenuButton.interactable = true;
            gameOverMainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            gameOverMainMenuButton.onClick.AddListener(ReturnToMainMenu);
            gameOverMainMenuButton.gameObject.SetActive(true);
        }
    }

    private IEnumerator FadeInGameOver()
    {
        if (gameOverCanvasGroup == null) yield break;

        gameOverCanvasGroup.blocksRaycasts = true;
        gameOverCanvasGroup.interactable = true;

        float elapsed = 0f;
        float duration = 1.0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            gameOverCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        gameOverCanvasGroup.alpha = 1f;
        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
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

    private void OnDestroy()
    {
        if (typingSystem != null)
        {
            typingSystem.OnWordsCompleted -= HandleWordsCompleted;
            typingSystem.OnRebootRequested -= HandleRebootRequested;
        }

        if (lookBehindController != null)
        {
            lookBehindController.OnLookBehindChanged -= HandleLookBehindChanged;
        }
    }
}