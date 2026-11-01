using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MenuUIBase.UI
{
    // Connects prefab joints in Canvas space and carries the existing menu buttons.
    public sealed class HangingMenuChains : MonoBehaviour
    {
        [SerializeField] private RectTransform leftLinkPrefab;
        [SerializeField] private RectTransform rightLinkPrefab;
        [SerializeField] private RectTransform[] buttons;
        [SerializeField] private float chainSpacing = 400f;
        [SerializeField, Min(0f)] private float verticalMargin = 0f;
        [SerializeField, Min(0.1f)] private float linkWidthScale = 1f;
        [Tooltip("Diferencia de altura entre los puntos que sujetan el último botón; positivo baja el lado derecho.")]
        [SerializeField] private float lastButtonDrop = 48f;
        [SerializeField] private float windStrength = 18f;
        [SerializeField] private float stiffness = 4f;
        [SerializeField] private float damping = 2.5f;
        [SerializeField] private float coupling = 5f;
        [SerializeField] private float maxAngle = 12f;

        private RectTransform panel;
        private RectTransform[,] links;
        private RectTransform[,] starts;
        private RectTransform[,] ends;
        private float[] angles, velocities;
        private float[,] originalLengths;
        private List<RectTransform>[,] chainPieces;
        private float elapsed;

        [Header("Pantallas editables")]
        [SerializeField] RectTransform difficultyScreen;
        [SerializeField] RectTransform optionsScreen;
        [SerializeField] RectTransform creditsScreen;
        private RectTransform currentScreen;
        private Vector2 menuPosition;
        private bool transitioning;

        public void ShowDifficulties() => ShowScreen(difficultyScreen);
        public void ShowOptions() => ShowScreen(optionsScreen);
        public void ShowCredits() => ShowScreen(creditsScreen);
        public void ReturnToMenu()
        {
            if (!transitioning && currentScreen != null) StartCoroutine(ChangeScreen(currentScreen, false));
        }
        private void ShowScreen(RectTransform screen)
        {
            if (transitioning || currentScreen != null) return;
            if (screen == null) { Debug.LogError("Falta asignar la pantalla del menú.", this); return; }
            StartCoroutine(ChangeScreen(screen, true));
        }
        private IEnumerator ChangeScreen(RectTransform screen, bool opening)
        {
            transitioning = true;
            var menuButtons = GetComponentsInChildren<Selectable>(true);
            var screenButtons = screen.GetComponentsInChildren<Selectable>(true);
            foreach (var control in menuButtons) control.interactable = false;
            foreach (var control in screenButtons) control.interactable = false;
            float travel = Mathf.Max(((RectTransform)panel.parent).rect.height, 1080f) + 500f;
            Vector2 screenPosition = screen.anchoredPosition;
            if (opening)
            {
                menuPosition = panel.anchoredPosition;
                screen.gameObject.SetActive(true);
                screen.anchoredPosition = screenPosition + Vector2.up * travel;
                yield return Move(panel, menuPosition, menuPosition + Vector2.up * travel, 0.55f);
                yield return Move(screen, screenPosition + Vector2.up * travel, screenPosition, 0.7f);
                currentScreen = screen;
                foreach (var control in screenButtons) control.interactable = true;
            }
            else
            {
                yield return Move(screen, screenPosition, screenPosition + Vector2.up * travel, 0.55f);
                screen.anchoredPosition = screenPosition;
                screen.gameObject.SetActive(false);
                yield return Move(panel, menuPosition + Vector2.up * travel, menuPosition, 0.7f);
                currentScreen = null;
                foreach (var control in menuButtons) control.interactable = true;
            }
            transitioning = false;
        }
        private IEnumerator Move(RectTransform target, Vector2 from, Vector2 to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                float progress = k * k * (3f - 2f * k);
                target.anchoredPosition = Vector2.LerpUnclamped(from, to, progress);
                yield return null;
            }
            target.anchoredPosition = to;
        }

        private void Awake() => InitializeChains();

        private void InitializeChains()
        {
            panel = (RectTransform)transform;
            if (leftLinkPrefab == null || rightLinkPrefab == null || buttons == null || buttons.Length == 0)
            {
                Debug.LogError("HangingMenuChains necesita ambos prefabs y los botones del menú.", this);
                enabled = false;
                return;
            }
            int count = buttons.Length;
            links = new RectTransform[2, count];
            starts = new RectTransform[2, count];
            ends = new RectTransform[2, count];
            originalLengths = new float[2, count];
            chainPieces = new List<RectTransform>[2, count];
            angles = new float[count];
            velocities = new float[count];
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < count; i++)
                {
                    string linkName = (side == 0 ? "Cadena izquierda " : "Cadena derecha ") + (i + 1);
                    RectTransform link = panel.Find(linkName) as RectTransform;
                    if (link == null)
                    {
                        link = Instantiate(side == 0 ? leftLinkPrefab : rightLinkPrefab, panel);
                        link.name = linkName;
                    }
                    link.localScale = Vector3.one;
                    link.SetAsFirstSibling();
                    link.anchorMin = link.anchorMax = new Vector2(0.5f, 0.5f);
                    link.localRotation = Quaternion.identity;
                    links[side, i] = link;
                    starts[side, i] = link.Find("Joint1") as RectTransform;
                    ends[side, i] = link.Find("Joint2") as RectTransform;
                    if (starts[side, i] == null || ends[side, i] == null)
                    {
                        Debug.LogError("El prefab de cadena necesita Joint1 y Joint2.", link);
                        enabled = false;
                        return;
                    }
                    float length = Vector3.Distance(starts[side, i].position, ends[side, i].position) / panel.lossyScale.y;
                    if (length < 0.001f) { enabled = false; Debug.LogError("Los marcadores de cadena coinciden.", link); return; }
                    originalLengths[side, i] = length;
                    // The link remains the joint carrier; visible pieces keep the prefab's proportions.
                    var carrierGraphic = link.GetComponent<Graphic>();
                    if (carrierGraphic != null) carrierGraphic.enabled = false;
                    chainPieces[side, i] = new List<RectTransform>();
                    foreach (Transform child in link)
                        if (child.name.StartsWith("Tramo visual ") && child is RectTransform piece)
                            chainPieces[side, i].Add(piece);
                    foreach (Graphic graphic in link.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                }
            LayoutChains();
        }

        private void Update()
        {
            // Unity cannot serialize multidimensional arrays during a script reload.
            // Reconnect the existing UI objects instead of instantiating duplicates.
            if (links == null || starts == null || ends == null || originalLengths == null || chainPieces == null ||
                angles == null || velocities == null || buttons == null ||
                links.GetLength(1) != buttons.Length || angles.Length != buttons.Length)
            {
                InitializeChains();
                if (!enabled) return;
            }
            float remaining = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            while (remaining > 0f)
            {
                float dt = Mathf.Min(remaining, 1f / 120f);
                elapsed += dt;
                float wind = windStrength * (Mathf.Sin(elapsed * 0.72f) + 0.35f * Mathf.Sin(elapsed * 1.43f));
                for (int i = 0; i < angles.Length; i++)
                {
                    float parentAngle = i == 0 ? 0f : angles[i - 1];
                    velocities[i] += (wind - stiffness * angles[i] - damping * velocities[i] + coupling * (parentAngle - angles[i])) * dt;
                    angles[i] += velocities[i] * dt;
                    if (Mathf.Abs(angles[i]) > maxAngle) { angles[i] = Mathf.Clamp(angles[i], -maxAngle, maxAngle); velocities[i] = 0f; }
                }
                remaining -= dt;
            }
            LayoutChains();
        }

        private void LayoutPieces(int side, int index, float length)
        {
            var carrier = links[side, index];
            var pieces = chainPieces[side, index];
            int required = Mathf.Max(1, Mathf.CeilToInt(length / originalLengths[side, index]));
            while (pieces.Count < required)
            {
                var piece = Instantiate(side == 0 ? leftLinkPrefab : rightLinkPrefab, carrier);
                piece.name = "Tramo visual " + (pieces.Count + 1);
                piece.anchorMin = piece.anchorMax = new Vector2(0.5f, 0.5f);
                piece.localRotation = Quaternion.identity;
                foreach (Graphic graphic in piece.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
                pieces.Add(piece);
            }
            Vector3 start = starts[side, index].localPosition;
            Vector3 end = ends[side, index].localPosition;
            for (int n = 0; n < pieces.Count; n++)
            {
                var piece = pieces[n];
                piece.gameObject.SetActive(n < required);
                if (n >= required) continue;
                piece.localPosition = Vector3.Lerp(start, end, (n + 0.5f) / required);
                // Cancel the carrier's vertical stretching and divide it evenly among pieces.
                piece.localScale = new Vector3(1f, 1f / required, 1f);
            }
        }

        private void LayoutChains()
        {
            float height = Mathf.Max(1f, panel.rect.height - verticalMargin * 2f);
            float linkLength = height / buttons.Length;
            float topOffset = panel.rect.yMax - verticalMargin;
            Vector2[] top = { new Vector2(-chainSpacing * 0.5f, topOffset), new Vector2(chainSpacing * 0.5f, topOffset) };
            Vector2[,] middle = new Vector2[2, buttons.Length];
            for (int side = 0; side < 2; side++)
                for (int i = 0; i < buttons.Length; i++)
                {
                    RectTransform link = links[side, i];
                    // The last pair has unequal lengths, so its attachment points tilt the button.
                    float drop = i == buttons.Length - 1 ? Mathf.Clamp(lastButtonDrop, -linkLength * 0.8f, linkLength * 0.8f) : 0f;
                    float sideLength = linkLength + (side == 0 ? -drop : drop);
                    link.localScale = new Vector3(linkWidthScale, sideLength / originalLengths[side, i], 1f);
                    link.localRotation = Quaternion.Euler(0f, 0f, angles[i]);
                    LayoutPieces(side, i, sideLength);
                    Vector2 currentStart = panel.InverseTransformPoint(starts[side, i].position);
                    link.anchoredPosition += top[side] - currentStart;
                    Vector2 end = panel.InverseTransformPoint(ends[side, i].position);
                    middle[side, i] = (top[side] + end) * 0.5f;
                    top[side] = end;
                }
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                buttons[i].anchoredPosition = (middle[0, i] + middle[1, i]) * 0.5f;
                Vector2 direction = middle[1, i] - middle[0, i];
                buttons[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
        }
    }
}
