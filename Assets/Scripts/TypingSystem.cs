using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using Random = UnityEngine.Random;

/// <summary>
/// Handles phrase generation, text glitching, keystroke audio, typo feedback, and player input checking.
/// </summary>
public class TypingSystem : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text headerDisplay;
    public TMP_Text promptDisplay;
    public TMP_InputField typingInput;

    [Header("Bulk Phrase Import")]
    [Tooltip("Drag your Phrases.txt file directly into this slot!")]
    public TextAsset phraseFile;

    [Header("Typing Audio")]
    public AudioSource typingAudioSource;
    public AudioClip keypressSound;

    public string OriginalPhrase => originalPhrase;
    public bool IsTextGlitched => isTextGlitched;

    public event System.Action<int> OnWordsCompleted;
    public event System.Action OnRebootRequested;

    [System.Serializable]
    public struct PhraseEntry
    {
        public string header;
        public string text;
    }

    private string originalPhrase;
    private string previousPhrase = "";
    private string scrambledPhrase = "";
    private List<PhraseEntry> parsedPhrases = new List<PhraseEntry>();
    private bool isTextGlitched = false;
    private Coroutine errorFlashCoroutine;

    private void Awake()
    {
        EnsureTerminalScreenLayout();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) EnsureTerminalScreenLayout();
        };
    }
