using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Displays the Shift Title Card with smooth fading, unscaled timing,
/// input debounce to prevent skip bleed-through from IntroCutscene,
/// sorting order 950 to prevent office flash, and clean handoff to EyeOpeningTransition.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class ShiftTitleCard : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject titleCardPanel;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI headerTextUI;
    [SerializeField] private TextMeshProUGUI instructionTextUI;

    [Header("Timings")]
    [SerializeField] private float displayDuration = 2.8f;
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private float minSkipDelay = 1.0f;

    [Header("Transitions")]
    [SerializeField] private EyeOpeningTransition eyeTransition;

    private Coroutine cardCoroutine;
    private bool isCardPlaying = false;
    private bool isSkipping = false;
    private float cardStartTime = 0f;

    private void Awake()
    {
        EnsureTitleCardUI();
    }

    private void Start()
    {
        // Only hide if not actively requested to play by IntroCutscene
        if (!isCardPlaying)
        {
            if (canvasGroup != null) canvasGroup.alpha = 0f;
            if (titleCardPanel != null && titleCardPanel != gameObject)
            {
                titleCardPanel.SetActive(false);
            }
            else if (titleCardPanel == gameObject)
            {
                gameObject.SetActive(false);
            }
        }
    }

    private void Update()
    {
        if (!isCardPlaying || isSkipping) return;

        // Debounce: prevent keypress that skipped cutscene (e.g. Escape / Space) from immediately skipping title card
        if (Time.unscaledTime - cardStartTime < minSkipDelay) return;

        if (gameObject.activeSelf && (titleCardPanel == null || titleCardPanel.activeSelf))
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetMouseButtonDown(0))
            {
                SkipCard();
            }
        }
    }

    public void PlayCard(int shiftNumber, string instructionMessage)
    {
        Debug.Log($"[ShiftTitleCard] PlayCard initiated for Shift {shiftNumber}: \"{instructionMessage}\"");
        GameManager.isCutscenePlaying = true;
        EnsureTitleCardUI();

        isCardPlaying = true;
        isSkipping = false;
        cardStartTime = Time.unscaledTime;

        gameObject.SetActive(true);

        if (titleCardPanel != null)
        {
            titleCardPanel.SetActive(true);
            titleCardPanel.transform.SetAsLastSibling();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }

        if (headerTextUI != null)
        {
            headerTextUI.text = shiftNumber switch
            {
                1 => "1ST NIGHT",
                2 => "2ND NIGHT",
                3 => "3RD NIGHT",
                _ => $"NIGHT {shiftNumber}"
            };
        }

        if (instructionTextUI != null)
        {
            if (shiftNumber == 2 && (string.IsNullOrEmpty(instructionMessage) || instructionMessage.Contains("Storage Area 7")))
            {
                instructionMessage = "Archive status compromised. Type REBOOT to clear corrupted text.";
            }
            instructionTextUI.text = instructionMessage;
        }

        if (cardCoroutine != null)
        {
            StopCoroutine(cardCoroutine);
        }

        cardCoroutine = StartCoroutine(CardSequence());
    }

    public void SkipCard()
    {
        if (isSkipping) return;
        isSkipping = true;

        if (cardCoroutine != null)
        {
            StopCoroutine(cardCoroutine);
        }

        StartCoroutine(FastFadeAndTransition());
    }

    private IEnumerator CardSequence()
    {
        if (canvasGroup != null) canvasGroup.alpha = 1f;

        // 1. Hold title card solid on screen (unscaled time)
        float elapsed = 0f;
        while (elapsed < displayDuration)
        {
            if (isSkipping) yield break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // 2. Smoothly fade out title card BEFORE triggering eye opening
        yield return StartCoroutine(FastFadeAndTransition());
    }

    private IEnumerator FastFadeAndTransition()
    {
        // 1. Prepare EyeOpeningTransition with eyelids starting closed in solid black UNDERNEATH the title card!
        if (eyeTransition == null)
        {
            eyeTransition = FindAnyObjectByType<EyeOpeningTransition>(FindObjectsInactive.Include);
        }

        if (eyeTransition != null)
        {
            eyeTransition.EnsureEyelidUI();
            eyeTransition.gameObject.SetActive(true);
            if (eyeTransition.topEyelid != null)
            {
                eyeTransition.topEyelid.gameObject.SetActive(true);
                eyeTransition.topEyelid.anchoredPosition = Vector2.zero;
            }
            if (eyeTransition.bottomEyelid != null)
            {
                eyeTransition.bottomEyelid.gameObject.SetActive(true);
                eyeTransition.bottomEyelid.anchoredPosition = Vector2.zero;
            }
        }

        // 2. Fade title card text out over fadeDuration (revealing the closed black eyelids underneath)
        if (canvasGroup != null)
        {
            float elapsed = 0f;
            float startAlpha = canvasGroup.alpha;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
        }

        // 3. Now start eyelid opening animation from solid blackness!
        if (eyeTransition != null)
        {
            Debug.Log("[ShiftTitleCard] Triggering EyeOpeningTransition.");
            eyeTransition.PlayTransition();
        }
        else
        {
            Debug.LogWarning("[ShiftTitleCard] EyeOpeningTransition not found! Attempting fallback boot.");
            OSBootSequence boot = FindAnyObjectByType<OSBootSequence>(FindObjectsInactive.Include);
            if (boot != null)
            {
                boot.gameObject.SetActive(true);
                boot.StartBoot();
            }
            else
            {
                GameManager.isCutscenePlaying = false;
            }
        }

        isCardPlaying = false;

        if (titleCardPanel != null && titleCardPanel != gameObject)
        {
            titleCardPanel.SetActive(false);
        }

        gameObject.SetActive(false);
    }

    public void EnsureTitleCardUI()
    {
        if (titleCardPanel == null)
        {
            titleCardPanel = gameObject;
        }

        if (canvasGroup == null)
        {
            canvasGroup = titleCardPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = titleCardPanel.AddComponent<CanvasGroup>();
        }

        // Discover targetCanvas to inherit sortingLayerID properly
        Canvas targetCanvas = titleCardPanel.GetComponentInParent<Canvas>();
        if (targetCanvas == null || targetCanvas == titleCardPanel.GetComponent<Canvas>())
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (var c in canvases)
            {
                if (c != null && c.gameObject != titleCardPanel && (c.gameObject.name.Contains("Workplace") || c.gameObject.name.Contains("HUD")))
                {
                    targetCanvas = c;
                    break;
                }
            }
            if (targetCanvas == null && canvases.Length > 0)
            {
                foreach (var c in canvases)
                {
                    if (c != null && c.gameObject != titleCardPanel) { targetCanvas = c; break; }
                }
            }
        }

        // Reparent to root targetCanvas if not already
        if (targetCanvas != null && titleCardPanel.transform.parent != targetCanvas.transform)
        {
            titleCardPanel.transform.SetParent(targetCanvas.transform, false);
        }

        // Crucial: Dedicated Canvas with override sorting at order 950 (in front of office & eyelids 900, behind cutscene 999)
        Canvas panelCanvas = titleCardPanel.GetComponent<Canvas>();
        if (panelCanvas == null) panelCanvas = titleCardPanel.AddComponent<Canvas>();
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
            panelCanvas.sortingOrder = 950;
        }

        GraphicRaycaster raycaster = titleCardPanel.GetComponent<GraphicRaycaster>();
        if (raycaster == null) raycaster = titleCardPanel.AddComponent<GraphicRaycaster>();

        // Ensure fullscreen solid black background
        Image bg = titleCardPanel.GetComponent<Image>();
        if (bg == null) bg = titleCardPanel.AddComponent<Image>();
        bg.color = Color.black;
        bg.raycastTarget = true;

        RectTransform panelRt = titleCardPanel.GetComponent<RectTransform>();
        if (panelRt != null)
        {
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.localScale = Vector3.one;
        }

        // Discover or setup Header Text
        if (headerTextUI == null)
        {
            TextMeshProUGUI[] tmps = titleCardPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t != null && (t.name.IndexOf("header", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  t.name.IndexOf("night", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                                  t.name.IndexOf("shift", System.StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    headerTextUI = t;
                    break;
                }
            }
            if (headerTextUI == null && tmps.Length > 0)
            {
                headerTextUI = tmps[0];
            }
        }

        if (headerTextUI != null)
        {
            RectTransform hRt = headerTextUI.rectTransform;
            hRt.anchorMin = new Vector2(0.5f, 0.5f);
            hRt.anchorMax = new Vector2(0.5f, 0.5f);
            hRt.pivot = new Vector2(0.5f, 0.5f);
            hRt.anchoredPosition = new Vector2(0f, 60f);
            hRt.sizeDelta = new Vector2(850f, 100f);
            hRt.localScale = Vector3.one;

            headerTextUI.alignment = TextAlignmentOptions.Center;
            headerTextUI.fontSize = 52;
            headerTextUI.characterSpacing = 4f;
            headerTextUI.fontStyle = FontStyles.Bold;
            headerTextUI.color = Color.white;
        }

        // Discover or setup Instruction Text
        if (instructionTextUI == null)
        {
            TextMeshProUGUI[] tmps = titleCardPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var t in tmps)
            {
                if (t != null && t != headerTextUI)
                {
                    instructionTextUI = t;
                    break;
                }
            }
        }

        if (instructionTextUI != null)
        {
            RectTransform iRt = instructionTextUI.rectTransform;
            iRt.anchorMin = new Vector2(0.5f, 0.5f);
            iRt.anchorMax = new Vector2(0.5f, 0.5f);
            iRt.pivot = new Vector2(0.5f, 0.5f);
            iRt.anchoredPosition = new Vector2(0f, -40f);
            iRt.sizeDelta = new Vector2(900f, 120f);
            iRt.localScale = Vector3.one;

            instructionTextUI.alignment = TextAlignmentOptions.Center;
            instructionTextUI.fontSize = 20;
            instructionTextUI.characterSpacing = 1.5f;
            instructionTextUI.lineSpacing = 12f;
            instructionTextUI.color = new Color(0.85f, 0.88f, 0.86f, 0.90f);
        }

        // Propagate retro font
        if (headerTextUI != null && (headerTextUI.font == null || instructionTextUI?.font == null))
        {
            TMP_Text sample = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
            if (sample != null && sample.font != null)
            {
                if (headerTextUI.font == null) headerTextUI.font = sample.font;
                if (instructionTextUI != null && instructionTextUI.font == null) instructionTextUI.font = sample.font;
            }
        }

        if (eyeTransition == null)
        {
            eyeTransition = FindAnyObjectByType<EyeOpeningTransition>(FindObjectsInactive.Include);
        }
    }
}