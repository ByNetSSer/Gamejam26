using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Light))]
public sealed class FlameLightFlicker : MonoBehaviour
{
    [Header("Intensidad")]
    [SerializeField, Min(0f), Tooltip("Intensidad mínima de la luz durante el parpadeo.")]
    float minimumIntensity = 0.8f;
    [SerializeField, Min(0f), Tooltip("Intensidad máxima de la luz durante el parpadeo.")]
    float maximumIntensity = 1.2f;
    [SerializeField, Min(0f), Tooltip("Velocidad del ruido de intensidad. Cero mantiene el valor inicial del ruido.")]
    float intensitySpeed = 4f;

    [Header("Color")]
    [SerializeField, Tooltip("Activa la variación de color usando el gradiente.")]
    bool animateColor;
    [SerializeField, Tooltip("Colores entre los que oscila la llama.")]
    Gradient colorGradient = new Gradient();
    [SerializeField, Min(0f), Tooltip("Velocidad del ruido de color.")]
    float colorSpeed = 1f;

    [Header("Variación entre luces")]
    [SerializeField, Tooltip("Cada activación comienza en un punto diferente del ruido.")]
    bool randomizePhase = true;
    [SerializeField, Min(0f), Tooltip("Multiplicador mínimo de las velocidades, elegido al activar la luz.")]
    float minimumSpeedMultiplier = 1f;
    [SerializeField, Min(0f), Tooltip("Multiplicador máximo de las velocidades, elegido al activar la luz.")]
    float maximumSpeedMultiplier = 1f;

    Light targetLight;
    float originalIntensity;
    Color originalColor;
    float intensityPosition;
    float colorPosition;
    float speedMultiplier;

    void OnEnable()
    {
        targetLight = GetComponent<Light>();
        originalIntensity = targetLight.intensity;
        originalColor = targetLight.color;
        intensityPosition = randomizePhase ? Random.value * 1000f : 0f;
        colorPosition = randomizePhase ? Random.value * 1000f : 100f;
        speedMultiplier = Random.Range(minimumSpeedMultiplier, maximumSpeedMultiplier);
        ApplyFlicker();
    }

    void Update()
    {
        intensityPosition += Time.deltaTime * intensitySpeed * speedMultiplier;
        colorPosition += Time.deltaTime * colorSpeed * speedMultiplier;
        ApplyFlicker();
    }

    void ApplyFlicker()
    {
        float intensityNoise = Mathf.Clamp01(Mathf.PerlinNoise(intensityPosition, 0f));
        targetLight.intensity = Mathf.Lerp(minimumIntensity, maximumIntensity, intensityNoise);
        if (animateColor)
        {
            float colorNoise = Mathf.Clamp01(Mathf.PerlinNoise(colorPosition, 0f));
            targetLight.color = colorGradient.Evaluate(colorNoise);
        }
        else
        {
            targetLight.color = originalColor;
        }
    }

    void OnDisable()
    {
        if (targetLight == null) return;
        targetLight.intensity = originalIntensity;
        targetLight.color = originalColor;
    }

    void OnValidate()
    {
        minimumIntensity = Mathf.Max(0f, minimumIntensity);
        maximumIntensity = Mathf.Max(minimumIntensity, maximumIntensity);
        intensitySpeed = Mathf.Max(0f, intensitySpeed);
        colorSpeed = Mathf.Max(0f, colorSpeed);
        minimumSpeedMultiplier = Mathf.Max(0f, minimumSpeedMultiplier);
        maximumSpeedMultiplier = Mathf.Max(minimumSpeedMultiplier, maximumSpeedMultiplier);
    }
}
