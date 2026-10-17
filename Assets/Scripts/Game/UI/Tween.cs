using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace PiratesDice.Game.UI
{
    /// <summary>Small coroutine tweens for UI juice: pops, punches, shakes, fades and flights.</summary>
    public static class Tween
    {
        sealed class Runner : MonoBehaviour { }

        static Runner _runner;

        static Runner Host
        {
            get
            {
                if (_runner) return _runner;
                var go = new GameObject("Tweens");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
                return _runner;
            }
        }

        public static Coroutine Run(IEnumerator routine) => Host.StartCoroutine(routine);

        public static void Stop(Coroutine routine)
        {
            if (routine != null && _runner) _runner.StopCoroutine(routine);
        }

        public static Coroutine Delay(float seconds, Action then) => Run(DelayRoutine(seconds, then));

        static IEnumerator DelayRoutine(float seconds, Action then)
        {
            yield return new WaitForSeconds(seconds);
            then?.Invoke();
        }

        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }

        public static float EaseOutCubic(float t) => 1 - Mathf.Pow(1 - t, 3);

        /// <summary>Scales from zero with a little overshoot.</summary>
        public static Coroutine PopIn(Transform t, float duration = 0.3f, float delay = 0f) => Run(PopRoutine(t, duration, delay));

        static IEnumerator PopRoutine(Transform t, float duration, float delay)
        {
            if (!t) yield break;
            t.localScale = Vector3.zero;
            if (delay > 0) yield return new WaitForSeconds(delay);
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                if (!t) yield break;
                t.localScale = Vector3.one * EaseOutBack(e / duration);
                yield return null;
            }
            if (t) t.localScale = Vector3.one;
        }

        /// <summary>A quick swell and settle, for something that just changed.</summary>
        public static Coroutine Punch(Transform t, float amount = 0.12f, float duration = 0.25f) => Run(PunchRoutine(t, amount, duration));

        static IEnumerator PunchRoutine(Transform t, float amount, float duration)
        {
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                if (!t) yield break;
                float k = Mathf.Sin(e / duration * Mathf.PI) * (1 - e / duration);
                t.localScale = Vector3.one * (1 + amount * k * 2);
                yield return null;
            }
            if (t) t.localScale = Vector3.one;
        }

        /// <summary>Jitters a UI element in place, like a cup being shaken.</summary>
        public static Coroutine Shake(RectTransform t, float magnitude = 10f, float duration = 0.5f) => Run(ShakeRoutine(t, magnitude, duration));

        static IEnumerator ShakeRoutine(RectTransform t, float magnitude, float duration)
        {
            if (!t) yield break;
            Quaternion rest = t.localRotation;
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                if (!t) yield break;
                float fade = 1 - e / duration;
                t.localRotation = rest * Quaternion.Euler(0, 0, Mathf.Sin(e * 55f) * magnitude * fade);
                yield return null;
            }
            if (t) t.localRotation = rest;
        }

        public static Coroutine Fade(CanvasGroup g, float to, float duration = 0.25f, float delay = 0f) => Run(FadeRoutine(g, to, duration, delay));

        static IEnumerator FadeRoutine(CanvasGroup g, float to, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (!g) yield break;
            float from = g.alpha;
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                if (!g) yield break;
                g.alpha = Mathf.Lerp(from, to, e / duration);
                yield return null;
            }
            if (g) g.alpha = to;
        }

        /// <summary>Sends a die tumbling away and fading, for a die lost overboard.</summary>
        public static Coroutine Overboard(Graphic g, float duration = 0.9f, float delay = 0f) => Run(OverboardRoutine(g, duration, delay));

        static IEnumerator OverboardRoutine(Graphic g, float duration, float delay)
        {
            if (delay > 0) yield return new WaitForSeconds(delay);
            if (!g) yield break;

            // Pull the die out of its layout so it can fly freely.
            RectTransform rt = g.rectTransform;
            Vector3 start = rt.position;
            var le = g.GetComponent<LayoutElement>();
            if (le) le.ignoreLayout = true;
            rt.position = start;

            if (!g.TryGetComponent(out CanvasGroup group)) group = g.gameObject.AddComponent<CanvasGroup>();
            float scale = rt.lossyScale.x;
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                if (!g) yield break;
                float k = e / duration;
                rt.position = start + new Vector3(160f * k, (120f * k - 520f * k * k), 0) * scale;
                rt.localRotation = Quaternion.Euler(0, 0, -400f * k);
                group.alpha = 1 - k * k;
                yield return null;
            }
            if (g) g.gameObject.SetActive(false);
        }

        /// <summary>Gold coins rain down over a parent rect, for a win.</summary>
        public static void CoinShower(RectTransform area, int coins = 36)
        {
            for (int i = 0; i < coins; i++) Run(CoinRoutine(area, UnityEngine.Random.Range(0f, 1.4f)));
        }

        static IEnumerator CoinRoutine(RectTransform area, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!area) yield break;
            RectTransform coin = UiKit.Rect("Coin", area);
            var img = coin.gameObject.AddComponent<Image>();
            img.sprite = Art.Coin;
            img.raycastTarget = false;
            float size = UnityEngine.Random.Range(40f, 70f);
            coin.sizeDelta = new Vector2(size, size);
            coin.anchorMin = coin.anchorMax = new Vector2(UnityEngine.Random.value, 1);
            float spin = UnityEngine.Random.Range(-500f, 500f);
            float fall = area.rect.height + 200;
            float duration = UnityEngine.Random.Range(1.4f, 2.2f);
            for (float e = 0; e < duration; e += Time.unscaledDeltaTime)
            {
                if (!coin) yield break;
                float k = e / duration;
                coin.anchoredPosition = new Vector2(Mathf.Sin(k * 9 + delay * 7) * 20, 60 - fall * k * k);
                coin.localRotation = Quaternion.Euler(0, 0, spin * k);
                coin.localScale = new Vector3(Mathf.Abs(Mathf.Cos(k * 14 + delay)), 1, 1);
                yield return null;
            }
            if (coin) UnityEngine.Object.Destroy(coin.gameObject);
        }
    }

    /// <summary>Gently breathes an element's scale while enabled, to show whose turn it is.</summary>
    public sealed class Pulse : MonoBehaviour
    {
        public float Amount = 0.035f;
        public float Speed = 5f;

        void OnDisable() => transform.localScale = Vector3.one;

        void Update() =>
            transform.localScale = Vector3.one * (1 + Amount * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Speed)));
    }
}
