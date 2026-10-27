using System;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.UI;

namespace ColourCascade.App
{
    /// <summary>
    /// Builders for every UI element, so all screens share one visual language (rounded "candy" surfaces,
    /// Fredoka type, consistent spacing) and nothing depends on hand-made prefabs.
    /// </summary>
    public static class UI
    {
        static ArtLibrary Art => ArtLibrary.Instance;

        // ---------------------------------------------------------------- RectTransform helpers

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5; // UI
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        /// <summary>Stretch to fill the parent with insets.</summary>
        public static RectTransform Fill(this RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Fixed size, positioned relative to an anchor point of the parent.</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
            return rt;
        }

        public static RectTransform Centre(this RectTransform rt, float width, float height, float x = 0, float y = 0)
            => rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, height));

        /// <summary>Full width strip anchored to the top of the parent.</summary>
        public static RectTransform TopStrip(this RectTransform rt, float height, float fromTop = 0, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -fromTop - height);
            rt.offsetMax = new Vector2(-right, -fromTop);
            return rt;
        }

        /// <summary>Full width strip anchored to the bottom of the parent.</summary>
        public static RectTransform BottomStrip(this RectTransform rt, float height, float fromBottom = 0, float left = 0, float right = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(left, fromBottom);
            rt.offsetMax = new Vector2(-right, fromBottom + height);
            return rt;
        }

        public static LayoutElement Layout(this Component c, float preferredHeight = -1, float preferredWidth = -1, float flexibleHeight = -1, float flexibleWidth = -1)
        {
            var le = GetOrAdd<LayoutElement>(c.gameObject);
            if (preferredHeight >= 0) { le.preferredHeight = preferredHeight; le.minHeight = preferredHeight; }
            if (preferredWidth >= 0) { le.preferredWidth = preferredWidth; le.minWidth = preferredWidth; }
            if (flexibleHeight >= 0) le.flexibleHeight = flexibleHeight;
            if (flexibleWidth >= 0) le.flexibleWidth = flexibleWidth;
            return le;
        }

        /// <summary>
        /// Returns the existing component or adds one. Never use "GetComponent() ?? AddComponent()": in the Editor
        /// GetComponent returns a "fake null" object that C#'s ?? and ?. operators treat as a real object.
        /// </summary>
        public static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.UpperCenter)
        {
            var g = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childAlignment = align;
            g.childControlHeight = true;
            g.childControlWidth = true;
            g.childForceExpandHeight = false;
            g.childForceExpandWidth = true;
            return g;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing, RectOffset padding = null, TextAnchor align = TextAnchor.MiddleCenter, bool expandWidth = true)
        {
            var g = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            g.spacing = spacing;
            g.padding = padding ?? new RectOffset();
            g.childAlignment = align;
            g.childControlHeight = true;
            g.childControlWidth = true;
            g.childForceExpandHeight = true;
            g.childForceExpandWidth = expandWidth;
            return g;
        }

        public static RectTransform Spacer(Transform parent, float height = -1, float flexible = 1)
        {
            var rt = Rect("Spacer", parent);
            rt.Layout(preferredHeight: height >= 0 ? height : 0, flexibleHeight: flexible, flexibleWidth: flexible);
            return rt;
        }

        // ---------------------------------------------------------------- Graphics

        public static Image Image(Transform parent, Sprite sprite, Color colour, string name = "Image")
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = colour;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>Rounded rectangle with the given corner radius (any size).</summary>
        public static Image Rounded(Transform parent, Color colour, float radius, string name = "Rounded", string sprite = ArtNames.Round)
        {
            var img = Image(parent, Art.Get(sprite), colour, name);
            img.type = UnityEngine.UI.Image.Type.Sliced;
            SetRadius(img, radius, sprite == ArtNames.RoundShadow ? ArtNames.ShadowRadiusPx : ArtNames.RoundRadiusPx);
            return img;
        }

        public static void SetRadius(Image img, float radius, float bakedRadius = ArtNames.RoundRadiusPx)
        {
            img.pixelsPerUnitMultiplier = bakedRadius / Mathf.Max(1f, radius);
        }

        /// <summary>A card surface (filling the parent) with a soft shadow and subtle top sheen. Returns the content rect.</summary>
        public static RectTransform Card(Transform parent, string name, Color? colour = null, float radius = Theme.CardRadius, bool shadow = true)
        {
            var holder = Rect(name, parent);
            holder.Fill();
            if (shadow)
            {
                var sh = Rounded(holder, new Color(0.02f, 0.01f, 0.08f, 0.5f), radius + 8, "Shadow", ArtNames.RoundShadow);
                sh.rectTransform.Fill(-22, -34, -22, -14);
            }
            var face = Rounded(holder, colour ?? Theme.Surface, radius, "Face");
            face.rectTransform.Fill();
            var sheen = Rounded(holder, new Color(1, 1, 1, 0.55f), radius, "Sheen", ArtNames.Gloss);
            sheen.rectTransform.Fill();
            var stroke = Rounded(holder, new Color(1, 1, 1, 0.07f), radius, "Stroke", ArtNames.RoundStroke);
            stroke.rectTransform.Fill();
            var content = Rect("Content", holder);
            content.Fill();
            return content;
        }

        public static Image Icon(Transform parent, Icon icon, Color colour, float size)
        {
            var img = Image(parent, Art.Icon(icon), colour, "Icon_" + icon);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.preserveAspect = true;
            return img;
        }

        public static Image Sprite(Transform parent, string spriteName, float size, Color? colour = null)
        {
            var img = Image(parent, Art.Get(spriteName), colour ?? Color.white, spriteName);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            img.preserveAspect = true;
            return img;
        }

        // ---------------------------------------------------------------- Text

        public static Text Text(Transform parent, string text, int size, Color colour, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true, string name = "Text")
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = bold ? Fonts.Bold : Fonts.Medium;
            t.fontSize = size;
            t.color = colour;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            t.lineSpacing = 0.95f;
            return t;
        }

        /// <summary>Single-line text that shrinks to fit its box.</summary>
        public static Text FitText(Transform parent, string text, int maxSize, Color colour, TextAnchor align = TextAnchor.MiddleCenter, bool bold = true, string name = "Text")
        {
            var t = Text(parent, text, maxSize, colour, align, bold, name);
            t.resizeTextForBestFit = true;
            t.resizeTextMaxSize = maxSize;
            t.resizeTextMinSize = Mathf.Max(14, maxSize / 2);
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        public static Shadow AddShadow(Graphic g, float y = -4f, float alpha = 0.35f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0.03f, 0.01f, 0.12f, alpha);
            s.effectDistance = new Vector2(0, y);
            return s;
        }

        /// <summary>"COLOUR" in the six tile colours, for the logo.</summary>
        public static string RainbowWord(string word)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < word.Length; i++)
            {
                var c = Theme.Tile(i % 6);
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(c)).Append('>').Append(word[i]).Append("</color>");
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- Buttons

        /// <summary>Full candy button. Width/height come from the caller's layout.</summary>
        public static CCButton Button(Transform parent, string label, ButtonStyle style, Action onClick, Icon? icon = null, int fontSize = Theme.FontLarge,
            float radius = Theme.ButtonRadius, float lip = 10f, string name = null)
        {
            var root = Rect(name ?? "Button_" + label, parent);
            var btn = root.gameObject.AddComponent<CCButton>();
            var shadow = Rounded(root, new Color(0.02f, 0.01f, 0.08f, 0.42f), radius + 6, "Shadow", ArtNames.RoundShadow);
            shadow.rectTransform.Fill(-14, -lip - 18, -14, -6);
            var lipImg = Rounded(root, Color.white, radius, "Lip");
            lipImg.rectTransform.Fill();
            var faceRt = Rect("Face", root);
            faceRt.Fill(0, lip, 0, 0);
            var face = faceRt.gameObject.AddComponent<Image>();
            face.sprite = Art.Get(ArtNames.Round);
            face.type = UnityEngine.UI.Image.Type.Sliced;
            SetRadius(face, radius);
            face.raycastTarget = true;
            var gloss = Rounded(faceRt, Color.white, radius, "Gloss", ArtNames.Gloss);
            gloss.rectTransform.Fill();

            var content = Rect("Content", faceRt);
            content.Fill(18, 2, 18, 0);
            // The row sets each child's width from its preferred width (icon size, label text width) and keeps
            // their own heights, so icon and label always sit side by side, centred, even after a relabel.
            var row = HStack(content, 16, null, TextAnchor.MiddleCenter, false);
            row.childControlWidth = true;
            row.childControlHeight = false;
            Image iconImg = null;
            if (icon.HasValue)
            {
                iconImg = Icon(content, icon.Value, Color.white, fontSize * 1.25f);
                iconImg.Layout(preferredWidth: fontSize * 1.25f);
            }
            // An empty label still gets a Text so it can be set later (SetLabel); only icon buttons pass null.
            Text text = null;
            if (label != null)
            {
                text = Text(content, label, fontSize, Color.white, TextAnchor.MiddleCenter, true, "Label");
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.rectTransform.sizeDelta = new Vector2(10, fontSize * 1.3f);
                AddShadow(text, -3, 0.3f);
            }

            btn.Face = face;
            btn.Lip = lipImg;
            btn.Gloss = gloss;
            btn.FaceRect = faceRt;
            btn.Label = text;
            btn.IconImage = iconImg;
            btn.OnClick = onClick;
            btn.Configure(style, lip);
            return btn;
        }

        /// <summary>Round icon-only button (minimum 96 reference pixels so it is easy to hit).</summary>
        public static CCButton IconButton(Transform parent, Icon icon, ButtonStyle style, Action onClick, float size = Theme.IconButtonSize, string name = null)
        {
            var btn = Button(parent, null, style, onClick, icon, (int)(size * 0.42f), size / 2f, 8f, name ?? "IconButton_" + icon);
            var rt = (RectTransform)btn.transform;
            rt.sizeDelta = new Vector2(size, size);
            btn.IconImage.rectTransform.sizeDelta = new Vector2(size * 0.5f, size * 0.5f);
            btn.IconImage.Layout(preferredWidth: size * 0.5f);
            return btn;
        }

        /// <summary>Small rounded label ("chip") used for statuses and badges.</summary>
        public static Text Chip(Transform parent, string text, Color background, Color textColour, int fontSize = Theme.FontSmall, string name = "Chip")
        {
            var rt = Rect(name, parent);
            var bg = Rounded(rt, background, 30, "Bg");
            bg.rectTransform.Fill();
            var t = Text(rt, text, fontSize, textColour, TextAnchor.MiddleCenter, true, "Label");
            t.rectTransform.Fill(18, 0, 18, 0);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        public static void Destroy(Component c)
        {
            if (c != null) UnityEngine.Object.Destroy(c.gameObject);
        }

        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }
    }
}
