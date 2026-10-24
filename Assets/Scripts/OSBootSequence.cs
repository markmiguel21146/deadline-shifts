using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Simulates 1990s MARSAR OS BIOS boot on the CRT monitor.
/// Uses unscaled timing, skip support, and safely restores typing controls.
/// </summary>
public class OSBootSequence : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject bootScreenPanel;
    [SerializeField] private Image companyLogo;
    [SerializeField] private TextMeshProUGUI bootText;

    [Header("Boot Settings")]
    [SerializeField] private AudioSource bootAudioSource;
    [SerializeField] private AudioClip bootBeepSound;

    [Header("Boot Text Lines")]
    [TextArea(2, 3)]
    [SerializeField] private string[] biosLoadingLines = new string[]
    {
        "MARSAR OS v1.04 Initializing...",
        "Memory Test: 640K OK",
        "Loading Shift Protocols...",
        "Connecting to Terminal 4..."
    };

    private bool isBooting = false;
    private bool isSkipping = false;
    private float bootStartTime = 0f;
    private Coroutine bootCoroutine;

    private void Awake()
    {
        EnsureBootScreenLayout();

        if (bootAudioSource == null)
        {
            bootAudioSource = GetComponent<AudioSource>();
            if (bootAudioSource == null)
            {
                bootAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (bootAudioSource != null)
        {
            bootAudioSource.playOnAwake = false;
            bootAudioSource.loop = false;
        }

        if (bootBeepSound == null)
        {
            AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (var c in clips)
            {
                if (c != null && (c.name.IndexOf("keystroke", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  c.name.IndexOf("beep", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    bootBeepSound = c;
                    break;
                }
            }
        }

        if (bootText == null)
        {
            if (bootScreenPanel != null)
            {
                bootText = bootScreenPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            if (bootText == null)
            {
                bootText = GetComponentInChildren<TextMeshProUGUI>(true);
            }
        }
    }

    public void EnsureBootScreenLayout()
    {
        // 1. Shift entire BootScreenPanel down so it never covers monitor logos/casing
        RectTransform panelRt = (bootScreenPanel != null) ? bootScreenPanel.GetComponent<RectTransform>() : GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = new Vector2(-313.04f, 175f);
        }

        // 2. Position Company Logo at top of CRT screen
        if (companyLogo != null)
        {
            RectTransform cRt = companyLogo.rectTransform;
            cRt.anchorMin = new Vector2(0.5f, 0.5f);
            cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot = new Vector2(0.5f, 0.5f);
            cRt.anchoredPosition = new Vector2(0f, 80f);
            cRt.sizeDelta = new Vector2(120f, 120f);
        }

        // 3. Fallback resolve BootText
        if (bootText == null)
        {
            if (bootScreenPanel != null) bootText = bootScreenPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (bootText == null) bootText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // 4. Position BootText with top pivot below logo with 30px clear breathing room
        if (bootText != null)
        {
            RectTransform bRt = bootText.rectTransform;
            bRt.anchorMin = new Vector2(0.5f, 0.5f);
            bRt.anchorMax = new Vector2(0.5f, 0.5f);
            bRt.pivot = new Vector2(0.5f, 1f); // Top pivot so text grows downward
            bRt.anchoredPosition = new Vector2(0f, -10f); // 30px clearance below bottom of logo (y=20f)
            bRt.sizeDelta = new Vector2(540f, 180f);

            bootText.fontSize = 17f;
            bootText.lineSpacing = 5f;
            bootText.alignment = TextAlignmentOptions.Top;
            bootText.textWrappingMode = TextWrappingModes.Normal;
            bootText.overflowMode = TextOverflowModes.Overflow;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) EnsureBootScreenLayout();
        };
    }
#endif

    private void Start()
    {
        if (!isBooting)
        {
            if (bootScreenPanel != null && bootScreenPanel != gameObject)
            {
                bootScreenPanel.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (isBooting && !isSkipping)
        {
            if (Time.unscaledTime - bootStartTime < 0.35f) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Escape))
            {
                SkipBoot();
            }
        }
    }

    public void StartBoot()
    {
        Debug.Log("[OSBootSequence] StartBoot initiated.");
        if (isBooting) return;
        isBooting = true;
        isSkipping = false;
        bootStartTime = Time.unscaledTime;

        // Temporarily hide typing terminal prompts so zero prompt text bleeds through during boot
        TypingSystem typingSys = FindAnyObjectByType<TypingSystem>(FindObjectsInactive.Include);
        if (typingSys != null)
        {
            typingSys.SetTypingActive(false);
            if (typingSys.headerDisplay != null) typingSys.headerDisplay.gameObject.SetActive(false);
            if (typingSys.promptDisplay != null) typingSys.promptDisplay.gameObject.SetActive(false);
        }

        gameObject.SetActive(true);
        if (bootScreenPanel != null) bootScreenPanel.SetActive(true);
        transform.SetAsLastSibling();

        EnsureBootScreenLayout();

        if (bootCoroutine != null)
        {
            StopCoroutine(bootCoroutine);
        }

        bootCoroutine = StartCoroutine(PlayBootSequence());
    }

    public void SkipBoot()
    {
        if (!isBooting || isSkipping) return;
        isSkipping = true;

        if (bootCoroutine != null)
        {
            StopCoroutine(bootCoroutine);
        }

        // When skipped, ensure the full text including REBOOT protocol is displayed before transitioning
        if (bootText != null)
        {
            bootText.text = "";
            foreach (string line in biosLoadingLines)
            {
                bootText.text += line + "\n";
            }

            int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
            GameManager gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
            if (gm != null) currentShift = Mathf.Max(currentShift, gm.currentShiftIndex + 1);

            if (currentShift >= 2)
            {
                bootText.text += "<color=#FFFF55>[ PROTOCOL: IF TEXT BUFFER CORRUPTS, ENTER REBOOT ]</color>\n";
            }
        }

        StartCoroutine(SkipHandoffRoutine());
    }

    private IEnumerator SkipHandoffRoutine()
    {
        yield return new WaitForSecondsRealtime(0.35f);
        FinishBoot();
    }

    private IEnumerator PlayBootSequence()
    {
        isBooting = true;

        if (bootScreenPanel != null) bootScreenPanel.SetActive(true);
        if (bootText != null) bootText.text = "";

        if (companyLogo != null)
        {
            companyLogo.gameObject.SetActive(true);
        }

        if (bootAudioSource != null && bootBeepSound != null)
        {
            bootAudioSource.pitch = 1.2f;
            bootAudioSource.PlayOneShot(bootBeepSound, 0.45f);
        }

        // Type out boot lines
        foreach (string line in biosLoadingLines)
        {
            if (isSkipping) yield break;

            if (bootText != null)
            {
                bootText.text += line + "\n";
            }

            if (bootAudioSource != null && bootBeepSound != null)
            {
                bootAudioSource.pitch = UnityEngine.Random.Range(1.1f, 1.4f);
                bootAudioSource.PlayOneShot(bootBeepSound, 0.18f);
            }

            yield return new WaitForSecondsRealtime(0.35f);
        }

        int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
        GameManager gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (gm != null)
        {
            currentShift = Mathf.Max(currentShift, gm.currentShiftIndex + 1);
        }

        if (currentShift >= 2 && !isSkipping)
        {
            if (bootText != null)
            {
                bootText.text += "<color=#FFFF55>[ PROTOCOL: IF TEXT BUFFER CORRUPTS, ENTER REBOOT ]</color>\n";
            }
            if (bootAudioSource != null && bootBeepSound != null)
            {
                bootAudioSource.pitch = 1.35f;
                bootAudioSource.PlayOneShot(bootBeepSound, 0.35f);
            }
            yield return new WaitForSecondsRealtime(0.5f);
        }

        yield return new WaitForSecondsRealtime(0.6f);

        FinishBoot();
    }

    private void FinishBoot()
    {
        Debug.Log("[OSBootSequence] FinishBoot called - enabling typing input and unpausing cutscene.");

        // Turn off boot overlay screen
        if (bootScreenPanel != null && bootScreenPanel != gameObject)
        {
            bootScreenPanel.SetActive(false);
        }

        // Restore typing input and caret in TypingSystem
        TypingSystem typingSys = FindAnyObjectByType<TypingSystem>(FindObjectsInactive.Include);
        if (typingSys != null)
        {
            if (typingSys.headerDisplay != null) typingSys.headerDisplay.gameObject.SetActive(true);
            if (typingSys.promptDisplay != null) typingSys.promptDisplay.gameObject.SetActive(true);
            typingSys.EnsureTerminalScreenLayout();
            typingSys.RestoreTypingInput();
            typingSys.AutoRefocusInput(true);
        }

        // UNPAUSE GAMEPLAY MECHANICS (Player can type now)
        GameManager.isCutscenePlaying = false;
        isBooting = false;
        isSkipping = false;

        gameObject.SetActive(false);
    }
}