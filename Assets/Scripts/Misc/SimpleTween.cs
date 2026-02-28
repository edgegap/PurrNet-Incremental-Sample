using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace ProgressiveSample
{
    public static class SimpleTween
    {
        private struct TweenEntry<TValue>
        {
            public Coroutine Coroutine;
            public TValue EndValue;
        }

        private static readonly Dictionary<Transform, List<TweenEntry<Vector3>>> _transformTweens = new();
        private static readonly Dictionary<Material, List<TweenEntry<(Color color, int propertyID)>>> _materialTweens = new();
        private static SimpleTweenRunner _runner;

        private static SimpleTweenRunner Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("[SimpleTweenRunner]");
                    Object.DontDestroyOnLoad(go);
                    _runner = go.AddComponent<SimpleTweenRunner>();
                }
                return _runner;
            }
        }

        public static void TweenKill(this Transform t)
        {
            if (_transformTweens.TryGetValue(t, out var list))
            {
                foreach (var e in list)
                    if (e.Coroutine != null)
                        Runner.StopCoroutine(e.Coroutine);
                list.Clear();
            }
        }

        public static void TweenComplete(this Transform t)
        {
            if (_transformTweens.TryGetValue(t, out var list))
            {
                foreach (var e in list)
                {
                    if (e.Coroutine != null)
                        Runner.StopCoroutine(e.Coroutine);
                    if (t) t.localScale = e.EndValue;
                }
                list.Clear();
            }
        }

        public static void TweenScale(this Transform t, Vector3 target, float duration)
        {
            t.TweenKill();
            var c = Runner.StartCoroutine(ScaleRoutine(t, target, duration));
            Register(_transformTweens, t, new TweenEntry<Vector3> { Coroutine = c, EndValue = target });
        }

        public static void TweenPunchScale(this Transform t, Vector3 punch, float duration)
        {
            t.TweenKill();
            Vector3 origin = t.localScale;
            var c = Runner.StartCoroutine(PunchScaleRoutine(t, punch, duration));
            Register(_transformTweens, t, new TweenEntry<Vector3> { Coroutine = c, EndValue = origin });
        }

        public static void TweenKill(this Material m)
        {
            if (_materialTweens.TryGetValue(m, out var list))
            {
                foreach (var e in list)
                    if (e.Coroutine != null)
                        Runner.StopCoroutine(e.Coroutine);
                list.Clear();
            }
        }

        public static void TweenComplete(this Material m)
        {
            if (_materialTweens.TryGetValue(m, out var list))
            {
                foreach (var e in list)
                {
                    if (e.Coroutine != null)
                        Runner.StopCoroutine(e.Coroutine);
                    if (m) m.SetColor(e.EndValue.propertyID, e.EndValue.color);
                }
                list.Clear();
            }
        }

        public static void TweenColor(this Material m, Color target, int propertyID, float duration)
        {
            m.TweenKill();
            var c = Runner.StartCoroutine(ColorRoutine(m, target, propertyID, duration));
            Register(_materialTweens, m, new TweenEntry<(Color, int)> { Coroutine = c, EndValue = (target, propertyID) });
        }

        private static void Register<T, TValue>(Dictionary<T, List<TweenEntry<TValue>>> dict, T key, TweenEntry<TValue> entry)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<TweenEntry<TValue>>();
                dict[key] = list;
            }
            list.Add(entry);
        }

        private static IEnumerator ScaleRoutine(Transform t, Vector3 target, float duration)
        {
            Vector3 start = t.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!t) yield break;
                elapsed += Time.deltaTime;
                t.localScale = Vector3.LerpUnclamped(start, target, Ease(elapsed / duration));
                yield return null;
            }
            if (t) t.localScale = target;
            Cleanup(_transformTweens, t);
        }

        private static IEnumerator PunchScaleRoutine(Transform t, Vector3 punch, float duration)
        {
            Vector3 origin = t.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!t) yield break;
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;
                float envelope = Mathf.Sin(progress * Mathf.PI);
                t.localScale = origin + punch * envelope;
                yield return null;
            }
            if (t) t.localScale = origin;
            Cleanup(_transformTweens, t);
        }

        private static IEnumerator ColorRoutine(Material m, Color target, int propertyID, float duration)
        {
            Color start = m.GetColor(propertyID);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!m) yield break;
                elapsed += Time.deltaTime;
                m.SetColor(propertyID, Color.LerpUnclamped(start, target, Ease(elapsed / duration)));
                yield return null;
            }
            if (m) m.SetColor(propertyID, target);
            Cleanup(_materialTweens, m);
        }

        private static void Cleanup<T, TValue>(Dictionary<T, List<TweenEntry<TValue>>> dict, T key)
        {
            if (key != null && dict.ContainsKey(key))
                dict[key].Clear();
        }

        private static float Ease(float t) => t * t * (3f - 2f * t);
    }

    public class SimpleTweenRunner : MonoBehaviour { }
}