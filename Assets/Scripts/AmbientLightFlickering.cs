using UnityEngine;
using UnityEngine.Rendering.Universal;

public class AmbientLightFlicker : MonoBehaviour
{
    private Light2D lampLight;

    [Header("Intensity Range")]
    [Tooltip("The minimum light intensity during a flicker.")]
    public float minIntensity = 0.7f;
    
    [Tooltip("The maximum normal light intensity.")]
    public float maxIntensity = 1.1f;

    [Header("Flicker Speed & Smoothness")]
    [Tooltip("Higher values make it flicker faster.")]
    public float flickerSpeed = 5f;

    [Header("Occasional Heavy Dips (Spooky Effect)")]
    [Tooltip("Enable for rare, sudden dark stutters.")]
    public bool enableSpookyDips = true;
    public float dipChance = 0.02f; // 2% chance per frame

    private float targetIntensity;
    private float noiseSeed;

    void Start()
    {
        lampLight = GetComponent<Light2D>();
        noiseSeed = Random.Range(0f, 100f);
    }

    void Update()
    {
        if (lampLight == null) return;

        // Use Perlin noise for a smooth, natural-looking warmth flicker
        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * flickerSpeed);
        targetIntensity = Mathf.Lerp(minIntensity, maxIntensity, noise);

        // Optional: Occasional sudden dark stutter/pop
        if (enableSpookyDips && Random.value < dipChance)
        {
            targetIntensity = minIntensity * 0.3f;
        }

        // Apply smooth transition to intensity
        lampLight.intensity = Mathf.Lerp(lampLight.intensity, targetIntensity, Time.deltaTime * 12f);
    }
}