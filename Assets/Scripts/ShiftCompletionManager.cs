using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class ShiftCompletionManager : MonoBehaviour
{
    [System.Serializable]
    public struct ShiftData
    {
        public string titleText;
        [TextArea(2, 4)] public string subtitleText;
        public TMP_FontAsset titleFont;     
        public TMP_FontAsset subtitleFont;  
        public AudioClip alarmSound;    
        public AudioClip victorySound;  
        public Color textColor;
    }

    [Header("UI References")]
    [SerializeField] private GameObject endScreenPanel;
    [SerializeField] private CanvasGroup endScreenCanvasGroup;
    [SerializeField] private TextMeshProUGUI titleTextUI;
    [SerializeField] private TextMeshProUGUI subtitleTextUI;
    [SerializeField] private TextMeshProUGUI continueTextUI;

    [Header("Statistics UI (Optional)")]
    [Tooltip("Consolidated classified dossier report box")]
    [SerializeField] private TextMeshProUGUI statsReportTextUI;
    [SerializeField] private TextMeshProUGUI quotaStatsTextUI;
    [SerializeField] private TextMeshProUGUI corruptionStatsTextUI;
    [SerializeField] private TextMeshProUGUI timeStatsTextUI;

    [Header("Interactive Buttons (Optional)")]
    [SerializeField] private Button nextShiftButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [Header("UI Cleanup on Victory (Optional)")]
    [Tooltip("Drag '1_GameplayHUD' here to automatically hide it when victory appears.")]
    [SerializeField] private GameObject gameplayHUD;

    [Header("Elements to Disable on Victory")]
    [SerializeField] private GameObject[] objectsToDisable;

    [Header("Audio & Effects")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private ParticleSystem confettiParticles;

    [Header("Shift Configurations")]
    [SerializeField] private ShiftData[] shiftVariations;

    [Header("Dependencies")]
    [SerializeField] private GameManager gameManager;

    private bool canContinue = false;

    private void Awake()
    {
        AutoDiscoverReferences();
        HookUpButtons();
    }

    private void OnEnable()
    {
        HookUpButtons();
    }

    private void AutoDiscoverReferences()
    {
        if (endScreenPanel == null)
        {
            endScreenPanel = gameObject;
        }

        if (endScreenCanvasGroup == null)
        {
            endScreenCanvasGroup = GetComponent<CanvasGroup>();
            if (endScreenCanvasGroup == null) endScreenCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = GetComponentInChildren<AudioSource>(true);
        }

        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        }

        if (gameplayHUD == null)
        {
            gameplayHUD = GameObject.Find("1_GameplayHUD") ?? GameObject.Find("GameplayHUD");
        }

        // Auto-discover text components if unassigned
        if (titleTextUI == null || subtitleTextUI == null || statsReportTextUI == null)
        {
            TextMeshProUGUI[] tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                string n = t.gameObject.name.ToLower();
                if (titleTextUI == null && n.Contains("title")) titleTextUI = t;
                else if (subtitleTextUI == null && (n.Contains("sub") || n.Contains("desc"))) subtitleTextUI = t;
                else if (statsReportTextUI == null && (n.Contains("stat") || n.Contains("report") || n.Contains("dossier"))) statsReportTextUI = t;
            }
        }

        // Auto-discover buttons if unassigned
        if (nextShiftButton == null || restartButton == null || mainMenuButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (var b in buttons)
            {
                string n = b.gameObject.name.ToLower();
                if (nextShiftButton == null && (n.Contains("next") || n.Contains("continue") || n.Contains("shift")))
                    nextShiftButton = b;
                else if (restartButton == null && (n.Contains("restart") || n.Contains("retry") || n.Contains("again")))
                    restartButton = b;
                else if (mainMenuButton == null && (n.Contains("menu") || n.Contains("title") || n.Contains("quit") || n.Contains("exit")))
                    mainMenuButton = b;
            }
        }
    }

    private void HookUpButtons()
    {
        if (nextShiftButton != null)
        {
            nextShiftButton.onClick.RemoveListener(AdvanceToNextShift);
            nextShiftButton.onClick.AddListener(AdvanceToNextShift);
        }

        if (restartButton != null)
        {
            // Per request: make Restart Shift button navigate to Main Menu
            TextMeshProUGUI btnText = restartButton.GetComponentInChildren<TextMeshProUGUI>(true);
            if (btnText != null)
            {
                btnText.text = "[ MAIN MENU ]";
            }

            restartButton.onClick.RemoveListener(ReturnToMainMenu);
            restartButton.onClick.RemoveListener(RestartCurrentShift);
            restartButton.onClick.AddListener(ReturnToMainMenu);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            mainMenuButton.onClick.AddListener(ReturnToMainMenu);
        }
    }

    private void Update()
    {
        // Keyboard fallback to advance when continue prompt is active
        if (canContinue)
        {
            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
            {
                AdvanceToNextShift();
            }
        }
    }

    public void CompleteShift(int shiftIndex)
    {
        // 1. Ensure this GameObject and the end screen panel are active so Coroutines run
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        if (endScreenPanel != null && !endScreenPanel.activeSelf)
        {
            endScreenPanel.SetActive(true);
        }

        AutoDiscoverReferences();
        HookUpButtons();

        StartCoroutine(PlayShiftCompleteSequence(shiftIndex));
    }

    private ShiftData GetShiftData(int shiftIndex)
    {
        ShiftData data = new ShiftData();

        // Enforce the authentic Shift 1 victory panel across ALL shifts throughout the entire game
        if (shiftVariations != null && shiftVariations.Length > 0)
        {
            data = shiftVariations[0];
        }

        if (string.IsNullOrEmpty(data.titleText))
        {
            data.titleText = "SHIFT COMPLETED";
        }

        if (string.IsNullOrEmpty(data.subtitleText))
        {
            data.subtitleText = "You can now go home and rest.\nBe ready for tomorrow!";
        }

        if (data.textColor.a <= 0f) data.textColor = Color.white;
        return data;
    }

    private IEnumerator PlayShiftCompleteSequence(int shiftIndex)
    {
        EnsureVictoryScreenLayout();
        ShiftData currentShift = GetShiftData(shiftIndex);

        // 1. Hide Gameplay HUD & World Interactions to prevent overlap
        if (gameplayHUD != null)
        {
            gameplayHUD.SetActive(false);
        }

        if (objectsToDisable != null)
        {
            foreach (GameObject obj in objectsToDisable)
            {
                if (obj != null) obj.SetActive(false);
            }
        }

        // 2. Restore Mouse Cursor so buttons are clickable
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 3. Populate Statistics from GameManager
        PopulateStatistics(shiftIndex);

        // 4. Set Shift Title & Subtitle
        if (titleTextUI != null)
        {
            titleTextUI.gameObject.SetActive(true);
            if (currentShift.titleFont != null) titleTextUI.font = currentShift.titleFont;
            titleTextUI.text = currentShift.titleText;
            
            if (currentShift.textColor.a > 0f)
            {
                titleTextUI.color = currentShift.textColor;
            }
        }

        if (subtitleTextUI != null)
        {
            subtitleTextUI.gameObject.SetActive(true);
            if (currentShift.subtitleFont != null) subtitleTextUI.font = currentShift.subtitleFont;
            subtitleTextUI.text = currentShift.subtitleText;
        }

        // 5. Play Alarm Sound
        if (currentShift.alarmSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(currentShift.alarmSound);
        }

        // 6. Fade In Victory Canvas
        if (endScreenPanel != null) endScreenPanel.SetActive(true);
        if (endScreenCanvasGroup != null)
        {
            endScreenCanvasGroup.alpha = 0f;
            endScreenCanvasGroup.blocksRaycasts = true;
            endScreenCanvasGroup.interactable = true;
        }

        float fadeDuration = 1.5f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (endScreenCanvasGroup != null)
            {
                endScreenCanvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / fadeDuration);
            }
            yield return null;
        }

        if (endScreenCanvasGroup != null)
        {
            endScreenCanvasGroup.alpha = 1f;
        }

        // 7. Freeze Game Physics
        Time.timeScale = 0f;

        // 8. Play Victory Sound
        if (currentShift.victorySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(currentShift.victorySound);
        }

        // 9. Trigger Particles
        if (confettiParticles != null)
        {
            var mainModule = confettiParticles.main;
            mainModule.useUnscaledTime = true;
            confettiParticles.Stop();
            confettiParticles.Play();
        }

        // 10. Display Continue Prompt
        if (continueTextUI != null)
        {
            continueTextUI.gameObject.SetActive(true);
            StartCoroutine(BlinkContinueText());
        }

        canContinue = true;
    }

    private void PopulateStatistics(int shiftIndex)
    {
        // Per user request: The authentic Shift 1 victory panel does not display a consolidated stats dossier box.
        // Keep statsReportTextUI disabled so no runtime card covers the screen.
        if (statsReportTextUI != null)
        {
            statsReportTextUI.gameObject.SetActive(false);
        }

        if (quotaStatsTextUI != null) quotaStatsTextUI.gameObject.SetActive(false);
        if (corruptionStatsTextUI != null) corruptionStatsTextUI.gameObject.SetActive(false);
        if (timeStatsTextUI != null) timeStatsTextUI.gameObject.SetActive(false);
    }

    private IEnumerator BlinkContinueText()
    {
        while (canContinue)
        {
            if (continueTextUI != null) continueTextUI.alpha = 0f;
            yield return new WaitForSecondsRealtime(0.5f);
            if (continueTextUI != null) continueTextUI.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.5f);
        }
    }

    public void AdvanceToNextShift()
    {
        canContinue = false;
        Time.timeScale = 1f;

        // Advance shift index tracker
        int nextShift = PlayerPrefs.GetInt("CurrentShift", 1) + 1;
        PlayerPrefs.SetInt("CurrentShift", nextShift);
        PlayerPrefs.SetInt("PlayIntroCutscene", 0);
        PlayerPrefs.Save();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void RestartCurrentShift()
    {
        canContinue = false;
        Time.timeScale = 1f;

        // Skip intro sequence on restart
        PlayerPrefs.SetInt("PlayIntroCutscene", 0);
        PlayerPrefs.Save();

        // Reload the scene cleanly; GameManager.Start() resets corruption, timer, and quota
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReturnToMainMenu()
    {
        canContinue = false;
        Time.timeScale = 1f;

        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    public void EnsureVictoryScreenLayout()
    {
        try
        {
            if (endScreenPanel == null) endScreenPanel = gameObject;

            // 1. Hide lingering prompt or HUD elements so they don't bleed through
            LookBehindController lbc = FindAnyObjectByType<LookBehindController>(FindObjectsInactive.Include);
            if (lbc != null) lbc.SetPromptVisible(false);

            GameObject lbPrompt = GameObject.Find("LookBehindPrompt");
            if (lbPrompt != null) lbPrompt.SetActive(false);

            if (LocationManager.Instance != null) LocationManager.Instance.HideAllHotspots();

            // 2. Hide any unwanted runtime card or stats report box
            if (statsReportTextUI != null)
            {
                Transform bgT = statsReportTextUI.transform.Find("ReportCardBg");
                if (bgT != null) Destroy(bgT.gameObject);
                statsReportTextUI.gameObject.SetActive(false);
            }

            // 3. Ensure interactive buttons and continue prompt are active
            if (nextShiftButton != null) nextShiftButton.gameObject.SetActive(true);
            if (restartButton != null) restartButton.gameObject.SetActive(true);
            if (continueTextUI != null) continueTextUI.gameObject.SetActive(true);

            // 4. Clean up any runtime Canvas added to endScreenPanel so native scene hierarchy renders properly
            Canvas vCanvas = endScreenPanel.GetComponent<Canvas>();
            if (vCanvas != null && vCanvas != endScreenPanel.GetComponentInParent<Canvas>())
            {
                Destroy(vCanvas);
            }
            GraphicRaycaster gr = endScreenPanel.GetComponent<GraphicRaycaster>();
            if (gr != null)
            {
                Destroy(gr);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ShiftCompletionManager] Non-fatal layout cleanup exception: {ex.Message}");
        }
    }
}