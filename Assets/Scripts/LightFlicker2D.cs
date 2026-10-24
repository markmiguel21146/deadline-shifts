using UnityEngine;
using UnityEngine.Rendering.Universal;

[RequireComponent(typeof(Light2D))]
public class LightFlicker2D : MonoBehaviour
{
    public enum FlickerType
    {
        PerlinSmooth,   // Phosphor glow / warm ambient flicker (Perlin noise)
        RandomPulse,    // CRT monitor intermittent pulse
        BrokenStutter   // Failing fluorescent / spooky broken bulb (timer state machine)
    }

    [Header("Mode")]
    [SerializeField] private FlickerType flickerType = FlickerType.PerlinSmooth;

    [Header("Intensity Bounds")]
    [SerializeField] private float minIntensity = 0.7f;
    [SerializeField] private float maxIntensity = 1.2f;

    [Header("Speed / Timing")]
    [Tooltip("Speed multiplier for Perlin or pulse frequency in seconds")]
    [SerializeField] private float speed = 5.0f;

    [Header("Spooky / Broken Light Settings")]
    [Tooltip("Chance per frame to drop sharply to low intensity (Perlin mode)")]
    [SerializeField] private bool enableSpookyDips = false;
    [Range(0f, 0.1f)]
    [SerializeField] private float dipChance = 0.02f;

    [Tooltip("Min and Max steady durations for Broken Stutter mode")]
    [SerializeField] private float minSteadyDuration = 0.5f;
    [SerializeField] private float maxSteadyDuration = 2.5f;

    private Light2D targetLight;
    private float timer;
    private bool isStutterGlitching;
    private float noiseSeed;

    private void Awake()
    {
        targetLight = GetComponent<Light2D>();
        noiseSeed = Random.Range(0f, 100f);
        ResetStutterTimer();
    }

    private void Update()
    {
        if (targetLight == null) return;

        switch (flickerType)
        {
            case FlickerType.PerlinSmooth:
                UpdatePerlinFlicker();
                break;

            case FlickerType.RandomPulse:
                UpdateRandomPulse();
                break;

            case FlickerType.BrokenStutter:
                UpdateBrokenStutter();
                break;
        }
    }

    private void UpdatePerlinFlicker()
    {
        float noise = Mathf.PerlinNoise(noiseSeed, Time.time * speed);
        float target = Mathf.Lerp(minIntensity, maxIntensity, noise);

        if (enableSpookyDips && Random.value < dipChance)
        {
            target = minIntensity * 0.3f;
        }

        targetLight.intensity = Mathf.Lerp(targetLight.intensity, target, Time.deltaTime * 12f);
    }

    private void UpdateRandomPulse()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            targetLight.intensity = Random.Range(minIntensity, maxIntensity);
            timer = speed;
        }
    }

    private void UpdateBrokenStutter()
    {
        timer -= Time.deltaTime;

        if (timer <= 0f)
        {
            isStutterGlitching = !isStutterGlitching;
            ResetStutterTimer();
        }

        if (isStutterGlitching)
        {
            targetLight.intensity = (Random.value > 0.5f) ? maxIntensity : 0f;
        }
        else
        {
            targetLight.intensity = Random.Range(maxIntensity * 0.85f, maxIntensity);
        }
    }

    private void ResetStutterTimer()
    {
        timer = isStutterGlitching
            ? Random.Range(0.08f, 0.3f)
            : Random.Range(minSteadyDuration, maxSteadyDuration);
    }
}
