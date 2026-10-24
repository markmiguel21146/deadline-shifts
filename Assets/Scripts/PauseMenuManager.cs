using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Manages the ESC Pause Menu with full gameplay suspension,
/// retro CRT horror aesthetic, and clean scene navigation.
/// </summary>
public class PauseMenuManager : MonoBehaviour
{
    public static PauseMenuManager Instance { get; private set; }
    public static bool IsPaused { get; private set; } = false;

    [Header("UI References")]
    [Tooltip("Root panel for the pause menu. Auto-created if null.")]
    public GameObject pausePanel;
    public CanvasGroup pauseCanvasGroup;

    [Header("Buttons")]
    public Button resumeButton;
    public Button restartButton;
    public Button mainMenuButton;
    public Button quitButton;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip pauseSound;
    public AudioClip resumeSound;
    public AudioClip buttonClickSound;

    [Header("Settings")]
    public string mainMenuSceneName = "MainMenuScene";

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

        IsPaused = false;
        Time.timeScale = 1f;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        // Auto-discover keystroke sound for button clicks if not set
        if (buttonClickSound == null)
        {
            AudioClip[] allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (var clip in allClips)
            {
                if (clip.name.IndexOf("keystroke", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    buttonClickSound = clip;
                    break;
                }
            }
        }
    }

    private void Start()
    {
        EnsurePauseMenuUI();

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Do not pause if a cutscene or game over / victory is actively handling the screen
            if (GameManager.isCutscenePlaying ||
                (GameManager.Instance != null && (GameManager.Instance.IsGameOver || GameManager.Instance.IsVictory || GameManager.Instance.IsShiftCompleted)))
            {
                return;
            }

            TogglePause();
        }
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (IsPaused) return;

        IsPaused = true;
        Time.timeScale = 0f;

        EnsurePauseMenuUI();

