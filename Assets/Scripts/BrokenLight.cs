using UnityEngine;
using UnityEngine.Rendering.Universal; // Required for 2D Lights

public class BrokenLight2D : MonoBehaviour
{
    private Light2D targetLight;

    [Header("Brightness Settings")]
    public float maxIntensity = 1.5f;

    [Header("Stutter Timing")]
    public float minSteadyTime = 0.5f;
    public float maxSteadyTime = 2.5f;

    private float timer;
    private bool isGlitching;

    void Start()
    {
        targetLight = GetComponent<Light2D>();
        ResetTimer();
    }

    void Update()
    {
        if (targetLight == null) return;

        timer -= Time.deltaTime;

        if (timer <= 0)
        {
            isGlitching = !isGlitching;
            ResetTimer();
        }

        if (isGlitching)
        {
            // Rapidly toggle light completely off and on
            targetLight.intensity = (Random.value > 0.5f) ? maxIntensity : 0f;
        }
        else
        {
            // Subtle flickering voltage hum while "ON"
            targetLight.intensity = Random.Range(maxIntensity * 0.85f, maxIntensity);
        }
    }

    void ResetTimer()
    {
        timer = isGlitching ? Random.Range(0.08f, 0.3f) : Random.Range(minSteadyTime, maxSteadyTime);
    }
}