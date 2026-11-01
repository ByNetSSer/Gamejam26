using UnityEngine;
using UnityEngine.UI;

// Presentation-only effect for legacy uGUI Text. Digits move independently; changed
// digits exchange places like a split-flap counter. Letter shaking is optional.
[RequireComponent(typeof(Text))]
public sealed class CharacterCounterFeedback : BaseMeshEffect
{
    [SerializeField, Min(0f)] float shake = 0.45f;
    [SerializeField, Min(0.01f)] float flipDuration = 0.22f;
    [SerializeField, Min(0f)] float frequency = 19f;
    [Tooltip("Mantiene visibles los dígitos cuando cambian varias veces por segundo.")]
    [SerializeField] bool continuousRoll;
    [Tooltip("Aplica el temblor también a las letras y símbolos visibles.")]
    [SerializeField] bool shakeLetters;
    Text label, previousLabel;
    CharacterCounterFeedback previousEffect, owner;
    string previous = "", current = "";
    float elapsed;
    float intensity = 1f;

    public float Intensity { get => intensity; set => intensity = Mathf.Max(0f, value); }

    protected override void Awake()
    {
        base.Awake();
        label = GetComponent<Text>();
        current = label.text;
    }

    protected override void OnDisable()
    {
        if (owner == null && previousLabel != null) previousLabel.enabled = false;
        base.OnDisable();
    }

    void LateUpdate()
    {
        if (owner != null || label == null) return;
        if (current != label.text)
        {
            previous = current;
            current = label.text;
            elapsed = 0f;
            if (!continuousRoll)
            {
                if (previousLabel == null) CreatePreviousLabel();
                previousLabel.text = previous;
            }
        }
        else elapsed += Time.deltaTime;
        if (previousLabel != null)
        {
            previousLabel.color = label.color;
            previousLabel.enabled = elapsed < flipDuration && !string.IsNullOrEmpty(previous);
            if (previousLabel.enabled) previousLabel.SetVerticesDirty();
        }
        label.SetVerticesDirty();
    }

    void CreatePreviousLabel()
    {
        var go = new GameObject("Dígitos anteriores", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(transform, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        previousLabel = go.GetComponent<Text>();
        previousLabel.font = label.font; previousLabel.fontSize = label.fontSize;
        previousLabel.fontStyle = label.fontStyle; previousLabel.alignment = label.alignment;
        previousLabel.horizontalOverflow = label.horizontalOverflow; previousLabel.verticalOverflow = label.verticalOverflow;
        previousLabel.lineSpacing = label.lineSpacing; previousLabel.supportRichText = false;
        previousLabel.raycastTarget = false;
        previousEffect = go.AddComponent<CharacterCounterFeedback>();
        previousEffect.owner = this;
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive()) return;
        var source = owner != null ? owner : this;
        string value = owner != null ? source.previous : source.current;
        float progress = Mathf.Clamp01(source.elapsed / Mathf.Max(0.01f, source.flipDuration));
        int quad = 0;
        for (int i = 0; i < value.Length && quad * 4 + 3 < vertices.currentVertCount; i++)
        {
            char c = value[i];
            if (c == '\n' || c == '\r') continue;
            bool digit = c >= '0' && c <= '9';
            bool changed = digit && (i >= (owner != null ? source.current.Length : source.previous.Length) ||
                c != (owner != null ? source.current[i] : source.previous[i]));
            int start = quad++ * 4;
            UIVertex a = default, b = default;
            vertices.PopulateUIVertex(ref a, start); vertices.PopulateUIVertex(ref b, start + 2);
            float centerY = (a.position.y + b.position.y) * 0.5f;
            float height = Mathf.Abs(a.position.y - b.position.y);
            float scale = 1f, offset = 0f;
            if (source.continuousRoll && changed && progress < 1f)
            {
                // Fast counters cannot wait for a complete old/new split every frame.
                float rollPhase = Time.time * source.frequency * 2f + i * 0.7f;
                scale = 0.8f + 0.2f * Mathf.Abs(Mathf.Cos(rollPhase));
                offset = height * 0.08f * Mathf.Sin(rollPhase);
            }
            else if (changed && progress < 1f)
            {
                // The old number is pressed down as the new one drops in and unfolds.
                scale = owner != null ? Mathf.Max(0.01f, 1f - progress * 2f) : Mathf.Max(0.01f, progress * 2f - 1f);
                offset = owner != null ? -height * 0.25f * progress : height * 0.25f * (1f - progress);
            }
            float phase = Time.time * source.frequency + i * 2.37f;
            bool shakeCharacter = digit || (source.shakeLetters && !char.IsWhiteSpace(c));
            Vector2 jitter = shakeCharacter ? new Vector2(Mathf.Sin(phase * 1.13f), Mathf.Cos(phase)) * source.shake * source.intensity : Vector2.zero;
            for (int j = 0; j < 4; j++)
            {
                UIVertex v = default;
                vertices.PopulateUIVertex(ref v, start + j);
                v.position.x += jitter.x;
                v.position.y = centerY + (v.position.y - centerY) * scale + offset + jitter.y;
                if (owner != null && (!changed || progress >= 0.5f)) { var color = v.color; color.a = 0; v.color = color; }
                if (!source.continuousRoll && owner == null && changed && progress < 0.5f) { var color = v.color; color.a = 0; v.color = color; }
                vertices.SetUIVertex(v, start + j);
            }
        }
    }
}