        if (pausePanel != null)
        {
            pausePanel.SetActive(true);
            pausePanel.transform.SetAsLastSibling();
        }

        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = 1f;
            pauseCanvasGroup.blocksRaycasts = true;
            pauseCanvasGroup.interactable = true;
        }

        // Ensure system cursor is visible and interactive
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (CustomCursor.Instance != null)
        {
            CustomCursor.Instance.SetHover(false);
            CustomCursor.Instance.SetCursorVisible(false);
        }

        // Suspend terminal typing input so keyboard doesn't type into the input field while paused
        TypingSystem ts = FindAnyObjectByType<TypingSystem>(FindObjectsInactive.Include);
        if (ts != null)
        {
            ts.CanType = false;
            if (ts.typingInput != null)
            {
                ts.typingInput.DeactivateInputField();
                ts.typingInput.interactable = false;
            }
        }

        if (audioSource != null && pauseSound != null)
        {
            audioSource.PlayOneShot(pauseSound);
        }
        else if (audioSource != null && buttonClickSound != null)
        {
            audioSource.pitch = 0.8f;
            audioSource.PlayOneShot(buttonClickSound);
        }
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;

        if (pausePanel != null)
        {
            pausePanel.SetActive(false);
        }

        if (pauseCanvasGroup != null)
        {
            pauseCanvasGroup.alpha = 0f;
            pauseCanvasGroup.blocksRaycasts = false;
            pauseCanvasGroup.interactable = false;
        }

        // Restore custom in-game cursor
        Cursor.visible = false;
        if (CustomCursor.Instance != null)
        {
            CustomCursor.Instance.SetCursorVisible(true);
        }

        // Re-enable and refocus terminal typing input
        TypingSystem ts = FindAnyObjectByType<TypingSystem>(FindObjectsInactive.Include);
        if (ts != null)
        {
            ts.CanType = true;
            if (ts.typingInput != null)
            {
                ts.typingInput.interactable = true;
                ts.AutoRefocusInput(true);
            }
        }

        if (audioSource != null && resumeSound != null)
        {
            audioSource.PlayOneShot(resumeSound);
        }
        else if (audioSource != null && buttonClickSound != null)
        {
            audioSource.pitch = 1.1f;
            audioSource.PlayOneShot(buttonClickSound);
        }
    }

    public void RestartShift()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        // Skip intro sequence on restart for smooth pacing
        PlayerPrefs.SetInt("PlayIntroCutscene", 0);
        PlayerPrefs.Save();

        PlayButtonSound();

        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
    }

    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        IsPaused = false;

        PlayButtonSound();

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        PlayButtonSound();

        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    private void PlayButtonSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(buttonClickSound);
        }
    }

    public void EnsurePauseMenuUI()
    {
        if (pausePanel != null) return;

        // 1. Search existing in scene
        GameObject existing = GameObject.Find("PauseMenuPanel");
        if (existing == null) existing = GameObject.Find("PausePanel");

        if (existing != null)
        {
            pausePanel = existing;
            pauseCanvasGroup = existing.GetComponent<CanvasGroup>();
            if (pauseCanvasGroup == null) pauseCanvasGroup = existing.AddComponent<CanvasGroup>();
            WireExistingButtons(existing);
            return;
        }

        // 2. Find Canvas
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
        if (targetCanvas == null) return;

        // 3. Build Full-Screen CRT Horror Pause Menu
        pausePanel = new GameObject("PauseMenuPanel");
        pausePanel.transform.SetParent(targetCanvas.transform, false);

        RectTransform rootRt = pausePanel.AddComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        Image backdrop = pausePanel.AddComponent<Image>();
        backdrop.color = new Color(0.02f, 0.04f, 0.03f, 0.90f);

        pauseCanvasGroup = pausePanel.AddComponent<CanvasGroup>();

        // Center CRT Box Frame
        GameObject frameObj = new GameObject("PauseFrame");
        frameObj.transform.SetParent(pausePanel.transform, false);

        RectTransform frameRt = frameObj.AddComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0.5f, 0.5f);
        frameRt.anchorMax = new Vector2(0.5f, 0.5f);
        frameRt.pivot = new Vector2(0.5f, 0.5f);
        frameRt.sizeDelta = new Vector2(540f, 480f);

        Image frameBg = frameObj.AddComponent<Image>();
        frameBg.color = new Color(0.02f, 0.07f, 0.035f, 0.96f);

        Outline frameOutline = frameObj.AddComponent<Outline>();
        frameOutline.effectColor = new Color(0.25f, 0.9f, 0.45f, 0.85f);
        frameOutline.effectDistance = new Vector2(2f, -2f);

        // Header Title
        GameObject headerObj = new GameObject("HeaderTitle");
        headerObj.transform.SetParent(frameObj.transform, false);

        RectTransform headerRt = headerObj.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0.5f, 1f);
        headerRt.anchorMax = new Vector2(0.5f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -28f);
        headerRt.sizeDelta = new Vector2(500f, 58f);

        TextMeshProUGUI headerText = headerObj.AddComponent<TextMeshProUGUI>();
        headerText.text = "DEPARTMENT OF METAPHYSICAL SCIENCES\n<size=15><color=#55EE77>// OPERATION SUSPENDED //</color></size>";
        headerText.fontSize = 17;
        headerText.lineSpacing = 16f;
        headerText.characterSpacing = 2.0f;
        headerText.fontStyle = FontStyles.Bold;
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.color = new Color(0.3f, 1f, 0.5f, 1f);

        // Top Divider Line
        GameObject div1Obj = new GameObject("Divider1");
        div1Obj.transform.SetParent(frameObj.transform, false);
        RectTransform div1Rt = div1Obj.AddComponent<RectTransform>();
        div1Rt.anchorMin = new Vector2(0.5f, 1f);
        div1Rt.anchorMax = new Vector2(0.5f, 1f);
        div1Rt.pivot = new Vector2(0.5f, 1f);
        div1Rt.anchoredPosition = new Vector2(0f, -92f);
        div1Rt.sizeDelta = new Vector2(460f, 1.5f);
        Image div1Img = div1Obj.AddComponent<Image>();
        div1Img.color = new Color(0.2f, 0.8f, 0.4f, 0.45f);

        // Shift Status Info
        int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
        GameObject infoObj = new GameObject("StatusInfo");
        infoObj.transform.SetParent(frameObj.transform, false);

        RectTransform infoRt = infoObj.AddComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0.5f, 1f);
        infoRt.anchorMax = new Vector2(0.5f, 1f);
        infoRt.pivot = new Vector2(0.5f, 1f);
        infoRt.anchoredPosition = new Vector2(0f, -106f);
        infoRt.sizeDelta = new Vector2(480f, 24f);

        TextMeshProUGUI infoText = infoObj.AddComponent<TextMeshProUGUI>();
        infoText.text = $"[ TERMINAL 4 // SHIFT {currentShift} // STATUS: SUSPENDED ]";
        infoText.fontSize = 12;
        infoText.characterSpacing = 2.0f;
        infoText.alignment = TextAlignmentOptions.Center;
        infoText.color = new Color(0.65f, 0.9f, 0.7f, 0.92f);

        // Bottom Divider Line
        GameObject div2Obj = new GameObject("Divider2");
        div2Obj.transform.SetParent(frameObj.transform, false);
        RectTransform div2Rt = div2Obj.AddComponent<RectTransform>();
        div2Rt.anchorMin = new Vector2(0.5f, 1f);
        div2Rt.anchorMax = new Vector2(0.5f, 1f);
        div2Rt.pivot = new Vector2(0.5f, 1f);
        div2Rt.anchoredPosition = new Vector2(0f, -136f);
        div2Rt.sizeDelta = new Vector2(460f, 1.5f);
        Image div2Img = div2Obj.AddComponent<Image>();
        div2Img.color = new Color(0.2f, 0.8f, 0.4f, 0.35f);

        // Match scene fonts
        TMP_Text fontSample = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (fontSample != null && fontSample.font != null)
        {
            headerText.font = fontSample.font;
            infoText.font = fontSample.font;
        }

        // Create the 4 Pause Buttons
        resumeButton = CreatePauseButton(frameObj.transform, "ResumeButton", "[ RESUME TERMINAL (ESC) ]", new Vector2(0f, -158f), ResumeGame, fontSample?.font);
        restartButton = CreatePauseButton(frameObj.transform, "RestartButton", "[ RESTART SHIFT ]", new Vector2(0f, -220f), RestartShift, fontSample?.font);
        mainMenuButton = CreatePauseButton(frameObj.transform, "MainMenuButton", "[ MAIN MENU ]", new Vector2(0f, -282f), ReturnToMainMenu, fontSample?.font);
        quitButton = CreatePauseButton(frameObj.transform, "QuitButton", "[ QUIT GAME ]", new Vector2(0f, -344f), QuitGame, fontSample?.font);

        // Footer Prompt Hint
        GameObject footerObj = new GameObject("FooterHint");
        footerObj.transform.SetParent(frameObj.transform, false);
        RectTransform footerRt = footerObj.AddComponent<RectTransform>();
        footerRt.anchorMin = new Vector2(0.5f, 1f);
        footerRt.anchorMax = new Vector2(0.5f, 1f);
        footerRt.pivot = new Vector2(0.5f, 1f);
        footerRt.anchoredPosition = new Vector2(0f, -418f);
        footerRt.sizeDelta = new Vector2(440f, 20f);

        TextMeshProUGUI footerText = footerObj.AddComponent<TextMeshProUGUI>();
        footerText.text = "[ PRESS ESC TO RESUME ]";
        footerText.fontSize = 11;
        footerText.characterSpacing = 2.4f;
        footerText.alignment = TextAlignmentOptions.Center;
        footerText.color = new Color(0.35f, 0.7f, 0.45f, 0.65f);
        if (fontSample != null && fontSample.font != null) footerText.font = fontSample.font;

        pausePanel.SetActive(false);
    }

    private Button CreatePauseButton(Transform parent, string name, string label, Vector2 pos, UnityEngine.Events.UnityAction action, TMP_FontAsset font)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(440f, 46f);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.04f, 0.12f, 0.06f, 0.92f);

        Outline btnOutline = btnObj.AddComponent<Outline>();
        btnOutline.effectColor = new Color(0.2f, 0.75f, 0.35f, 0.7f);
        btnOutline.effectDistance = new Vector2(1.5f, -1.5f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
        colors.pressedColor = new Color(0.7f, 1f, 0.7f, 1f);
        btn.colors = colors;
        btn.onClick.AddListener(action);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 14;
        tmp.characterSpacing = 2.2f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.margin = new Vector4(12f, 2f, 12f, 2f);
        tmp.color = new Color(0.35f, 1f, 0.55f, 1f);

        if (font != null) tmp.font = font;

        return btn;
    }

    private void WireExistingButtons(GameObject panel)
    {
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons)
        {
            string n = b.gameObject.name.ToLowerInvariant();
            if (n.Contains("resume"))
            {
                resumeButton = b;
                b.onClick.RemoveListener(ResumeGame);
                b.onClick.AddListener(ResumeGame);
            }
            else if (n.Contains("restart") || n.Contains("retry"))
            {
                restartButton = b;
                b.onClick.RemoveListener(RestartShift);
                b.onClick.AddListener(RestartShift);
            }
            else if (n.Contains("menu") || n.Contains("main"))
            {
                mainMenuButton = b;
                b.onClick.RemoveListener(ReturnToMainMenu);
                b.onClick.AddListener(ReturnToMainMenu);
            }
            else if (n.Contains("quit") || n.Contains("exit"))
            {
                quitButton = b;
                b.onClick.RemoveListener(QuitGame);
                b.onClick.AddListener(QuitGame);
            }
        }
    }
}