#endif

    private void Start()
    {
        EnsureTerminalScreenLayout();
        LoadPhrasesFromTextFile();

        // Only generate a random phrase if StoryManager isn't driving chronological story documents
        StoryManager storyMgr = FindAnyObjectByType<StoryManager>(FindObjectsInactive.Include);
        if (storyMgr == null || !storyMgr.IsStoryActive)
        {
            GenerateNewPhrase();
        }

        if (typingInput != null)
        {
            typingInput.onValueChanged.AddListener(OnTypeCheck);
        }
    }

    public void EnsureTerminalScreenLayout()
    {
        // 1. Header (DOCUMENT #...)
        if (headerDisplay != null)
        {
            RectTransform hRt = headerDisplay.rectTransform;
            hRt.anchorMin = new Vector2(0.5f, 0.5f);
            hRt.anchorMax = new Vector2(0.5f, 0.5f);
            hRt.pivot = new Vector2(0.5f, 0.5f);
            hRt.anchoredPosition = new Vector2(-360f, 340f);
            hRt.sizeDelta = new Vector2(555f, 45f);

            headerDisplay.alignment = TextAlignmentOptions.TopLeft;
            headerDisplay.fontSize = 26f;
            headerDisplay.fontStyle = FontStyles.Bold;
            headerDisplay.color = Color.white;
            headerDisplay.textWrappingMode = TextWrappingModes.NoWrap;
            headerDisplay.overflowMode = TextOverflowModes.Ellipsis;
        }

        // 2. Prompt Display (The text to copy - significantly larger font for readability)
        if (promptDisplay != null)
        {
            RectTransform pRt = promptDisplay.rectTransform;
            pRt.anchorMin = new Vector2(0.5f, 0.5f);
            pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = new Vector2(-360f, 218f);
            pRt.sizeDelta = new Vector2(555f, 175f);

            promptDisplay.alignment = TextAlignmentOptions.TopLeft;
            promptDisplay.fontSize = 24f;
            promptDisplay.enableAutoSizing = true;
            promptDisplay.fontSizeMin = 20f;
            promptDisplay.fontSizeMax = 25f;
            promptDisplay.lineSpacing = 10f;
            promptDisplay.textWrappingMode = TextWrappingModes.Normal;
            promptDisplay.overflowMode = TextOverflowModes.Truncate;
            promptDisplay.color = new Color(0.92f, 0.95f, 0.98f, 1f);
        }

        // 3. Typing Input Field (The player's typed characters)
        if (typingInput != null)
        {
            RectTransform iRt = typingInput.GetComponent<RectTransform>();
            if (iRt != null)
            {
                iRt.anchorMin = new Vector2(0.5f, 0.5f);
                iRt.anchorMax = new Vector2(0.5f, 0.5f);
                iRt.pivot = new Vector2(0.5f, 0.5f);
                iRt.anchoredPosition = new Vector2(-360f, 35f);
                iRt.sizeDelta = new Vector2(555f, 140f);
            }

            if (typingInput.textComponent != null)
            {
                typingInput.textComponent.alignment = TextAlignmentOptions.TopLeft;
                typingInput.textComponent.fontSize = 30f;
                typingInput.textComponent.lineSpacing = 8f;
                typingInput.textComponent.textWrappingMode = TextWrappingModes.Normal;
            }

            if (typingInput.placeholder != null && typingInput.placeholder is TMP_Text placeholderText)
            {
                placeholderText.alignment = TextAlignmentOptions.TopLeft;
                placeholderText.fontSize = 30f;
                placeholderText.lineSpacing = 8f;
                placeholderText.textWrappingMode = TextWrappingModes.Normal;
            }

            if (typingInput.textViewport != null)
            {
                typingInput.textViewport.anchorMin = Vector2.zero;
                typingInput.textViewport.anchorMax = Vector2.one;
                typingInput.textViewport.offsetMin = new Vector2(8f, 8f);
                typingInput.textViewport.offsetMax = new Vector2(-8f, -8f);
            }
        }
    }

    public void LoadPhrasesFromTextFile()
    {
        parsedPhrases.Clear();

        if (phraseFile != null)
        {
            parsedPhrases = ParsePhrasesFile(phraseFile.text);
            Debug.Log($"[TypingSystem] Loaded and parsed {parsedPhrases.Count} structured documents from {phraseFile.name}.");
        }

        if (parsedPhrases.Count == 0)
        {
            Debug.LogWarning("[TypingSystem] No valid phrases parsed from text file! Using emergency fallback phrase.");
            parsedPhrases.Add(new PhraseEntry { header = "DOCUMENT #00000", text = "DEFAULT EMERGENCY PHRASE" });
        }
    }

    public void GenerateNewPhrase()
    {
        if (parsedPhrases != null && parsedPhrases.Count > 0)
        {
            PhraseEntry selected;

            if (parsedPhrases.Count == 1)
            {
                selected = parsedPhrases[0];
            }
            else
            {
                do
                {
                    selected = parsedPhrases[Random.Range(0, parsedPhrases.Count)];
                }
                while (parsedPhrases.Count > 1 && selected.text == previousPhrase);
            }

            previousPhrase = selected.text;

            if (headerDisplay != null)
            {
                headerDisplay.text = selected.header;
            }

            originalPhrase = selected.text.Replace("\r", "").Trim();
        }

        isTextGlitched = false;
        ResetTypingTextColor();
        EnsureTerminalScreenLayout();

        if (promptDisplay != null)
        {
            promptDisplay.text = originalPhrase;
        }

        if (typingInput != null)
        {
            typingInput.text = "";
            typingInput.ActivateInputField();
        }
    }

    /// <summary>
    /// Parses structured document texts (handles multi-line headers, comments starting with #, and body lines).
    /// </summary>
    public static List<PhraseEntry> ParsePhrasesFile(string rawText)
    {
        List<PhraseEntry> list = new List<PhraseEntry>();
        if (string.IsNullOrWhiteSpace(rawText)) return list;

        string[] lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, System.StringSplitOptions.None);
        string currentHeader = "";
        System.Text.StringBuilder currentBody = new System.Text.StringBuilder();

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            // 1. Skip comments and section banners
            if (string.IsNullOrEmpty(line) || line.StartsWith("#") || line.StartsWith("//"))
            {
                continue;
            }

            // 2. Support single-line pipe format: "Header | Body"
            if (line.Contains("|"))
            {
                FlushCurrentPhrase(list, ref currentHeader, currentBody);
                string[] parts = line.Split('|');
                list.Add(new PhraseEntry
                {
                    header = parts[0].Trim(),
                    text = parts[1].Trim()
                });
                continue;
            }

            // 3. Detect Document/Log Headers (e.g. DOCUMENT #081, LOG #001, MEMO #012)
            // Use regex pattern with delimiter (#, //, :, or digit) so sentences starting with words like "Records" are not misidentified as headers.
            bool isHeader = System.Text.RegularExpressions.Regex.IsMatch(
                line, 
                @"^(DOCUMENT|LOG|MEMO|INCIDENT|RECORD)\s*(#|//|\d|:)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            if (isHeader)
            {
                FlushCurrentPhrase(list, ref currentHeader, currentBody);
                currentHeader = line;
            }
            else
            {
                if (currentBody.Length > 0) currentBody.Append(" ");
                currentBody.Append(line);
            }
        }

        FlushCurrentPhrase(list, ref currentHeader, currentBody);
        return list;
    }

    private static void FlushCurrentPhrase(List<PhraseEntry> list, ref string header, System.Text.StringBuilder body)
    {
        string text = body.ToString().Trim();
        if (!string.IsNullOrEmpty(text))
        {
            list.Add(new PhraseEntry
            {
                header = string.IsNullOrEmpty(header) ? "DOCUMENT" : header,
                text = text
            });
        }
        header = "";
        body.Clear();
    }

    /// <summary>
    /// Loads an explicit narrative story document into the terminal.
    /// </summary>
    public void LoadSpecificPhrase(string header, string phrase)
    {
        isTextGlitched = false;
        ResetTypingTextColor();
        EnsureTerminalScreenLayout();

        originalPhrase = phrase.Replace("\r", "").Trim();

        if (headerDisplay != null)
        {
            headerDisplay.text = header;
        }

        if (promptDisplay != null)
        {
            promptDisplay.text = originalPhrase;
        }

        if (typingInput != null)
        {
            typingInput.text = "";
            typingInput.ActivateInputField();
        }
    }

    public void GenerateGlitchedVersion(float corruption)
    {
        if (string.IsNullOrEmpty(originalPhrase)) return;

        isTextGlitched = true;
        string randomChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        char[] chars = originalPhrase.ToCharArray();

        int totalGlitches = Mathf.FloorToInt((corruption / 100f) * chars.Length);
        totalGlitches = Mathf.Clamp(totalGlitches, 2, chars.Length);

        for (int i = 0; i < totalGlitches; i++)
        {
            int randomIndex = Random.Range(0, chars.Length);
            if (chars[randomIndex] != ' ')
            {
                chars[randomIndex] = (Random.value > 0.5f)
                    ? randomChars[Random.Range(0, randomChars.Length)]
                    : '█';
            }
        }

        string corruptedResult = new string(chars);

        if (Random.value > 0.5f)
        {
            string[] words = corruptedResult.Split(' ');
            if (words.Length > 0)
            {
                int randomWordIndex = Random.Range(0, words.Length);
                words[randomWordIndex] = $"<color=#FFD700>{words[randomWordIndex]}</color>";
                corruptedResult = string.Join(" ", words);
            }
        }

        scrambledPhrase = corruptedResult;
        if (promptDisplay != null)
        {
            promptDisplay.text = scrambledPhrase;
        }
    }

    public bool CanType = true;

    public void OnTypeCheck(string input)
    {
        if (PauseMenuManager.IsPaused || !CanType || string.IsNullOrEmpty(originalPhrase)) return;

        PlayKeypressSound();

        string cleanInput = input.Contains("\r") ? input.Replace("\r", "") : input;
        string target = originalPhrase;

        bool hasTypo = false;
        int checkLength = Mathf.Min(cleanInput.Length, target.Length);

        for (int i = 0; i < checkLength; i++)
        {
            if (cleanInput[i] != target[i])
            {
                hasTypo = true;
                break;
            }
        }

        if (cleanInput.Length > target.Length)
        {
            hasTypo = true;
        }

        // Color feedback with Yellow Sign / Carcosa hint
        if (typingInput != null && typingInput.textComponent != null)
        {
            if (hasTypo)
            {
                typingInput.textComponent.color = Color.red;
            }
            else
            {
                bool isLoreSecret = false;
                if (!string.IsNullOrEmpty(originalPhrase))
                {
                    if (originalPhrase.IndexOf("Yellow Sign", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        originalPhrase.IndexOf("Carcosa", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isLoreSecret = true;
                    }
                }
                if (!isLoreSecret && !string.IsNullOrEmpty(cleanInput))
                {
                    if (cleanInput.IndexOf("Yellow Sign", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                        cleanInput.IndexOf("Carcosa", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isLoreSecret = true;
                    }
                }

                if (isLoreSecret)
                {
                    typingInput.textComponent.color = new Color(1f, 0.88f, 0.12f, 1f); // Eldritch Yellow (#FFE01F)
                }
                else
                {
                    typingInput.textComponent.color = new Color(0f, 1f, 0.4f, 1f); // Phosphor green
                }
            }
        }

        // Reboot check
        if (cleanInput.Length >= 6 && cleanInput.Trim().Equals("REBOOT", System.StringComparison.OrdinalIgnoreCase))
        {
            ResetTypingTextColor();
            OnRebootRequested?.Invoke();
            return;
        }

        // Phrase completion
        if (!hasTypo && cleanInput.Length >= target.Length && cleanInput.Trim().Equals(target.Trim(), System.StringComparison.OrdinalIgnoreCase))
        {
            ResetTypingTextColor();
            int wordsInPhrase = CountWords(originalPhrase);
            OnWordsCompleted?.Invoke(wordsInPhrase);
        }
    }

    public void ShowLookingAwayPrompt(bool lookingAway)
    {
        CanType = !lookingAway;

        if (lookingAway)
        {
            if (typingInput != null)
            {
                typingInput.DeactivateInputField();
            }
        }
        else
        {
            if (typingInput != null && typingInput.gameObject.activeInHierarchy)
            {
                typingInput.ActivateInputField();
                typingInput.caretPosition = typingInput.text.Length;
            }

            if (promptDisplay != null && !string.IsNullOrEmpty(originalPhrase))
            {
                promptDisplay.text = isTextGlitched ? scrambledPhrase : originalPhrase;
            }
        }
    }

    public void SetTypingActive(bool active)
    {
        CanType = active;

        if (typingInput != null)
        {
            if (active)
            {
                typingInput.gameObject.SetActive(true);
                typingInput.ActivateInputField();
            }
            else
            {
                typingInput.DeactivateInputField();
                typingInput.gameObject.SetActive(false);
            }
        }

        if (headerDisplay != null) headerDisplay.gameObject.SetActive(active);
        if (promptDisplay != null) promptDisplay.gameObject.SetActive(active);
    }

    /// <summary>
    /// Displays an urgent system/environmental LOG message on the computer screen.
    /// </summary>
    public void DisplaySystemMessage(string header, string message, bool disableTyping = true)
    {
        EnsureTerminalScreenLayout();

        if (headerDisplay != null)
        {
            headerDisplay.gameObject.SetActive(true);
            headerDisplay.text = header;
        }

        if (promptDisplay != null)
        {
            promptDisplay.gameObject.SetActive(true);
            promptDisplay.text = message;
        }

        if (disableTyping && typingInput != null)
        {
            typingInput.text = "";
            typingInput.DeactivateInputField();
            typingInput.gameObject.SetActive(false);
            CanType = false;
        }
    }

    /// <summary>
    /// Restores the typing input box after a system message or event completes.
    /// </summary>
    public void RestoreTypingInput()
    {
        CanType = true;
        if (typingInput != null)
        {
            typingInput.gameObject.SetActive(true);
            typingInput.text = "";
            typingInput.ActivateInputField();
        }
    }

    public void DeactivateInputCaret()
    {
        CanType = false;
        if (typingInput != null)
        {
            typingInput.DeactivateInputField();
        }
    }

    public void AutoRefocusInput(bool canRefocus)
    {
        if (PauseMenuManager.IsPaused || !canRefocus || typingInput == null) return;

        CanType = true;

        if (!typingInput.isFocused && typingInput.gameObject.activeInHierarchy)
        {
            typingInput.ActivateInputField();
            typingInput.caretPosition = typingInput.text.Length;
        }
    }

    public void ResetTypingTextColor()
    {
        if (typingInput != null && typingInput.textComponent != null)
        {
            typingInput.textComponent.color = new Color(0f, 1f, 0.4f);
        }
    }

    public void PlayKeypressSound()
    {
        if (PauseMenuManager.IsPaused) return;

        if (typingAudioSource != null && keypressSound != null)
        {
            typingAudioSource.pitch = Random.Range(0.9f, 1.1f);
            typingAudioSource.PlayOneShot(keypressSound);
        }
    }

    public int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        string[] words = text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        return words.Length;
    }
}
