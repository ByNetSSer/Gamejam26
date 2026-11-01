using System.Collections.Generic;
using AssetKits.ParticleImage;
using UnityEngine;
using UnityEngine.UI;

// Scene-authored presentation; PlayerScore remains the authoritative balance.
public sealed class CoinWalletHud : MonoBehaviour
{
    [SerializeField] PlayerScore score;
    [SerializeField] Image bag;
    [SerializeField] Sprite emptyBag, fullBag;
    [SerializeField] Text amountLabel;
    [SerializeField] RectTransform numberVisual;
    [SerializeField] ParticleImage coins;
    [SerializeField] RectTransform particleArea;
    [SerializeField, Min(1)] long fullBagThreshold = 750;
    [SerializeField, Min(1)] int approximateParticles = 20;
    [Tooltip("Duración para pagos pequeños y para un pago de referencia de 1000 monedas.")]
    [SerializeField, Min(0.05f)] float smallPayoutDuration = 0.65f;
    [SerializeField, Min(0.05f)] float largePayoutDuration = 3f;
    [SerializeField, Min(1)] long largePayoutReference = 1000;
    [SerializeField, Min(0f)] float growthPerSecond = 0.055f;
    [SerializeField, Min(1f)] float maximumScale = 1.6f;
    readonly Queue<long> bursts = new Queue<long>();
    long displayed, target, lastActual, unannouncedGain, burstRemaining, burstAmount, countStartValue;
    float countingTime, countStartTime, countDuration;
    bool animatingBurst, counting;
    Vector3 originalScale;

    void Awake()
    {
        if (numberVisual != null) originalScale = numberVisual.localScale;
        if (score != null) displayed = target = lastActual = score.Coins;
        if (coins != null) { coins.attractorTarget = bag != null ? bag.rectTransform : null; coins.attractorEnabled = true; }
        Refresh();
    }
    void OnEnable()
    {
        if (score != null) { score.CoinsChanged += OnCoinsChanged; score.ComboPaid += OnComboPaid; }
        if (coins != null) { coins.onAnyParticleFinished.AddListener(OnCoinArrived); coins.onParticleStop.AddListener(OnBurstStopped); }
    }
    void OnDisable()
    {
        if (score != null) { score.CoinsChanged -= OnCoinsChanged; score.ComboPaid -= OnComboPaid; }
        if (coins != null) { coins.onAnyParticleFinished.RemoveListener(OnCoinArrived); coins.onParticleStop.RemoveListener(OnBurstStopped); }
        bursts.Clear(); animatingBurst = false; counting = false; unannouncedGain = 0;
        if (score != null) displayed = target = lastActual = score.Coins;
        countingTime = 0f;
        Refresh();
    }
    void OnCoinsChanged(long actual)
    {
        if (actual > lastActual) unannouncedGain = actual - lastActual;
        else
        {
            unannouncedGain = 0;
            displayed = System.Math.Min(displayed, actual);
            target = System.Math.Min(target, actual);
            if (counting) { countStartValue = displayed; countStartTime = Time.time; }
        }
        lastActual = actual;
        Refresh();
    }
    void OnComboPaid(long points, long payout)
    {
        long gain = unannouncedGain;
        unannouncedGain = 0;
        if (gain <= 0) return;
        if (coins == null || !coins.isActiveAndEnabled) { target = score.Coins; BeginCounting(gain); return; }
        bursts.Enqueue(gain);
        if (!animatingBurst) StartBurst();
    }
    void StartBurst()
    {
        if (bursts.Count == 0 || coins == null) return;
        burstRemaining = bursts.Dequeue();
        burstAmount = burstRemaining;
        animatingBurst = true;
        var combo = score != null ? score.GetComponent<ComboHud>() : null;
        if (combo != null && combo.PayoutOrigin != null && particleArea != null)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, combo.PayoutOrigin.position);
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(particleArea, screenPoint, null, out Vector3 world))
                coins.transform.position = world;
        }
        coins.Play();
    }
    void OnCoinArrived()
    {
        if (!animatingBurst || burstRemaining <= 0 || score == null) return;
        long chunk = System.Math.Max(1L, burstRemaining / System.Math.Max(1, approximateParticles));
        burstRemaining -= chunk;
        target = target > long.MaxValue - chunk ? long.MaxValue : target + chunk;
        target = System.Math.Min(target, score.Coins);
        BeginCounting(burstAmount);
    }
    void OnBurstStopped()
    {
        if (!animatingBurst) return;
        animatingBurst = false;
        if (score != null)
        {
            target = target > long.MaxValue - burstRemaining ? long.MaxValue : target + burstRemaining;
            target = System.Math.Min(target, score.Coins);
        }
        burstRemaining = 0;
        if (bursts.Count > 0) StartBurst();
        else if (score != null) target = score.Coins;
        BeginCounting(burstAmount);
    }
    void BeginCounting(long amount)
    {
        if (counting || target <= displayed) return;
        countStartValue = displayed;
        countStartTime = Time.time;
        float size = Mathf.Clamp01((float)((double)amount / System.Math.Max(1L, largePayoutReference)));
        countDuration = Mathf.Max(0.05f, Mathf.Lerp(smallPayoutDuration, largePayoutDuration, size));
        counting = true;
    }
    void Update()
    {
        if (score == null) return;
        if (counting)
        {
            countingTime += Time.deltaTime;
            float progress = Mathf.Clamp01((Time.time - countStartTime) / countDuration);
            // Large rewards must finish promptly; displays necessarily skip values between frames.
            double value = countStartValue + ((double)target - countStartValue) * progress;
            long next = progress >= 1f || value >= long.MaxValue ? target : (long)System.Math.Floor(value);
            next = System.Math.Max(displayed, System.Math.Min(next, target));
            if (next != displayed) { displayed = next; Refresh(); }
            if (progress >= 1f && displayed >= target && !animatingBurst && bursts.Count == 0) counting = false;
        }
        else if (!animatingBurst && bursts.Count == 0) countingTime = 0f;
        if (numberVisual != null)
        {
            float scale = Mathf.Min(maximumScale, 1f + countingTime * growthPerSecond);
            numberVisual.localScale = Vector3.Lerp(numberVisual.localScale, originalScale * scale, 10f * Time.deltaTime);
        }
    }
    void Refresh()
    {
        if (amountLabel != null) amountLabel.text = displayed.ToString();
        if (bag != null) bag.sprite = displayed >= fullBagThreshold ? fullBag : emptyBag;
    }
}
