using System.Collections.Generic;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// Pooled UI particles, shockwave rings and floating text. A fixed pool is simulated in Update with no
    /// per-frame allocations. Reduced Motion emits far fewer particles.
    /// </summary>
    public sealed class FxLayer : MonoBehaviour
    {
        const int MaxParticles = 220;

        struct Particle
        {
            public Vector2 Pos, Vel;
            public float Life, MaxLife, Rot, RotVel, Size, Gravity, EndScale;
            public Color Colour;
            public bool Active;
        }

        readonly Particle[] particles = new Particle[MaxParticles];
        readonly Image[] images = new Image[MaxParticles];
        readonly List<Text> textPool = new List<Text>();
        RectTransform rt;
        int nextSlot;
        int activeCount;

        public static FxLayer Create(Transform parent, string name = "Fx")
        {
            var rt = UI.Rect(name, parent);
            rt.Fill();
            var fx = rt.gameObject.AddComponent<FxLayer>();
            fx.rt = rt;
            return fx;
        }

        static bool Reduced => Tw.Reduced;

        Image Slot(int i)
        {
            if (images[i] == null)
            {
                images[i] = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.ParticleShard), Color.white, "P");
                images[i].gameObject.SetActive(false);
            }
            return images[i];
        }

        int Allocate()
        {
            for (int n = 0; n < MaxParticles; n++)
            {
                int i = (nextSlot + n) % MaxParticles;
                if (!particles[i].Active)
                {
                    nextSlot = (i + 1) % MaxParticles;
                    return i;
                }
            }
            int r = nextSlot;
            nextSlot = (nextSlot + 1) % MaxParticles;
            return r;
        }

        void Emit(Vector2 pos, Vector2 vel, Color colour, float size, float life, float gravity, string sprite, float endScale = 0.2f)
        {
            int i = Allocate();
            var img = Slot(i);
            var s = ArtLibrary.Instance.Get(sprite);
            if (img.sprite != s) img.sprite = s;
            img.color = colour;
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.rectTransform.anchoredPosition = pos;
            if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
            if (!particles[i].Active) activeCount++;
            particles[i] = new Particle
            {
                Pos = pos, Vel = vel, Life = 0, MaxLife = life, Rot = Random.Range(0, 360f), RotVel = Random.Range(-400f, 400f),
                Size = size, Gravity = gravity, Colour = colour, Active = true, EndScale = endScale,
            };
        }

        /// <summary>Burst of tile shards (used when tiles are cleared).</summary>
        public void Burst(Vector2 pos, Color colour, int count, float speed, string sprite = ArtNames.ParticleShard, float size = 26f)
        {
            if (Reduced) count = Mathf.Max(1, count / 3);
            for (int k = 0; k < count; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2);
                float sp = speed * Random.Range(0.45f, 1.1f);
                var vel = new Vector2(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp + speed * 0.35f);
                var c = Color.Lerp(colour, Color.white, Random.Range(0f, 0.35f));
                Emit(pos, vel, c, size * Random.Range(0.6f, 1.25f), Random.Range(0.45f, 0.75f), 1900f, sprite);
            }
        }

        public void BurstAt(RectTransform target, Color colour, int count, float speed)
        {
            Burst(ToLocal(target.position), colour, count, speed, ArtNames.ParticleSpark, 34f);
        }

        /// <summary>Floating sparkles that drift upwards (rewards, unlocks).</summary>
        public void Sparkle(Vector2 pos, Color colour, int count, float radius = 60f)
        {
            if (Reduced) count = Mathf.Max(1, count / 3);
            for (int k = 0; k < count; k++)
            {
                var offset = Random.insideUnitCircle * radius;
                var vel = new Vector2(Random.Range(-60f, 60f), Random.Range(80f, 220f));
                Emit(pos + offset, vel, colour, Random.Range(22f, 44f), Random.Range(0.6f, 1.1f), -60f, ArtNames.ParticleSpark, 0f);
            }
        }

        /// <summary>Expanding shockwave ring.</summary>
        public void Ring(Vector2 pos, Color colour, float size)
        {
            var img = UI.Image(rt, ArtLibrary.Instance.Get(ArtNames.ParticleRing), colour, "Ring");
            img.rectTransform.anchoredPosition = pos;
            img.rectTransform.sizeDelta = new Vector2(size * 0.3f, size * 0.3f);
            Tw.Size(img.rectTransform, new Vector2(size, size), 0.4f, Ease.OutCubic);
            Tw.Fade(img, 0f, 0.4f, Ease.InQuad).OnComplete(() =>
            {
                if (img != null) Destroy(img.gameObject);
            });
        }

        /// <summary>Text that rises and fades ("+90", "Great!").</summary>
        public Text FloatText(Vector2 pos, string text, Color colour, int size, float rise = 120f, float duration = 0.85f, float delay = 0f, float popScale = 1.3f)
        {
            Text t = null;
            for (int i = 0; i < textPool.Count; i++)
                if (!textPool[i].gameObject.activeSelf) { t = textPool[i]; break; }
            if (t == null)
            {
                t = UI.Text(rt, "", size, colour, TextAnchor.MiddleCenter, true, "FloatText");
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.rectTransform.sizeDelta = new Vector2(700, 160);
                var outline = t.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.05f, 0.02f, 0.15f, 0.75f);
                outline.effectDistance = new Vector2(3, -3);
                UI.AddShadow(t, -6, 0.45f);
                textPool.Add(t);
            }
            t.text = text;
            t.fontSize = size;
            t.color = colour;
            t.rectTransform.anchoredPosition = pos;
            t.transform.localScale = Vector3.one * 0.4f;
            t.transform.SetAsLastSibling();
            t.gameObject.SetActive(true);
            var target = t;
            Tw.Scale(t.transform, popScale, 0.18f, Ease.OutBack, delay);
            Tw.Move(t.rectTransform, pos + new Vector2(0, rise), duration, Ease.OutCubic, delay);
            Tw.Fade(t, 0f, duration * 0.45f, Ease.InQuad, delay + duration * 0.55f).OnComplete(() =>
            {
                if (target != null) target.gameObject.SetActive(false);
            });
            return t;
        }

        public Vector2 ToLocal(Vector3 world)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, RectTransformUtility.WorldToScreenPoint(null, world), null, out local);
            return local;
        }

        public void Clear()
        {
            for (int i = 0; i < MaxParticles; i++)
            {
                particles[i].Active = false;
                if (images[i] != null) images[i].gameObject.SetActive(false);
            }
            activeCount = 0;
            foreach (var t in textPool) t.gameObject.SetActive(false);
        }

        void Update()
        {
            if (activeCount == 0) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            for (int i = 0; i < MaxParticles; i++)
            {
                if (!particles[i].Active) continue;
                var p = particles[i];
                p.Life += dt;
                var img = images[i];
                if (p.Life >= p.MaxLife)
                {
                    p.Active = false;
                    particles[i] = p;
                    img.gameObject.SetActive(false);
                    activeCount--;
                    continue;
                }
                p.Vel.y -= p.Gravity * dt;
                p.Vel *= 1f - 1.2f * dt;
                p.Pos += p.Vel * dt;
                p.Rot += p.RotVel * dt;
                float k = p.Life / p.MaxLife;
                var r = img.rectTransform;
                r.anchoredPosition = p.Pos;
                r.localEulerAngles = new Vector3(0, 0, p.Rot);
                float scale = Mathf.Lerp(1f, p.EndScale, k * k);
                r.localScale = new Vector3(scale, scale, 1);
                var c = p.Colour;
                c.a = p.Colour.a * (1f - k * k);
                img.color = c;
                particles[i] = p;
            }
        }
    }
}
