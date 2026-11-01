using AssetKits.ParticleImage;
using UnityEngine;
using UnityEngine.UI;

// Editable references belonging to the combo panel prefab.
public sealed class ComboHudView : MonoBehaviour
{
    public RectTransform rankVisual, shakeTarget;
    public Text rankLabel, rankTitle, statsLabel, log, timerLabel;
    public Image timer, accent, background, maskingColor;
    public CanvasGroup group;
    public ParticleImage rankParticles, maskingParticles;
    [Tooltip("Partículas por segundo del fondo para D, C, B, A, S y SS.")]
    public float[] maskingRates = { 8f, 13f, 20f, 30f, 45f, 65f };
}
