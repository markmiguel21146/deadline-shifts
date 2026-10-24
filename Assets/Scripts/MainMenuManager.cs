using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("Scene Settings")]
    public string gameSceneName = "GameScene";

    [Header("Audio Settings")]
    public AudioSource bgmAudioSource;
    public float fadeOutDuration = 1.0f;

    [Header("Visual Fade Settings")]
    public CanvasGroup screenFadeGroup;

    [Header("How to Play UI")]
    public GameObject howToPlayPanel;
    public Button howToPlayButton;
    public Button closeHowToPlayButton;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        EnsureHowToPlayUI();
    }

    private void Update()
    {
        if (howToPlayPanel != null && howToPlayPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseHowToPlay();
            }
        }
    }

    public void OpenHowToPlay()
    {
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(true);
            howToPlayPanel.transform.SetAsLastSibling();
        }
    }

    public void CloseHowToPlay()
    {
        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(false);
        }
    }

    private void EnsureHowToPlayUI()
    {
        // 1. Discover or create HowToPlayButton
        if (howToPlayButton == null)
        {
            Button[] allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include);
            foreach (var b in allButtons)
            {
                string n = b.gameObject.name.ToLower();
                if (n.Contains("howtoplay") || n.Contains("manual") || n.Contains("guide") || n.Contains("instruction"))
                {
                    howToPlayButton = b;
                    break;
                }
            }
        }

        Canvas mainCanvas = FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        Transform buttonParent = mainCanvas != null ? mainCanvas.transform : null;

        Button startBtn = GameObject.Find("StartButton")?.GetComponent<Button>();
        if (startBtn != null && startBtn.transform.parent != null)
        {
            buttonParent = startBtn.transform.parent;
        }

        if (howToPlayButton == null && buttonParent != null)
        {
            GameObject btnObj = new GameObject("HowToPlayButton");
            btnObj.transform.SetParent(buttonParent, false);

            RectTransform rt = btnObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // Positioned over the literal "HOW TO PLAY" text on the CRT (Start: 222, HowToPlay: 167.48, Quit: 111)
            rt.anchoredPosition = new Vector2(-38.82f, 167.48f);
            rt.sizeDelta = new Vector2(465f, 48f);
            rt.localEulerAngles = new Vector3(0f, 0f, 0.456f);

            // Transparent hit graphic matching StartButton and QuitButton
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;

            howToPlayButton = btnObj.AddComponent<Button>();
            howToPlayButton.targetGraphic = img;

            // Wire hover trigger to crossfade
            MenuBackgroundCrossfade crossfade = FindAnyObjectByType<MenuBackgroundCrossfade>(FindObjectsInactive.Include);
            if (crossfade != null)
            {
                EventTrigger trigger = btnObj.AddComponent<EventTrigger>();

                EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener((data) => crossfade.ShowHowToPlayBG());
                trigger.triggers.Add(enter);

                EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener((data) => crossfade.ShowNeutralBG());
                trigger.triggers.Add(exit);
            }
        }

        if (howToPlayButton != null)
        {
            // Clean up any legacy dynamic UI text or solid background from previous versions
            Transform legacyText = howToPlayButton.transform.Find("ButtonText");
            if (legacyText != null) Destroy(legacyText.gameObject);

            Image btnImg = howToPlayButton.GetComponent<Image>();
            if (btnImg != null && btnImg.color.a > 0.05f)
            {
                Color c = btnImg.color;
                c.a = 0f;
                btnImg.color = c;
            }

            // Ensure EventTrigger for hover crossfade exists on HowToPlayButton
            EventTrigger trigger = howToPlayButton.GetComponent<EventTrigger>();
            if (trigger == null)
            {
                MenuBackgroundCrossfade crossfade = FindAnyObjectByType<MenuBackgroundCrossfade>(FindObjectsInactive.Include);
                if (crossfade != null)
                {
                    trigger = howToPlayButton.gameObject.AddComponent<EventTrigger>();

                    EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                    enter.callback.AddListener((data) => crossfade.ShowHowToPlayBG());
                    trigger.triggers.Add(enter);

                    EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                    exit.callback.AddListener((data) => crossfade.ShowNeutralBG());
                    trigger.triggers.Add(exit);
                }
            }

            howToPlayButton.onClick.RemoveListener(OpenHowToPlay);
            howToPlayButton.onClick.AddListener(OpenHowToPlay);
        }

        // 2. Discover or create HowToPlayPanel modal
        if (howToPlayPanel == null)
        {
            Transform found = (mainCanvas != null) ? mainCanvas.transform.Find("HowToPlayPanel") : null;
            if (found != null)
            {
                howToPlayPanel = found.gameObject;
            }
        }

        if (howToPlayPanel == null && mainCanvas != null)
        {
            GameObject panelObj = new GameObject("HowToPlayPanel");
            panelObj.transform.SetParent(mainCanvas.transform, false);
            panelObj.transform.SetAsLastSibling();

            RectTransform prt = panelObj.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.sizeDelta = Vector2.zero;
            prt.anchoredPosition = Vector2.zero;

            Image bg = panelObj.AddComponent<Image>();
            bg.color = new Color(0.01f, 0.03f, 0.02f, 0.94f);

            CanvasGroup cg = panelObj.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = true;
            cg.interactable = true;

            // Frame Container
            GameObject frameObj = new GameObject("ManualFrame");
            frameObj.transform.SetParent(panelObj.transform, false);
            RectTransform frt = frameObj.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0.5f, 0.5f);
            frt.anchorMax = new Vector2(0.5f, 0.5f);
            frt.pivot = new Vector2(0.5f, 0.5f);
            frt.sizeDelta = new Vector2(1180f, 820f);
            frt.anchoredPosition = Vector2.zero;

            Image frameImg = frameObj.AddComponent<Image>();
            frameImg.color = new Color(0.03f, 0.07f, 0.04f, 0.98f);

            Outline frameOutline = frameObj.AddComponent<Outline>();
            frameOutline.effectColor = new Color(0.2f, 0.8f, 0.35f, 0.85f);
            frameOutline.effectDistance = new Vector2(3f, -3f);

            // Title Header
            GameObject titleObj = new GameObject("HeaderTitle");
            titleObj.transform.SetParent(frameObj.transform, false);
            RectTransform trt = titleObj.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, 1f);
            trt.anchorMax = new Vector2(0.5f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -25f);
            trt.sizeDelta = new Vector2(1120f, 65f);

            TextMeshProUGUI headerTmp = titleObj.AddComponent<TextMeshProUGUI>();
            headerTmp.text = "DEPARTMENT OF METAPHYSICAL SCIENCES\n<size=18><color=#88FF99>// TERMINAL OPERATOR PROTOCOL MANUAL //</color></size>";
            headerTmp.alignment = TextAlignmentOptions.Center;
            headerTmp.fontSize = 24;
            headerTmp.fontStyle = FontStyles.Bold;
            headerTmp.color = new Color(0.3f, 1f, 0.45f, 1f);

            // Body Instructions
            GameObject bodyObj = new GameObject("ManualBody");
            bodyObj.transform.SetParent(frameObj.transform, false);
            RectTransform brt = bodyObj.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.5f);
            brt.anchorMax = new Vector2(0.5f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = new Vector2(0f, 10f);
            brt.sizeDelta = new Vector2(1100f, 570f);

            TextMeshProUGUI bodyTmp = bodyObj.AddComponent<TextMeshProUGUI>();
            bodyTmp.fontSize = 17;
            bodyTmp.lineSpacing = 12;
            bodyTmp.color = new Color(0.82f, 0.95f, 0.85f, 1f);
            bodyTmp.text = 
@"<color=#44FF66><b>[ 01. TRANSCRIPTION & QUOTA ]</b></color>
• Accurately type the classified record shown on the monitor.
• Typing Speed (WPM) and accuracy determine your department compliance rating.
• Advance through assigned records to fulfill your shift quota before time expires.

<color=#44FF66><b>[ 02. CORRUPTION & TERMINAL REBOOT ]</b></color>
• Anomalous records suffer severe Eldritch text corruption over time.
• When corrupted glyphs block transcription, type <b><color=#FFFF55>REBOOT</color></b> and press <b>[ENTER]</b>.
• System rebooting clears terminal corruption without resetting your current shift record.

<color=#44FF66><b>[ 03. SURVEILLANCE & LOOKING AWAY (TAB) ]</b></color>
• Unseen entities stalk the archives during late shift hours.
• Hold or press <b><color=#FFFF55>[TAB]</color></b> to look behind your desk and monitor your surroundings.
• Do not abandon your terminal for too long—neglecting your desk will cause shift time to slip away!

<color=#44FF66><b>[ 04. POWER OUTAGES & CIRCUIT BREAKER ]</b></color>
• Facility overloads will plunge the office into total blackout.
• Follow the pulsing amber cue on the left doorway <b><color=#FFAA22>[ ◄ BREAKER ROOM ]</color></b> to leave your desk.
• Inspect the breaker box on the wall, throw the main switch to restore power, and return immediately.

<color=#44FF66><b>[ 05. SHIFT PROGRESSION ]</b></color>
• Shift 2 introduces classified Department of Metaphysical Sciences records (Documents #201+).
• Obey department directives, maintain focus, and survive until 06:00:00.";

            // Close Button
            GameObject closeBtnObj = new GameObject("CloseManualButton");
            closeBtnObj.transform.SetParent(frameObj.transform, false);
            RectTransform crt = closeBtnObj.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.5f, 0f);
            crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 30f);
            crt.sizeDelta = new Vector2(360f, 50f);

            Image closeImg = closeBtnObj.AddComponent<Image>();
            closeImg.color = new Color(0.1f, 0.28f, 0.15f, 1f);

            closeHowToPlayButton = closeBtnObj.AddComponent<Button>();
            ColorBlock ccb = closeHowToPlayButton.colors;
            ccb.normalColor = Color.white;
            ccb.highlightedColor = new Color(0.4f, 1f, 0.5f, 1f);
            ccb.pressedColor = new Color(0.1f, 0.5f, 0.2f, 1f);
            closeHowToPlayButton.colors = ccb;

            GameObject closeTextObj = new GameObject("ButtonText");
            closeTextObj.transform.SetParent(closeBtnObj.transform, false);
            RectTransform ctr = closeTextObj.AddComponent<RectTransform>();
            ctr.anchorMin = Vector2.zero;
            ctr.anchorMax = Vector2.one;
            ctr.sizeDelta = Vector2.zero;

            TextMeshProUGUI closeTmp = closeTextObj.AddComponent<TextMeshProUGUI>();
            closeTmp.text = "[ RETURN TO MAIN MENU ]";
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeTmp.fontSize = 20;
            closeTmp.fontStyle = FontStyles.Bold;
            closeTmp.color = new Color(0.4f, 1f, 0.5f, 1f);

            howToPlayPanel = panelObj;
        }

        if (closeHowToPlayButton == null && howToPlayPanel != null)
        {
            Button[] panelButtons = howToPlayPanel.GetComponentsInChildren<Button>(true);
            foreach (var b in panelButtons)
            {
                string n = b.gameObject.name.ToLower();
                if (n.Contains("close") || n.Contains("back") || n.Contains("return") || n.Contains("menu"))
                {
                    closeHowToPlayButton = b;
                    break;
                }
            }
        }

        if (closeHowToPlayButton != null)
        {
            closeHowToPlayButton.onClick.RemoveListener(CloseHowToPlay);
            closeHowToPlayButton.onClick.AddListener(CloseHowToPlay);
        }

        if (howToPlayPanel != null)
        {
            howToPlayPanel.SetActive(false);
        }
    }

    public void StartGame()
    {
        StartCoroutine(StartGameWithFade());
    }

    private IEnumerator StartGameWithFade()
    {
        float elapsed = 0f;

        if (screenFadeGroup != null)
        {
            screenFadeGroup.blocksRaycasts = true;
        }

        float startVolume = (bgmAudioSource != null) ? bgmAudioSource.volume : 0f;

        // Fade out screen and music
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / fadeOutDuration;

            if (bgmAudioSource != null)
            {
                bgmAudioSource.volume = Mathf.Lerp(startVolume, 0f, t);
            }

            if (screenFadeGroup != null)
            {
                screenFadeGroup.alpha = Mathf.Lerp(0f, 1f, t);
            }

            yield return null;
        }

        if (bgmAudioSource != null) bgmAudioSource.Stop();

        // Save flags for new game run
        PlayerPrefs.SetInt("PlayIntroCutscene", 1);
        PlayerPrefs.SetInt("CurrentShift", 1);
        PlayerPrefs.Save();

        // Load GameScene
        SceneManager.LoadScene(gameSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #endif
    }
}