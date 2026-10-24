using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages the psychological horror narrative loop:
/// Type Document -> Computer displays LOG -> Environmental Event -> Point-and-Click Exploration -> Resolve Event -> Return to Computer -> Next Document.
/// Horror progression: Normal records -> Strange records -> Predictive logs -> The computer recognizing the player.
/// </summary>
public class StoryManager : MonoBehaviour
{
    private static StoryManager instance;
    public static StoryManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindAnyObjectByType<StoryManager>(FindObjectsInactive.Include);
            }
            return instance;
        }
        private set => instance = value;
    }

    [Header("Phrases.txt Import")]
    [Tooltip("Drag Phrases.txt here! If left empty, it will auto-use phraseFile from TypingSystem.")]
    public TextAsset phraseFile;

    [Header("Progression Controls")]
    [Tooltip("Index of document to start on. Default 0 (DOCUMENT #081). Set to 5 to jump straight to DOCUMENT #086!")]
    public int startingDocumentIndex = 0;

    [Header("Story Documents Progression")]
    [Tooltip("Chronological sequence of story beats (auto-populated from Phrases.txt if empty)")]
    [SerializeField] private StoryDocument[] storyDocuments;

    [Header("Dynamic Story Gameplay Triggers")]
    [Tooltip("Gameplay conditions that trigger story events or Department alerts alongside typing")]
    [SerializeField] private List<NarrativeGameplayTrigger> gameplayTriggers = new List<NarrativeGameplayTrigger>();

    [Header("Dependencies")]
    [SerializeField] private TypingSystem typingSystem;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnvironmentalEventManager eventManager;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip logAlertSound;

    public bool IsStoryActive { get; private set; } = true;
    public int CurrentDocumentIndex { get; private set; } = 0;
    private bool isPlayingBroadcast = false;

    public StoryDocument CurrentDocument => (storyDocuments != null && CurrentDocumentIndex >= 0 && CurrentDocumentIndex < storyDocuments.Length) 
        ? storyDocuments[CurrentDocumentIndex] 
        : null;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        if (typingSystem == null) typingSystem = FindAnyObjectByType<TypingSystem>(FindObjectsInactive.Include);
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        if (eventManager == null) eventManager = FindAnyObjectByType<EnvironmentalEventManager>(FindObjectsInactive.Include);
        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        // Auto-link phraseFile from typingSystem or gameManager if not explicitly assigned
        if (phraseFile == null && typingSystem != null)
        {
            phraseFile = typingSystem.phraseFile;
        }
        if (phraseFile == null && gameManager != null)
        {
            phraseFile = gameManager.phraseFile;
        }

        // Initialize default gameplay narrative triggers if list is empty
        if (gameplayTriggers == null || gameplayTriggers.Count == 0)
        {
            SetupDefaultGameplayTriggers();
        }

        // Auto-import from Phrases.txt if storyDocuments is empty
        if (storyDocuments == null || storyDocuments.Length == 0)
        {
            if (phraseFile != null)
            {
                LoadStoryFromPhraseFile();
            }
            else
            {
                SetupDefaultHorrorProgression();
            }
        }
    }

    private void Start()
    {
        int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
        if (gameManager != null && gameManager.currentShiftIndex >= 0 && currentShift < gameManager.currentShiftIndex + 1)
        {
            currentShift = gameManager.currentShiftIndex + 1;
        }

        if (storyDocuments != null && storyDocuments.Length > 0)
        {
            CurrentDocumentIndex = Mathf.Clamp(startingDocumentIndex, 0, storyDocuments.Length - 1);

            if (startingDocumentIndex == 0 && currentShift > 1)
            {
                // Auto-seek to where this shift begins in Phrases.txt (e.g. 201 for Shift 2, 301 for Shift 3)
                string shiftPrefix = currentShift switch
                {
                    2 => "201",
                    3 => "301",
                    4 => "401",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(shiftPrefix))
                {
                    for (int i = 0; i < storyDocuments.Length; i++)
                    {
                        if (storyDocuments[i].headerTitle.Contains(shiftPrefix))
                        {
                            CurrentDocumentIndex = i;
                            break;
                        }
                    }
                }
            }
        }

        // Reset runtime triggers for fresh shift run
        if (gameplayTriggers != null)
        {
            foreach (var t in gameplayTriggers)
            {
                t.hasTriggered = false;
            }
        }

        if (IsStoryActive && CurrentDocument != null && typingSystem != null)
        {
            // Load initial story document into terminal
            typingSystem.LoadSpecificPhrase(CurrentDocument.headerTitle, CurrentDocument.documentText);
        }
    }

    private void Update()
    {
        if (!IsStoryActive || isPlayingBroadcast || GameManager.isCutscenePlaying) return;
        if (typingSystem == null || !typingSystem.CanType) return;
        if (gameManager == null) return;

        CheckGameplayTriggers();
    }

    private void CheckGameplayTriggers()
    {
        if (gameplayTriggers == null || gameplayTriggers.Count == 0) return;

        int currentShift = PlayerPrefs.GetInt("CurrentShift", 1);
        if (gameManager != null && gameManager.currentShiftIndex >= 0 && currentShift < gameManager.currentShiftIndex + 1)
        {
            currentShift = gameManager.currentShiftIndex + 1;
        }

        float quotaPercent = (gameManager.targetQuotaWords > 0) 
            ? ((float)gameManager.GetCurrentWordsTyped() / gameManager.targetQuotaWords) * 100f 
            : 0f;
        float corruption = gameManager.corruption;
        float timeSec = gameManager.GetGameTimer();

        for (int i = 0; i < gameplayTriggers.Count; i++)
        {
            var trigger = gameplayTriggers[i];
            if (trigger.hasTriggered) continue;
            if (trigger.shiftRequired != 0 && trigger.shiftRequired != currentShift) continue;

            bool conditionMet = trigger.type switch
            {
                NarrativeGameplayTrigger.TriggerType.QuotaPercentage => quotaPercent >= trigger.thresholdValue,
                NarrativeGameplayTrigger.TriggerType.CorruptionLevel => corruption >= trigger.thresholdValue,
                NarrativeGameplayTrigger.TriggerType.TimeElapsed => timeSec >= trigger.thresholdValue,
                _ => false
            };

            if (conditionMet)
            {
                trigger.hasTriggered = true;
                StartCoroutine(PlayNarrativeBroadcastRoutine(trigger));
                break;
            }
        }
    }

    private IEnumerator PlayNarrativeBroadcastRoutine(NarrativeGameplayTrigger trigger)
    {
        isPlayingBroadcast = true;

        if (logAlertSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(logAlertSound);
        }

        string currentTyped = (typingSystem != null && typingSystem.typingInput != null) ? typingSystem.typingInput.text : "";

        if (typingSystem != null)
        {
            typingSystem.DisplaySystemMessage(trigger.alertHeader, trigger.alertMessage, true);
        }

        yield return new WaitForSeconds(trigger.displayDuration);

        if (CurrentDocument != null && typingSystem != null)
        {
            typingSystem.RestoreTypingInput();
            typingSystem.LoadSpecificPhrase(CurrentDocument.headerTitle, CurrentDocument.documentText);
            if (typingSystem.typingInput != null)
            {
                typingSystem.typingInput.text = currentTyped;
                typingSystem.typingInput.caretPosition = currentTyped.Length;
            }
        }

        isPlayingBroadcast = false;
    }

    /// <summary>
    /// Called when the player finishes typing the current document.
    /// Evaluates post-typing logs and environmental disturbances.
    /// </summary>
    public void OnDocumentCompleted()
    {
        if (!IsStoryActive || CurrentDocument == null)
        {
            typingSystem?.GenerateNewPhrase();
            return;
        }

        StartCoroutine(HandleDocumentCompletedRoutine(CurrentDocument));
    }

    private IEnumerator HandleDocumentCompletedRoutine(StoryDocument completedDoc)
    {
        // 1. Automatic Post-Typing LOG Message on PC
        if (completedDoc.hasPostLog)
        {
            if (logAlertSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(logAlertSound);
            }

            if (typingSystem != null)
            {
                typingSystem.DisplaySystemMessage(completedDoc.logHeader, completedDoc.logMessage, true);
            }

            yield return new WaitForSeconds(completedDoc.logDisplayDelay);
        }

        // 2. Trigger Environmental Disturbance
        if (!string.IsNullOrEmpty(completedDoc.eventTriggerId))
        {
            if (EnvironmentalEventManager.Instance != null)
            {
                EnvironmentalEventManager.Instance.TriggerEvent(completedDoc.eventTriggerId);
            }

            // Wait for player to investigate and resolve via point-and-click
            if (completedDoc.pauseTypingForEvent)
            {
                while (EnvironmentalEventManager.Instance != null && EnvironmentalEventManager.Instance.IsEventActive(completedDoc.eventTriggerId))
                {
                    yield return null;
                }

                // Brief post-resolution acknowledgement on the terminal
                if (!string.IsNullOrEmpty(completedDoc.postResolutionMessage) && typingSystem != null)
                {
                    typingSystem.DisplaySystemMessage("TERMINAL SYSTEM", completedDoc.postResolutionMessage, true);
                    yield return new WaitForSeconds(2.0f);
                }
            }
        }

        // 3. Advance to the next story document
        AdvanceToNextDocument();
    }

    public void AdvanceToNextDocument()
    {
        CurrentDocumentIndex++;

        if (storyDocuments != null && CurrentDocumentIndex < storyDocuments.Length)
        {
            if (typingSystem != null)
            {
                typingSystem.RestoreTypingInput();
                typingSystem.LoadSpecificPhrase(CurrentDocument.headerTitle, CurrentDocument.documentText);
            }
        }
        else
        {
            // All story documents completed; fallback to standard phrase generation if quota remains
            IsStoryActive = false;
            Debug.Log("[StoryManager] All story progression documents completed. Reverting to standard pool.");
            
            if (typingSystem != null)
            {
                typingSystem.RestoreTypingInput();
                typingSystem.GenerateNewPhrase();
            }
        }
    }

    /// <summary>
    /// Automatically builds the chronological story document progression directly from Phrases.txt.
    /// </summary>
    private void LoadStoryFromPhraseFile()
    {
        var parsed = TypingSystem.ParsePhrasesFile(phraseFile.text);
        if (parsed.Count == 0)
        {
            SetupDefaultHorrorProgression();
            return;
        }

        List<StoryDocument> docs = new List<StoryDocument>();

        for (int i = 0; i < parsed.Count; i++)
        {
            var p = parsed[i];

            // Only queue documents as typing prompts (LOGs are displayed as computer alerts)
            if (p.header.StartsWith("LOG", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            StoryDocument doc = new StoryDocument
            {
                documentId = p.header,
                headerTitle = p.header,
                documentText = p.text,
                hasPostLog = false,
                eventTriggerId = ""
            };

            // Associate DOCUMENT #086 with the Breaker Outage Event
            if (p.header.Contains("086"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "LOG #003 // CRITICAL";
                doc.logMessage = "THE LIGHTS WILL BE CLOSING.\nRESTART THE BREAKER.";
                doc.eventTriggerId = "BreakerOutage";
                doc.pauseTypingForEvent = true;
                doc.postResolutionMessage = "[ POWER RESTORED // PROTOCOL RESUMED ]";
            }
            // Associate DOCUMENT #101 with First Anomalies Log Notice
            else if (p.header.Contains("101"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "SYS_NOTICE // 02:14";
                doc.logMessage = "LOG #005: DO NOT LEAVE YOUR WORKSTATION.";
                doc.logDisplayDelay = 2.5f;
            }
            // Associate DOCUMENT #104 with The Predictive Log
            else if (p.header.Contains("104"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "SYS_ALERT // 02:14";
                doc.logMessage = "THE LIGHTS WILL BE CLOSING.";
                doc.logDisplayDelay = 2.0f;
            }
            // --- SHIFT 2 DEPARTMENT OF METAPHYSICAL SCIENCES LOGS ---
            else if (p.header.Contains("203"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_NETWORK // AUDIT NOTICE";
                doc.logMessage = "LOG #201: CONTRADICTION DETECTED. DISREGARD EARLIER TIMELINE.";
                doc.logDisplayDelay = 2.0f;
            }
            else if (p.header.Contains("206"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_MONITOR // CLASSIFIED";
                doc.logMessage = "LOG #202: ARCHIVE PURGE FAILURE. STORAGE AREA 7 CANNOT BE ERASED.";
                doc.logDisplayDelay = 2.0f;
            }
            else if (p.header.Contains("212"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "TERMINAL_MONITOR // 03:33";
                doc.logMessage = "LOG #203: ACTIVE KEYSTROKES CONFIRMED. OPERATOR #447 MATCHES CURRENT TERMINAL.";
                doc.logDisplayDelay = 2.2f;
            }
            else if (p.header.Contains("216"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_PROTOCOL // PREDICTION";
                doc.logMessage = "LOG #204: YOU WILL LOOK BEHIND YOU NOW.";
                doc.logDisplayDelay = 2.2f;
            }
            else if (p.header.Contains("218"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_HAZARD // COGNITIVE LOAD";
                doc.logMessage = "LOG #206: DO NOT LOOK BEHIND YOUR CHAIR AGAIN. IT IS NO LONGER EMPTY.";
                doc.logDisplayDelay = 2.2f;
            }
            else if (p.header.Contains("221"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_SURVEILLANCE // PULSE RATE";
                doc.logMessage = "LOG #205: WE NOTICED YOU HESITATED ON THAT SENTENCE.";
                doc.logDisplayDelay = 2.0f;
            }
            else if (p.header.Contains("223"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_INTERCEPT // LAKE HALI";
                doc.logMessage = "LOG #207: THE NAME CARCOSA HAS BEEN COPIED INTO YOUR MEMORY.";
                doc.logDisplayDelay = 2.2f;
            }
            else if (p.header.Contains("224"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_AUDIT // CHECKING MANIFEST";
                doc.logMessage = "LOG #208: CHECKING EMPLOYEE ID AGAINST 1924 FOUNDING MANIFEST...";
                doc.logDisplayDelay = 2.2f;
            }
            else if (p.header.Contains("225"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_AUDIT // PARADOX CONFIRMED";
                doc.logMessage = "LOG #209: MATCH FOUND. OPERATOR #447 CONFIRMED.\nTHE ARCHIVAL CHAIN HAS NO ROOT RECORD.";
                doc.logDisplayDelay = 2.5f;
            }
            else if (p.header.Contains("226"))
            {
                doc.hasPostLog = true;
                doc.logHeader = "DMS_PERSONNEL // CONVERGENCE";
                doc.logMessage = "LOG #210: TRANSCRIPTION CHAIN COMPLETE. THERE IS NO ORIGINAL DOCUMENT. WELCOME HOME.";
                doc.logDisplayDelay = 3.0f;
            }

            docs.Add(doc);
        }

        storyDocuments = docs.ToArray();
        Debug.Log($"[StoryManager] Successfully imported {storyDocuments.Length} story documents from {phraseFile.name}!");
    }

    private void SetupDefaultGameplayTriggers()
    {
        gameplayTriggers = new List<NarrativeGameplayTrigger>
        {
            new NarrativeGameplayTrigger
            {
                triggerId = "Shift2_Quota50",
                shiftRequired = 2,
                type = NarrativeGameplayTrigger.TriggerType.QuotaPercentage,
                thresholdValue = 50f,
                alertHeader = "DMS ARCHIVE INTERCEPT // 50% QUOTA",
                alertMessage = "ANOMALOUS DATA STABILIZATION DETECTED.\nDO NOT RE-READ PREVIOUS LINES. THE WORDS HAVE ALREADY RECORDED YOU.",
                displayDuration = 2.5f
            },
            new NarrativeGameplayTrigger
            {
                triggerId = "Shift2_Corruption30",
                shiftRequired = 2,
                type = NarrativeGameplayTrigger.TriggerType.CorruptionLevel,
                thresholdValue = 30f,
                alertHeader = "DMS COGNITIVE AUDIT // WARNING",
                alertMessage = "HAZARD ADVISORY: ELEVATED CEREBRAL RESONANCE DETECTED AT TERMINAL 4.\nLOOK AWAY TO VENT RESIDUAL CONTAMINATION.",
                displayDuration = 2.5f
            },
            new NarrativeGameplayTrigger
            {
                triggerId = "Shift2_Corruption60",
                shiftRequired = 2,
                type = NarrativeGameplayTrigger.TriggerType.CorruptionLevel,
                thresholdValue = 60f,
                alertHeader = "UNKNOWN TRANSMISSION // THE YELLOW SIGN",
                alertMessage = "ALONG THE SHORE THE CLOUD WAVES BREAK.\nTHE TWIN SUNS SINK BEHIND THE LAKE. CARCOSA CALLS.",
                displayDuration = 2.5f
            },
            new NarrativeGameplayTrigger
            {
                triggerId = "Shift2_Time55",
                shiftRequired = 2,
                type = NarrativeGameplayTrigger.TriggerType.TimeElapsed,
                thresholdValue = 55f,
                alertHeader = "DMS INTERNAL DIRECTIVE // 03:40",
                alertMessage = "DEPARTMENT NOTICE: REGULATION 12-A REMAINS IN EFFECT.\nUNDER NO CIRCUMSTANCES UTTER THE NAME HASTUR.",
                displayDuration = 2.5f
            }
        };
    }

    /// <summary>
    /// Establishes the default psychological horror story arc if unconfigured in Inspector.
    /// </summary>
    private void SetupDefaultHorrorProgression()
    {
        storyDocuments = new StoryDocument[]
        {
            // Stage 1: Normal Government Record
            new StoryDocument
            {
                documentId = "DOC_001",
                category = StoryDocument.DocumentCategory.NormalRecord,
                headerTitle = "DOCUMENT #00182",
                documentText = "The shipment arrived at 14:32. Three crates were unloaded from the transport vehicle. All seals remained intact.",
                hasPostLog = false,
                eventTriggerId = ""
            },

            // Stage 2: Strange Record
            new StoryDocument
            {
                documentId = "DOC_002",
                category = StoryDocument.DocumentCategory.StrangeRecord,
                headerTitle = "INCIDENT #00419",
                documentText = "Sub-level 3 power grid experienced an unscheduled harmonic resonance. Field operatives reported auditory hums beneath the flooring.",
                hasPostLog = true,
                logHeader = "SYS_NOTICE // 02:45",
                logMessage = "WARNING: Local power draw exceeds authorized thresholds. Regulators unstable.",
                logDisplayDelay = 2.5f,
                eventTriggerId = ""
            },

            // Stage 3: Predictive Log -> First Breaker Event
            new StoryDocument
            {
                documentId = "DOC_003",
                category = StoryDocument.DocumentCategory.PredictiveLog,
                headerTitle = "DOCUMENT #086",
                documentText = "Auxiliary relay 09 severed. Structural illumination in central processing office will terminate in ten seconds.",
                hasPostLog = true,
                logHeader = "SYS_ALERT // 03:14",
                logMessage = "THE LIGHTS WILL BE CLOSING.\nRESTART THE BREAKER.",
                logDisplayDelay = 2.0f,
                eventTriggerId = "BreakerOutage",
                pauseTypingForEvent = true,
                postResolutionMessage = "[ POWER RESTORED // PROTOCOL RESUMED ]"
            },

            // Stage 4: Shift 2 DMS Inscription Directive
            new StoryDocument
            {
                documentId = "DOC_201",
                category = StoryDocument.DocumentCategory.NormalRecord,
                headerTitle = "DOCUMENT #201",
                documentText = "Records relating to the eastern archive wing have been transferred to Storage Area 7.",
                hasPostLog = false,
                eventTriggerId = ""
            },

            // Stage 5: Shift 2 Anomaly & Contradiction
            new StoryDocument
            {
                documentId = "DOC_215",
                category = StoryDocument.DocumentCategory.StrangeRecord,
                headerTitle = "DOCUMENT #215",
                documentText = "Storage Area 7 was excavated thirty meters below the foundation where no foundation was ever poured.",
                hasPostLog = true,
                logHeader = "DMS_ARCHIVE // AUDIT NOTICE",
                logMessage = "LOG #201: DISCREPANCY DETECTED IN FACILITY GENESIS TIMELINE.",
                logDisplayDelay = 2.0f,
                eventTriggerId = ""
            },

            // Stage 6: The Computer Recognizing The Player
            new StoryDocument
            {
                documentId = "DOC_004",
                category = StoryDocument.DocumentCategory.DirectEntityCommunication,
                headerTitle = "DOCUMENT #220",
                documentText = "Department surveillance confirms that Terminal 4 operator is progressing as scheduled. Do not alert them to the locked doors.",
                hasPostLog = true,
                logHeader = "DMS_COMMAND // CLASSIFIED",
                logMessage = "LOG #207: SHIFT 2 QUOTA NEAR TARGET. FACILITY LOCKS ENGAGED.",
                logDisplayDelay = 2.5f,
                eventTriggerId = ""
            }
        };
    }
}

/// <summary>
/// Defines a dynamic gameplay condition that triggers psychological horror broadcasts or Department intercepts alongside typing.
/// </summary>
[System.Serializable]
public class NarrativeGameplayTrigger
{
    public enum TriggerType
    {
        QuotaPercentage,  // Trigger when % quota reached (e.g. 50%)
        CorruptionLevel,  // Trigger when corruption exceeds threshold (e.g. 30%)
        TimeElapsed       // Trigger after time elapsed in shift (e.g. 55s)
    }

    public string triggerId = "Trigger_01";
    public int shiftRequired = 2; // Which shift this trigger belongs to (0 for any)
    public TriggerType type = TriggerType.QuotaPercentage;
    public float thresholdValue = 50f;

    [Header("Terminal Intercept Content")]
    public string alertHeader = "DMS_SURVEILLANCE // ALERT";
    [TextArea(2, 4)]
    public string alertMessage = "OPERATOR RECORD TRANSCRIPTION: PROGRESS MONITORED.";
    public float displayDuration = 2.5f;

    [HideInInspector]
    public bool hasTriggered = false;
}
