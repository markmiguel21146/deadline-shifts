using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Eyelid blinking / waking transition at the terminal desk.
/// Features unscaled timing, audio playback, skip support, and safe boot handoff.
/// </summary>
public class EyeOpeningTransition : MonoBehaviour
{
    [Header("Eyelid RectTransforms")]
    public RectTransform topEyelid;
    public RectTransform bottomEyelid;

    [Header("Audio Settings")]
    public AudioSource audioSource;
    public AudioClip workVoiceLine;

    [Header("Transition Settings")]
    public float duration = 2.0f;
    public float moveDistance = 600f;

    [Header("Next Sequence")]
    public OSBootSequence bootSequence;

    private bool isTransitioning = false;
    private bool isSkipping = false;
    private Coroutine transitionCoroutine;

    private void Awake()
    {
        EnsureEyelidUI();
        AutoDiscoverComponents();
    }

    private void Start()
    {
        // Keep eyelids inactive until explicitly played by ShiftTitleCard
        if (!isTransitioning)
        {
            if (topEyelid != null) topEyelid.gameObject.SetActive(false);
            if (bottomEyelid != null) bottomEyelid.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isTransitioning && !isSkipping)
        {
            // Allow skipping via Space, Enter, KeypadEnter, Escape, or Left Click
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) ||
                Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetMouseButtonDown(0))
            {
                SkipTransition();
            }
        }
    }

    public void PlayTransition()
    {
        Debug.Log("[EyeOpeningTransition] PlayTransition called.");
        if (isTransitioning) return;
        isTransitioning = true;
        isSkipping = false;

        EnsureEyelidUI();
        AutoDiscoverComponents();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        transitionCoroutine = StartCoroutine(OpenEyesRoutine());
    }

    public void SkipTransition()
    {
        if (!isTransitioning || isSkipping) return;
        isSkipping = true;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
        }

        float dist = (moveDistance > 0f) ? moveDistance : 600f;
        if (topEyelid != null) topEyelid.anchoredPosition = new Vector2(0f, dist);
        if (bottomEyelid != null) bottomEyelid.anchoredPosition = new Vector2(0f, -dist);

        FinishTransition();
    }

    private IEnumerator OpenEyesRoutine()
    {
        isTransitioning = true;
        float elapsed = 0f;

        EnsureEyelidUI();

        if (topEyelid != null)
        {
            topEyelid.gameObject.SetActive(true);
            topEyelid.anchoredPosition = Vector2.zero;
        }

        if (bottomEyelid != null)
        {
            bottomEyelid.gameObject.SetActive(true);
            bottomEyelid.anchoredPosition = Vector2.zero;
        }

        Vector2 topStartPos = Vector2.zero;
        Vector2 bottomStartPos = Vector2.zero;

        float dist = (moveDistance > 0f) ? moveDistance : 600f;
        Vector2 topTargetPos = new Vector2(0f, dist);
        Vector2 bottomTargetPos = new Vector2(0f, -dist);

        yield return new WaitForSecondsRealtime(0.12f);

        if (audioSource != null && workVoiceLine != null)
        {
            audioSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            audioSource.PlayOneShot(workVoiceLine, 0.7f);
        }

        float openDuration = (duration > 0.5f && duration <= 3.5f) ? duration : 2.0f;

        while (elapsed < openDuration)
        {
            if (isSkipping) yield break;

            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, elapsed / openDuration);

            if (topEyelid != null)
                topEyelid.anchoredPosition = Vector2.Lerp(topStartPos, topTargetPos, progress);

            if (bottomEyelid != null)
                bottomEyelid.anchoredPosition = Vector2.Lerp(bottomStartPos, bottomTargetPos, progress);

            yield return null;
        }

        FinishTransition();
    }

    private void FinishTransition()
    {
        Debug.Log("[EyeOpeningTransition] FinishTransition called.");

        if (topEyelid != null) topEyelid.gameObject.SetActive(false);
        if (bottomEyelid != null) bottomEyelid.gameObject.SetActive(false);

        isTransitioning = false;

        // Trigger OS Boot Sequence on monitor
        if (bootSequence == null)
        {
            bootSequence = FindAnyObjectByType<OSBootSequence>(FindObjectsInactive.Include);
        }

        if (bootSequence != null)
        {
            Debug.Log("[EyeOpeningTransition] Starting OSBootSequence.");
            bootSequence.gameObject.SetActive(true);
            bootSequence.StartBoot();
        }
        else
        {
            Debug.LogWarning("[EyeOpeningTransition] BootSequence not found! Resuming gameplay directly.");
            TypingSystem typingSys = FindAnyObjectByType<TypingSystem>(FindObjectsInactive.Include);
            if (typingSys != null)
            {
                typingSys.RestoreTypingInput();
                typingSys.AutoRefocusInput(true);
            }
            GameManager.isCutscenePlaying = false;
        }

        gameObject.SetActive(false);
    }

    public void EnsureEyelidUI()
    {
        // 1. Container fills entire screen
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }

        // Disable rogue WorldSpace Canvas component so eyelids render directly in WorkplaceCanvas ScreenSpaceCamera
        Canvas canvas = GetComponent<Canvas>();
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.WorldSpace)
            {
                canvas.enabled = false;
            }
            else
            {
                canvas.overrideSorting = true;
                Canvas targetCanvas = GetComponentInParent<Canvas>();
                if (targetCanvas != null && targetCanvas != canvas)
                {
                    canvas.sortingLayerID = targetCanvas.sortingLayerID;
                    canvas.sortingLayerName = targetCanvas.sortingLayerName;
                }
                else
                {
                    canvas.sortingLayerName = "UI";
                }
                canvas.sortingOrder = 900;
            }
        }

        // 2. Discover eyelids
        if (topEyelid == null || bottomEyelid == null)
        {
            RectTransform[] allRects = GetComponentsInChildren<RectTransform>(true);
            foreach (var r in allRects)
            {
                if (topEyelid == null && r.name.IndexOf("top", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    topEyelid = r;
                }
                if (bottomEyelid == null && r.name.IndexOf("bottom", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    bottomEyelid = r;
                }
            }
        }

        // Top eyelid: covers upper half (pivot at bottom edge y=0, anchor at y=0.5 to y=1.0)
        if (topEyelid != null)
        {
            topEyelid.anchorMin = new Vector2(0f, 0.5f);
            topEyelid.anchorMax = new Vector2(1f, 1f);
            topEyelid.pivot = new Vector2(0.5f, 0f); // Pivot at bottom edge of top eyelid
            topEyelid.offsetMin = Vector2.zero;
            topEyelid.offsetMax = Vector2.zero;
            topEyelid.localScale = Vector3.one;

            Image topImg = topEyelid.GetComponent<Image>();
            if (topImg == null) topImg = topEyelid.gameObject.AddComponent<Image>();
            topImg.color = Color.black;
            topImg.raycastTarget = false;
        }

        // Bottom eyelid: covers lower half (pivot at top edge y=1, anchor at y=0.0 to y=0.5)
        if (bottomEyelid != null)
        {
            bottomEyelid.anchorMin = new Vector2(0f, 0f);
            bottomEyelid.anchorMax = new Vector2(1f, 0.5f);
            bottomEyelid.pivot = new Vector2(0.5f, 1f); // Pivot at top edge of bottom eyelid
            bottomEyelid.offsetMin = Vector2.zero;
            bottomEyelid.offsetMax = Vector2.zero;
            bottomEyelid.localScale = Vector3.one;

            Image botImg = bottomEyelid.GetComponent<Image>();
            if (botImg == null) botImg = bottomEyelid.gameObject.AddComponent<Image>();
            botImg.color = Color.black;
            botImg.raycastTarget = false;
        }
    }

    private void AutoDiscoverComponents()
    {
        if (bootSequence == null)
        {
            bootSequence = FindAnyObjectByType<OSBootSequence>(FindObjectsInactive.Include);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        if (workVoiceLine == null)
        {
            AudioClip[] clips = Resources.FindObjectsOfTypeAll<AudioClip>();
            foreach (var c in clips)
            {
                if (c != null && c.name.IndexOf("start", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    workVoiceLine = c;
                    break;
                }
            }
        }
    }
}