using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Manages atmospheric horror visuals and audio (Vignette, Whisper Audio, CRT Monitor Light).
/// </summary>
public class AtmosphereController : MonoBehaviour
{
    [Header("CRT Monitor Lighting")]
    public Light2D monitorLight;

    [Header("Corruption Atmosphere (Vignette & Audio)")]
    public Image vignetteImage;
    public AudioSource whisperAudioSource;
    [Range(0f, 100f)] public float vignetteStartThreshold = 30f;
    [Range(0f, 100f)] public float whisperStartThreshold = 50f;

    private void Awake()
    {
        if (vignetteImage == null)
        {
            Image[] allImages = FindObjectsByType<Image>(FindObjectsInactive.Include);
            foreach (var img in allImages)
            {
                if (img.gameObject.name.IndexOf("vignette", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    vignetteImage = img;
                    break;
                }
            }
        }

        if (vignetteImage != null)
        {
            vignetteImage.raycastTarget = false;
        }
    }

    private void Start()
    {
        if (whisperAudioSource != null)
        {
            whisperAudioSource.volume = 0f;
            whisperAudioSource.loop = true;
            whisperAudioSource.Play();
        }

        if (vignetteImage != null)
        {
            vignetteImage.raycastTarget = false;
            Color c = vignetteImage.color;
            c.a = 0f;
            vignetteImage.color = c;
        }
    }

    /// <summary>
    /// Smoothly updates vignette opacity, whisper audio volume, and CRT monitor light color based on corruption percentage (0-100).
    /// </summary>
    public void UpdateAtmosphere(float corruption)
    {
        // 1. Dynamic Vignette
        if (vignetteImage != null)
        {
            float targetAlpha = 0f;
            if (corruption >= vignetteStartThreshold)
            {
                float progress = (corruption - vignetteStartThreshold) / (100f - vignetteStartThreshold);
                targetAlpha = Mathf.Lerp(0f, 0.85f, progress);
            }

            Color c = vignetteImage.color;
            if (!Mathf.Approximately(c.a, targetAlpha))
            {
                c.a = targetAlpha;
                vignetteImage.color = c;
            }
        }

        // 2. Whisper Audio
        if (whisperAudioSource != null)
        {
            float targetVolume = 0f;
            if (corruption >= whisperStartThreshold)
            {
                float progress = (corruption - whisperStartThreshold) / (100f - whisperStartThreshold);
                targetVolume = Mathf.Lerp(0f, 0.45f, progress);
            }

            if (!Mathf.Approximately(whisperAudioSource.volume, targetVolume))
            {
                whisperAudioSource.volume = targetVolume;
            }
        }

        // 3. Ambient Monitor Light Color Lerp
        if (monitorLight != null)
        {
            float corruptionPercent = corruption / 100f;
            Color normalColor = new Color(0f, 1f, 0.4f);
            Color corruptedColor = new Color(1f, 0.1f, 0.1f);
            Color targetColor = Color.Lerp(normalColor, corruptedColor, corruptionPercent);

            if (monitorLight.color != targetColor)
            {
                monitorLight.color = targetColor;
            }
        }
    }

    /// <summary>
    /// Resets all atmospheric effects back to pristine state (e.g., after terminal reboot).
    /// </summary>
    public void ResetAtmosphere()
    {
        if (vignetteImage != null)
        {
            Color c = vignetteImage.color;
            c.a = 0f;
            vignetteImage.color = c;
        }

        if (whisperAudioSource != null)
        {
            whisperAudioSource.volume = 0f;
        }

        if (monitorLight != null)
        {
            monitorLight.color = new Color(0f, 1f, 0.4f);
        }
    }

    public IEnumerator FadeInAudio(AudioSource source, float targetVolume, float duration)
    {
        if (source == null) yield break;

        source.volume = 0f;
        source.Play();

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            source.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
            yield return null;
        }

        source.volume = targetVolume;
    }
}
