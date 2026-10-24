using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages point-and-click environmental interactions within the OfficeBehind backroom view.
/// Provides interactive inspection for the Storage Area 7 filing cabinet and the surveillance camera.
/// </summary>
public class OfficeBehindInteractions : MonoBehaviour
{
    [Header("Inspection Popup UI")]
    private GameObject inspectionModal;
    private TextMeshProUGUI modalHeaderText;
    private TextMeshProUGUI modalBodyText;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        SetupInteractions();
    }

    public void SetupInteractions()
    {
        EnsureModalUI();
        EnsureHotspots();
    }

    private void EnsureHotspots()
    {
        // 1. Storage Area 7 Classified Filing Cabinet Hotspot (left/mid-left of backroom)
        Transform cabinetTransform = transform.Find("Hotspot_Cabinet");
        if (cabinetTransform == null)
        {
            GameObject cabObj = new GameObject("Hotspot_Cabinet");
            cabObj.transform.SetParent(transform, false);

            RectTransform rt = cabObj.AddComponent<RectTransform>();
            // Positioned around mid-left filing cabinets
            rt.anchorMin = new Vector2(0.18f, 0.25f);
            rt.anchorMax = new Vector2(0.38f, 0.65f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            ClickableHotspot hotspot = cabObj.AddComponent<ClickableHotspot>();
            hotspot.hotspotName = "[ INSPECT CLASSIFIED CABINET ]";
            hotspot.enableHoverAlpha = true;
            hotspot.normalAlpha = 0.05f;
            hotspot.hoverAlpha = 0.25f;
            hotspot.SetupVisualCue("[ INSPECT CLASSIFIED CABINET ]", new Color(0.75f, 0.85f, 0.80f, 1f), true, 0.05f, 0.25f);

            if (hotspot.onClicked == null) hotspot.onClicked = new UnityEngine.Events.UnityEvent();
            hotspot.onClicked.AddListener(() =>
            {
                ShowCabinetDossier();
            });
        }

        // 2. Remove Surveillance Camera Hotspot if previously present
        Transform camTransform = transform.Find("Hotspot_Camera");
        if (camTransform != null)
        {
            Destroy(camTransform.gameObject);
        }
    }

    private void EnsureModalUI()
    {
        if (inspectionModal != null) return;

        Transform existing = transform.Find("InspectionModal");
        if (existing != null)
        {
            // Destroy existing legacy modal so new spacious layout rebuilds cleanly
            Destroy(existing.gameObject);
        }

        // Fullscreen translucent black backdrop
        inspectionModal = new GameObject("InspectionModal");
        inspectionModal.transform.SetParent(transform, false);

        RectTransform modalRt = inspectionModal.AddComponent<RectTransform>();
        modalRt.anchorMin = Vector2.zero;
        modalRt.anchorMax = Vector2.one;
        modalRt.offsetMin = Vector2.zero;
        modalRt.offsetMax = Vector2.zero;

        Image backdrop = inspectionModal.AddComponent<Image>();
        backdrop.color = new Color(0f, 0f, 0f, 0.82f);

        Button backdropBtn = inspectionModal.AddComponent<Button>();
        backdropBtn.onClick.AddListener(CloseModal);

        // Center dossier container (Spacious 980 x 640 archival dossier)
        GameObject box = new GameObject("Container");
        box.transform.SetParent(inspectionModal.transform, false);

        RectTransform boxRt = box.AddComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(980f, 640f);

        Image boxBg = box.AddComponent<Image>();
        boxBg.color = new Color(0.11f, 0.12f, 0.14f, 0.98f); // Retro weathered charcoal/slate dossier

        Outline outline = box.AddComponent<Outline>();
        outline.effectColor = new Color(0.48f, 0.52f, 0.56f, 0.85f); // Crisp retro steel border
        outline.effectDistance = new Vector2(1.5f, 1.5f);

        // Header Banner
        GameObject headerObj = new GameObject("Header");
        headerObj.transform.SetParent(box.transform, false);
        RectTransform headerRt = headerObj.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -16f);
        headerRt.sizeDelta = new Vector2(-50f, 40f);

        modalHeaderText = headerObj.AddComponent<TextMeshProUGUI>();
        modalHeaderText.fontSize = 18;
        modalHeaderText.enableAutoSizing = true;
        modalHeaderText.fontSizeMin = 13;
        modalHeaderText.fontSizeMax = 18;
        modalHeaderText.fontStyle = FontStyles.Bold;
        modalHeaderText.characterSpacing = 1.2f;
        modalHeaderText.alignment = TextAlignmentOptions.Center;
        modalHeaderText.textWrappingMode = TextWrappingModes.NoWrap;
        modalHeaderText.color = new Color(0.92f, 0.94f, 0.96f, 1f); // Crisp silver-white

        // Header Divider Line
        GameObject divObj = new GameObject("Divider");
        divObj.transform.SetParent(box.transform, false);
        RectTransform divRt = divObj.AddComponent<RectTransform>();
        divRt.anchorMin = new Vector2(0f, 1f);
        divRt.anchorMax = new Vector2(1f, 1f);
        divRt.pivot = new Vector2(0.5f, 1f);
        divRt.anchoredPosition = new Vector2(0f, -60f);
        divRt.sizeDelta = new Vector2(-50f, 1.5f);

        Image divImg = divObj.AddComponent<Image>();
        divImg.color = new Color(0.38f, 0.42f, 0.46f, 0.70f);

        // Body Text Viewport Container (with RectMask2D so text NEVER bleeds outside)
        GameObject bodyObj = new GameObject("Body");
        bodyObj.transform.SetParent(box.transform, false);
        RectTransform bodyRt = bodyObj.AddComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.offsetMin = new Vector2(50f, 75f);
        bodyRt.offsetMax = new Vector2(-50f, -75f);

        bodyObj.AddComponent<RectMask2D>();

        // Text Component
        GameObject textContentObj = new GameObject("TextContent");
        textContentObj.transform.SetParent(bodyObj.transform, false);
        RectTransform tcRt = textContentObj.AddComponent<RectTransform>();
        tcRt.anchorMin = Vector2.zero;
        tcRt.anchorMax = Vector2.one;
        tcRt.pivot = new Vector2(0.5f, 0.5f);
        tcRt.offsetMin = Vector2.zero;
        tcRt.offsetMax = Vector2.zero;

        modalBodyText = textContentObj.AddComponent<TextMeshProUGUI>();
        modalBodyText.fontSize = 17f;
        modalBodyText.enableAutoSizing = true;
        modalBodyText.fontSizeMin = 13.5f;
        modalBodyText.fontSizeMax = 17.5f;
        modalBodyText.lineSpacing = 7f;
        modalBodyText.paragraphSpacing = 10f;
        modalBodyText.characterSpacing = 0.5f;
        modalBodyText.alignment = TextAlignmentOptions.TopLeft;
        modalBodyText.textWrappingMode = TextWrappingModes.Normal;
        modalBodyText.overflowMode = TextOverflowModes.Truncate;
        modalBodyText.color = new Color(0.90f, 0.92f, 0.95f, 1f); // Soft retro paper white

        // Bottom Close Button
        GameObject closeObj = new GameObject("CloseButton");
        closeObj.transform.SetParent(box.transform, false);
        RectTransform closeRt = closeObj.AddComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0f);
        closeRt.anchorMax = new Vector2(0.5f, 0f);
        closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.anchoredPosition = new Vector2(0f, 20f);
        closeRt.sizeDelta = new Vector2(300f, 42f);

        Image closeBg = closeObj.AddComponent<Image>();
        closeBg.color = new Color(0.18f, 0.20f, 0.23f, 0.95f);

        Outline closeOutline = closeObj.AddComponent<Outline>();
        closeOutline.effectColor = new Color(0.50f, 0.54f, 0.58f, 0.75f);
        closeOutline.effectDistance = new Vector2(1f, 1f);

        GameObject closeTextObj = new GameObject("Text");
        closeTextObj.transform.SetParent(closeObj.transform, false);
        RectTransform ctRt = closeTextObj.AddComponent<RectTransform>();
        ctRt.anchorMin = Vector2.zero;
        ctRt.anchorMax = Vector2.one;
        ctRt.offsetMin = Vector2.zero;
        ctRt.offsetMax = Vector2.zero;

        TextMeshProUGUI closeTmp = closeTextObj.AddComponent<TextMeshProUGUI>();
        closeTmp.text = "[ CLOSE DOSSIER (ESC / SPACE) ]";
        closeTmp.fontSize = 14;
        closeTmp.characterSpacing = 0.8f;
        closeTmp.alignment = TextAlignmentOptions.Center;
        closeTmp.color = new Color(0.88f, 0.92f, 0.95f, 1f);
        closeTmp.fontStyle = FontStyles.Bold;
        closeTmp.textWrappingMode = TextWrappingModes.NoWrap;

        Button closeBtn = closeObj.AddComponent<Button>();
        ColorBlock cb = closeBtn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.28f, 0.32f, 0.36f, 1f);
        cb.pressedColor = new Color(0.10f, 0.12f, 0.14f, 1f);
        closeBtn.colors = cb;
        closeBtn.onClick.AddListener(CloseModal);

        // Inherit scene font if available
        TMP_Text fontSample = FindAnyObjectByType<TMP_Text>(FindObjectsInactive.Include);
        if (fontSample != null && fontSample.font != null)
        {
            modalHeaderText.font = fontSample.font;
            modalBodyText.font = fontSample.font;
            closeTmp.font = fontSample.font;
        }

        inspectionModal.SetActive(false);
    }

    public void ShowCabinetDossier()
    {
        EnsureModalUI();
        if (inspectionModal == null) return;

        modalHeaderText.text = "DEPARTMENT OF METAPHYSICAL SCIENCES // REPORT 1974-B";
        modalBodyText.text =
            "<b>SUBJECT: DISCREPANCY AUDIT — STORAGE AREA 7</b>\n\n" +
            "Sealed under Executive Order 44-D. Access strictly restricted.\n\n" +
            "On October 14, 1974, Lead Investigator Thorne entered Storage Area 7 to verify anomalous ledger counts. Upon attempting exit, no doorway was present.\n\n" +
            "Recovery teams excavated thirty meters below the foundation, locating the investigator's badge beside a blank concrete wall inscribed with the <i>Yellow Sign</i>.\n\n" +
            "<color=#D4AF37>Notice: Personnel are strictly forbidden from cross-referencing this ledger with records from 1994. The dates are known to coincide.</color>";

        inspectionModal.SetActive(true);
        inspectionModal.transform.SetAsLastSibling();
    }

    public bool IsModalOpen => inspectionModal != null && inspectionModal.activeSelf;

    private void Update()
    {
        if (IsModalOpen)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                CloseModal();
            }
        }
    }

    public void CloseModal()
    {
        if (inspectionModal != null)
        {
            inspectionModal.SetActive(false);
        }
    }
}
