using UnityEngine;
using UnityEngine.UI;

public class MenuBackgroundCrossfade : MonoBehaviour
{
    [Header("Canvas Groups")]
    public CanvasGroup startGameGroup;
    public CanvasGroup howToPlayGroup;
    public CanvasGroup quitGameGroup;

    [Header("Transition Settings")]
    public float fadeSpeed = 10f;

    private float targetStartAlpha = 0f;
    private float targetHowToPlayAlpha = 0f;
    private float targetQuitAlpha = 0f;

    private void Awake()
    {
        AutoDiscoverCanvasGroups();
    }

    public void AutoDiscoverCanvasGroups()
    {
        if (startGameGroup == null)
        {
            startGameGroup = GameObject.Find("StartGameBG")?.GetComponent<CanvasGroup>();
        }
        if (howToPlayGroup == null)
        {
            howToPlayGroup = GameObject.Find("HowToPlayBG")?.GetComponent<CanvasGroup>();
        }
        if (quitGameGroup == null)
        {
            quitGameGroup = GameObject.Find("QuitGameBG")?.GetComponent<CanvasGroup>();
        }
    }

    void Update()
    {
        // Fade StartGameBG smoothly over Neutral
        if (startGameGroup != null && !Mathf.Approximately(startGameGroup.alpha, targetStartAlpha))
        {
            startGameGroup.alpha = Mathf.MoveTowards(startGameGroup.alpha, targetStartAlpha, Time.deltaTime * fadeSpeed);
        }

        // Fade HowToPlayBG smoothly over Neutral
        if (howToPlayGroup != null && !Mathf.Approximately(howToPlayGroup.alpha, targetHowToPlayAlpha))
        {
            howToPlayGroup.alpha = Mathf.MoveTowards(howToPlayGroup.alpha, targetHowToPlayAlpha, Time.deltaTime * fadeSpeed);
        }

        // Fade QuitGameBG smoothly over Neutral
        if (quitGameGroup != null && !Mathf.Approximately(quitGameGroup.alpha, targetQuitAlpha))
        {
            quitGameGroup.alpha = Mathf.MoveTowards(quitGameGroup.alpha, targetQuitAlpha, Time.deltaTime * fadeSpeed);
        }
    }

    public void ShowStartGameBG()
    {
        targetStartAlpha = 1f;
        targetHowToPlayAlpha = 0f;
        targetQuitAlpha = 0f;
    }

    public void ShowHowToPlayBG()
    {
        targetStartAlpha = 0f;
        targetHowToPlayAlpha = 1f;
        targetQuitAlpha = 0f;
    }

    public void ShowQuitGameBG()
    {
        targetStartAlpha = 0f;
        targetHowToPlayAlpha = 0f;
        targetQuitAlpha = 1f;
    }

    public void ShowNeutralBG()
    {
        targetStartAlpha = 0f;
        targetHowToPlayAlpha = 0f;
        targetQuitAlpha = 0f;
    }
}