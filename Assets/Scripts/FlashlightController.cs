using UnityEngine;
using UnityEngine.Rendering.Universal;

public class FlashlightController : MonoBehaviour
{
    [Header("Target & Proximity")]
    public Transform kingInYellowArea;
    public float detectRadius = 3f;
    public float failureRadius = 1.2f;

    [Header("Light Settings")]
    public float baseIntensity = 1.0f;
    public float flickerSpeed = 30f;

    [Header("Manager Reference")]
    public GameManager gameManager;

    private Light2D flashLight;
    private Camera mainCam;

    void Start()
    {
        if (flashLight == null) flashLight = GetComponent<Light2D>();
        if (flashLight == null) flashLight = GetComponentInChildren<Light2D>(true);
        mainCam = Camera.main;

        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        if (flashLight != null)
        {
            flashLight.enabled = false;
        }
    }

    void Update()
    {
        if (PauseMenuManager.IsPaused) return;
        if (mainCam == null) mainCam = Camera.main;
        if (flashLight == null || mainCam == null) return;

        // 1. Flashlight is strictly OFF unless the player is looking behind (in OfficeBehind)
        bool inOfficeBehind = LocationManager.Instance != null && LocationManager.Instance.CurrentLocationId.Equals("OfficeBehind", System.StringComparison.OrdinalIgnoreCase);
        bool isLookingBehind = (gameManager != null && gameManager.isLookingAway) || inOfficeBehind;

        if (!isLookingBehind)
        {
            if (flashLight.enabled) flashLight.enabled = false;
            return;
        }

        // 2. Turn flashlight on once looking behind
        if (!flashLight.enabled)
        {
            flashLight.enabled = true;
        }

        // 3. Follow mouse position in world space
        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(Input.mousePosition);
        mouseWorldPos.z = 0f;
        transform.position = mouseWorldPos;

        if (kingInYellowArea == null)
        {
            flashLight.intensity = baseIntensity;
            return;
        }

        // 4. Calculate distance from cursor light to the entity (using squared distance to avoid square root overhead)
        float sqrDistance = ((Vector2)transform.position - (Vector2)kingInYellowArea.position).sqrMagnitude;
        float sqrFailure = failureRadius * failureRadius;
        float sqrDetect = detectRadius * detectRadius;

        if (sqrDistance <= sqrFailure)
        {
            // Flashlight cuts out entirely
            flashLight.intensity = 0f;
        }
        else if (sqrDistance <= sqrDetect)
        {
            // Rapid flickering effect
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f);
            flashLight.intensity = Mathf.Lerp(0f, baseIntensity, noise);
        }
        else
        {
            // Normal intensity
            flashLight.intensity = baseIntensity;
        }
    }
}