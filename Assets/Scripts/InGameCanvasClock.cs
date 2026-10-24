using UnityEngine;
using TMPro;

public class DigitalClock : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private TMP_Text clockText;
    [Tooltip("Optional image or screen backlight for the digital clock")]
    [SerializeField] private UnityEngine.UI.Image clockDisplayImage;
    [Tooltip("Optional 2D sprite renderer if the clock is a world object")]
    [SerializeField] private SpriteRenderer clockSpriteRenderer;

    [Header("Dependencies")]
    [SerializeField] private GameManager gameManager;

    [Header("Clock Settings")]
    [SerializeField] private int startHour = 12; // Starting hour (e.g., 12 AM)
    [SerializeField] private bool isAM = true;
    [SerializeField] private bool enableFlashingColon = true;

    private float flashTimer;
    private bool showColon = true;
    private bool isPowerOff = false;

    // Cache to prevent per-frame string allocation and UI mesh rebuilds
    private int lastHour = -1;
    private int lastMinutes = -1;
    private bool lastAM = false;
    private bool lastShowColon = false;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>(FindObjectsInactive.Include);
        }

        if (clockText == null)
        {
            clockText = GetComponent<TMP_Text>();
            if (clockText == null)
            {
                clockText = GetComponentInChildren<TMP_Text>(true);
            }
        }

        if (clockDisplayImage == null)
        {
            clockDisplayImage = GetComponent<UnityEngine.UI.Image>();
        }

        if (clockSpriteRenderer == null)
        {
            clockSpriteRenderer = GetComponent<SpriteRenderer>();
        }
    }

    public void SetPower(bool hasPower)
    {
        isPowerOff = !hasPower;
        if (!hasPower)
        {
            if (clockText != null)
            {
                clockText.text = "";
                clockText.enabled = false;
            }
            if (clockDisplayImage != null)
            {
                clockDisplayImage.enabled = false;
            }
            if (clockSpriteRenderer != null)
            {
                clockSpriteRenderer.enabled = false;
            }
            lastMinutes = -1; // Force refresh when power returns
        }
        else
        {
            if (clockText != null)
            {
                clockText.enabled = true;
            }
            if (clockDisplayImage != null)
            {
                clockDisplayImage.enabled = true;
            }
            if (clockSpriteRenderer != null)
            {
                clockSpriteRenderer.enabled = true;
            }
            lastMinutes = -1;
        }
    }

    void Update()
    {
        if (isPowerOff)
        {
            if (clockText != null && !string.IsNullOrEmpty(clockText.text))
            {
                clockText.text = "";
            }
            return;
        }

        if (gameManager == null || clockText == null) return;

        bool colonChanged = false;

        // Toggle colon visibility every 0.5 seconds
        if (enableFlashingColon)
        {
            flashTimer += Time.deltaTime;
            if (flashTimer >= 0.5f)
            {
                flashTimer = 0f;
                showColon = !showColon;
                colonChanged = true;
            }
        }
        else if (!showColon)
        {
            showColon = true;
            colonChanged = true;
        }

        // Calculate hours and minutes from GameManager timer
        float gameTimer = gameManager.GetGameTimer();
        int totalInGameMinutes = Mathf.FloorToInt(gameTimer); 
        
        int minutes = totalInGameMinutes % 60;
        int hoursAdded = totalInGameMinutes / 60;
        
        int currentHour = (startHour + hoursAdded) % 12;
        if (currentHour == 0) currentHour = 12;

        bool currentAM = isAM;
        if (((startHour + hoursAdded) / 12) % 2 != 0)
        {
            currentAM = !isAM;
        }

        // Only rebuild the text mesh when the time or colon state actually changed
        if (minutes != lastMinutes || currentHour != lastHour || currentAM != lastAM || colonChanged || showColon != lastShowColon)
        {
            lastMinutes = minutes;
            lastHour = currentHour;
            lastAM = currentAM;
            lastShowColon = showColon;

            string colonSymbol = showColon ? ":" : " ";
            string amPmString = currentAM ? "AM" : "PM";

            clockText.text = $"{currentHour:D2}{colonSymbol}{minutes:D2} {amPmString}";
        }
    }
}