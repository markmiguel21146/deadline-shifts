using UnityEngine;
using UnityEngine.Rendering.Universal;

public class CRTLightFlicker : MonoBehaviour
{
    private Light2D monitorLight;

    [Header("Flicker Settings")]
    public float minIntensity = 0.2f;
    public float maxIntensity = 0.4f;
    [Range(0.01f, 0.2f)]
    public float flickerSpeed = 0.05f;

    private float timer;

    void Start()
    {
        monitorLight = GetComponent<Light2D>();
    }

    void Update()
    {
        if (monitorLight == null) return;

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            // Randomize light intensity within range
            monitorLight.intensity = Random.Range(minIntensity, maxIntensity);
            timer = flickerSpeed;
        }
    }
}