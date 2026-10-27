using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    public enum Ease
    {
        Linear,
        InQuad,
        OutQuad,
        InOutQuad,
        InCubic,
        OutCubic,
        InOutCubic,
        OutBack,
        OutBackStrong,
        OutElastic,
        OutBounce,
    }

    /// <summary>One running animation. Instances are pooled; do not keep references after completion.</summary>
    public sealed class Tween
    {
        internal enum Kind { Position, Scale, Rotation, GroupAlpha, Colour, Size, Value, Delay }

        internal Kind kind;
        internal UnityEngine.Object target;
        internal RectTransform rect;
        internal Transform transform;
        internal CanvasGroup group;
        internal Graphic graphic;
        internal Vector4 from, to;
        internal float delay, duration, elapsed;
        internal Ease ease;
        internal Action<float> setter;
        internal Action onComplete;
        internal bool killed;
        internal bool started;
        internal bool fromCurrent;
        internal int version;

        /// <summary>Called once when the tween finishes normally (not when killed or its target is destroyed).</summary>
        public Tween OnComplete(Action action)
        {
            onComplete = action;
            return this;
        }

        internal void Reset()
        {
            target = null;
            rect = null;
            transform = null;
            group = null;
            graphic = null;
            setter = null;
            onComplete = null;
            killed = false;
            started = false;
            fromCurrent = false;
            elapsed = 0;
            delay = 0;
            version++;
        }

        // ReferenceEquals distinguishes "no owner" from Unity's "destroyed object == null".
        internal bool TargetAlive => ReferenceEquals(target, null) ? (kind == Kind.Value || kind == Kind.Delay) : (bool)target;

        internal void Capture()
        {
            switch (kind)
            {
                case Kind.Position: from = rect.anchoredPosition; break;
                case Kind.Scale: from = transform.localScale; break;
                case Kind.Rotation: from = new Vector4(transform.localEulerAngles.z, 0, 0, 0); break;
                case Kind.GroupAlpha: from = new Vector4(group.alpha, 0, 0, 0); break;
                case Kind.Colour: from = graphic.color; break;
                case Kind.Size: from = rect.sizeDelta; break;
            }
        }

        internal void Apply(float t)
        {
            float e = Tw.Evaluate(ease, t);
            Vector4 v = from + (to - from) * e;
            switch (kind)
            {
                case Kind.Position: rect.anchoredPosition = new Vector2(v.x, v.y); break;
                case Kind.Scale: transform.localScale = new Vector3(v.x, v.y, v.z); break;
                case Kind.Rotation: transform.localEulerAngles = new Vector3(0, 0, v.x); break;
                case Kind.GroupAlpha: group.alpha = v.x; break;
                case Kind.Colour: graphic.color = new Color(v.x, v.y, v.z, v.w); break;
                case Kind.Size: rect.sizeDelta = new Vector2(v.x, v.y); break;
                case Kind.Value: setter?.Invoke(v.x); break;
            }
        }
    }

    /// <summary>
    /// Tiny tween engine. One runner updates all tweens with unscaled time; tweens are pooled, and starting
    /// a tween of the same kind on the same object replaces the old one, so animations never fight.
    /// </summary>
    public static class Tw
    {
        static TweenRunner runner;
        /// <summary>Reduced motion: overshoot easings become plain easings and durations shorten.</summary>
        public static bool Reduced;

        internal static TweenRunner Runner
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("TweenRunner");
                    UnityEngine.Object.DontDestroyOnLoad(go);
                    runner = go.AddComponent<TweenRunner>();
                }
                return runner;
            }
        }

        public static Tween Move(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            var t = Runner.Create(Tween.Kind.Position, rt, duration, ease, delay);
            t.rect = rt;
            t.to = to;
            return Runner.Add(t);
        }

        public static Tween Scale(Transform tr, float to, float duration, Ease ease = Ease.OutCubic, float delay = 0f)
            => Scale(tr, new Vector3(to, to, 1f), duration, ease, delay);

        public static Tween Scale(Transform tr, Vector3 to, float duration, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            var t = Runner.Create(Tween.Kind.Scale, tr, duration, ease, delay);
            t.transform = tr;
            t.to = to;
            return Runner.Add(t);
        }

        public static Tween Rotate(Transform tr, float z, float duration, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            var t = Runner.Create(Tween.Kind.Rotation, tr, duration, ease, delay);
            t.transform = tr;
            t.to = new Vector4(z, 0, 0, 0);
            return Runner.Add(t);
        }

        public static Tween Fade(CanvasGroup g, float to, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
        {
            var t = Runner.Create(Tween.Kind.GroupAlpha, g, duration, ease, delay);
            t.group = g;
            t.to = new Vector4(to, 0, 0, 0);
            return Runner.Add(t);
        }

        public static Tween Colour(Graphic g, Color to, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
        {
            var t = Runner.Create(Tween.Kind.Colour, g, duration, ease, delay);
            t.graphic = g;
            t.to = to;
            return Runner.Add(t);
        }

        public static Tween Fade(Graphic g, float alpha, float duration, Ease ease = Ease.OutQuad, float delay = 0f)
        {
            var c = g.color;
            return Colour(g, new Color(c.r, c.g, c.b, alpha), duration, ease, delay);
        }

        public static Tween Size(RectTransform rt, Vector2 to, float duration, Ease ease = Ease.OutCubic, float delay = 0f)
        {
            var t = Runner.Create(Tween.Kind.Size, rt, duration, ease, delay);
            t.rect = rt;
            t.to = to;
            return Runner.Add(t);
        }

        /// <summary>Animates a number. owner (optional) stops the tween when destroyed.</summary>
        public static Tween Value(float from, float to, float duration, Action<float> setter, Ease ease = Ease.OutCubic, float delay = 0f, UnityEngine.Object owner = null)
        {
            var t = Runner.Create(Tween.Kind.Value, owner, duration, ease, delay);
            t.setter = setter;
            t.from = new Vector4(from, 0, 0, 0);
            t.to = new Vector4(to, 0, 0, 0);
            t.fromCurrent = false;
            return Runner.Add(t, replace: false);
        }

        public static Tween Delay(float seconds, Action action, UnityEngine.Object owner = null)
        {
            var t = Runner.Create(Tween.Kind.Delay, owner, 0f, Ease.Linear, seconds);
            t.onComplete = action;
            return Runner.Add(t, replace: false);
        }

        /// <summary>Stops every tween on this object without completing it.</summary>
        public static void Kill(UnityEngine.Object target)
        {
            if (runner != null) runner.KillTarget(target);
        }

        /// <summary>Stops all tweens owned by this object or any of its children (used when a screen is rebuilt).</summary>
        public static void KillAll()
        {
            if (runner != null) runner.KillEverything();
        }

        public static float Evaluate(Ease ease, float t)
        {
            if (t <= 0) return 0;
            if (t >= 1) return 1;
            if (Reduced && (ease == Ease.OutBack || ease == Ease.OutBackStrong || ease == Ease.OutElastic || ease == Ease.OutBounce)) ease = Ease.OutCubic;
            switch (ease)
            {
                case Ease.InQuad: return t * t;
                case Ease.OutQuad: return 1 - (1 - t) * (1 - t);
                case Ease.InOutQuad: return t < 0.5f ? 2 * t * t : 1 - Mathf.Pow(-2 * t + 2, 2) / 2;
                case Ease.InCubic: return t * t * t;
                case Ease.OutCubic: return 1 - Mathf.Pow(1 - t, 3);
                case Ease.InOutCubic: return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
                case Ease.OutBack:
                    {
                        const float c1 = 1.4f, c3 = c1 + 1;
                        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
                    }
                case Ease.OutBackStrong:
                    {
                        const float c1 = 2.4f, c3 = c1 + 1;
                        return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
                    }
                case Ease.OutElastic:
                    {
                        const float c4 = 2 * Mathf.PI / 3;
                        return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * c4) + 1;
                    }
                case Ease.OutBounce:
                    {
                        const float n1 = 7.5625f, d1 = 2.75f;
                        if (t < 1 / d1) return n1 * t * t;
                        if (t < 2 / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
                        if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
                        t -= 2.625f / d1;
                        return n1 * t * t + 0.984375f;
                    }
                default: return t;
            }
        }
    }

    public sealed class TweenRunner : MonoBehaviour
    {
        readonly List<Tween> active = new List<Tween>(256);
        readonly Stack<Tween> pool = new Stack<Tween>(256);

        internal Tween Create(Tween.Kind kind, UnityEngine.Object target, float duration, Ease ease, float delay)
        {
            var t = pool.Count > 0 ? pool.Pop() : new Tween();
            t.Reset();
            t.kind = kind;
            t.target = target;
            t.duration = Mathf.Max(0f, Tw.Reduced ? duration * 0.6f : duration);
            t.delay = Mathf.Max(0f, delay);
            t.ease = ease;
            t.fromCurrent = true;
            return t;
        }

        // Not called Start: Unity would treat it as the Start message (which can't take parameters).
        internal Tween Add(Tween t, bool replace = true)
        {
            if (replace && t.target != null)
            {
                for (int i = 0; i < active.Count; i++)
                {
                    var o = active[i];
                    if (!o.killed && o.kind == t.kind && ReferenceEquals(o.target, t.target)) o.killed = true;
                }
            }
            active.Add(t);
            return t;
        }

        internal void KillTarget(UnityEngine.Object target)
        {
            for (int i = 0; i < active.Count; i++)
                if (ReferenceEquals(active[i].target, target)) active[i].killed = true;
        }

        internal void KillEverything()
        {
            for (int i = 0; i < active.Count; i++) active[i].killed = true;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.1f) dt = 0.1f;
            int count = active.Count;
            for (int i = 0; i < count; i++)
            {
                var t = active[i];
                if (t.killed) continue;
                if (!t.TargetAlive)
                {
                    t.killed = true;
                    continue;
                }
                if (t.delay > 0)
                {
                    t.delay -= dt;
                    if (t.delay > 0) continue;
                    dt = -t.delay;
                    t.delay = 0;
                }
                if (!t.started)
                {
                    t.started = true;
                    if (t.fromCurrent) t.Capture();
                }
                t.elapsed += dt;
                float p = t.duration <= 0 ? 1f : Mathf.Clamp01(t.elapsed / t.duration);
                t.Apply(p);
                if (p >= 1f)
                {
                    t.killed = true;
                    var done = t.onComplete;
                    t.onComplete = null;
                    if (done != null)
                    {
                        try
                        {
                            done();
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e);
                        }
                    }
                }
                dt = Time.unscaledDeltaTime > 0.1f ? 0.1f : Time.unscaledDeltaTime;
            }
            // Compact: recycle finished tweens (tweens started during callbacks stay in the list).
            int w = 0;
            for (int i = 0; i < active.Count; i++)
            {
                var t = active[i];
                if (t.killed)
                {
                    t.Reset();
                    pool.Push(t);
                }
                else
                {
                    active[w++] = t;
                }
            }
            active.RemoveRange(w, active.Count - w);
        }
    }
}
