using UnityEngine;
using UnityEngine.Rendering.Universal; // Required for Light2D

[RequireComponent(typeof(Light2D))]
public class CRTGlowFlicker : MonoBehaviour
{
    private Light2D targetLight;

    [Header("Base Settings")]
    public float baseIntensity = 1.0f;
    public float flickerRange = 0.15f;

    [Header("Flicker Speed")]
    public float flickerSpeed = 12.0f;

    void Start()
    {
        targetLight = GetComponent<Light2D>();
    }

    void Update()
    {
        if (targetLight == null) return;

        // Uses Noise to create smooth, retro phosphor pulsing
        float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0.0f);
        targetLight.intensity = baseIntensity + (noise - 0.5f) * (flickerRange * 2f);
    }
}