using System;
using ColourCascade.Procedural;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColourCascade.App
{
    public enum ButtonStyle
    {
        Primary,
        Secondary,
        Success,
        Gold,
        Danger,
        Ghost,
        Teal,
        Disabled,
    }

    /// <summary>
    /// The game's button: a pressable "candy" face over a darker lip. Debounced so repeated taps can never
    /// trigger an action twice, plays the tap sound and haptic, and still reports taps while disabled so
    /// the UI can explain why an action is unavailable.
    /// </summary>
    public sealed class CCButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler, IPointerExitHandler
    {
        public Action OnClick;
        /// <summary>Invoked instead of OnClick when the button is not interactable.</summary>
        public Action OnDisabledClick;
        public float DebounceSeconds = 0.35f;
        public string ClickSound = SfxNames.Tap;
        public bool Haptic = true;

        public Image Face;
        public Image Lip;
        public Image Gloss;
        public RectTransform FaceRect;
        public Text Label;
        public Image IconImage;
        public ButtonStyle Style;

        float lastClick = -10f;
        bool pressed;
        bool interactable = true;
        float faceRestY;
        float pressDepth = 8f;

        public bool Interactable
        {
            get => interactable;
            set
            {
                interactable = value;
                ApplyStyle();
            }
        }

        public void Configure(ButtonStyle style, float lipDepth)
        {
            Style = style;
            pressDepth = lipDepth * 0.7f;
            if (FaceRect != null) faceRestY = FaceRect.anchoredPosition.y;
            ApplyStyle();
        }

        public void SetStyle(ButtonStyle style)
        {
            Style = style;
            ApplyStyle();
        }

        public void SetLabel(string text)
        {
            if (Label != null) Label.text = text;
        }

        void ApplyStyle()
        {
            var style = interactable ? Style : ButtonStyle.Disabled;
            Color face, lip, text;
            Colours(style, out face, out lip, out text);
            if (Face != null) Face.color = face;
            if (Lip != null) Lip.color = lip;
            if (Label != null) Label.color = text;
            if (IconImage != null) IconImage.color = text;
            if (Gloss != null) Gloss.color = new Color(1, 1, 1, style == ButtonStyle.Ghost || style == ButtonStyle.Disabled ? 0.35f : 1f);
        }

        public static void Colours(ButtonStyle style, out Color face, out Color lip, out Color text)
        {
            text = Theme.Text;
            switch (style)
            {
                case ButtonStyle.Primary: face = Theme.Primary; lip = Theme.PrimaryLip; break;
                case ButtonStyle.Secondary: face = Theme.Secondary; lip = Theme.SecondaryLip; break;
                case ButtonStyle.Success: face = Theme.Success; lip = Theme.SuccessLip; break;
                case ButtonStyle.Gold: face = Theme.Gold; lip = Theme.GoldLip; text = Theme.TextDark; break;
                case ButtonStyle.Danger: face = Theme.Danger; lip = Theme.PrimaryLip; break;
                case ButtonStyle.Teal: face = Theme.Teal; lip = Theme.Hex("#1E8F88"); text = Theme.TextDark; break;
                case ButtonStyle.Ghost: face = Theme.SurfaceHigh; lip = Theme.SurfaceLow; text = Theme.TextSecondary; break;
                default: face = Theme.Disabled; lip = Theme.DisabledLip; text = Theme.TextMuted; break;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            pressed = true;
            if (FaceRect != null) Tw.Move(FaceRect, new Vector2(FaceRect.anchoredPosition.x, faceRestY - pressDepth), 0.06f, Ease.OutQuad);
            Tw.Scale(transform, 0.97f, 0.08f, Ease.OutQuad);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (pressed) Release();
        }

        void Release()
        {
            pressed = false;
            if (FaceRect != null) Tw.Move(FaceRect, new Vector2(FaceRect.anchoredPosition.x, faceRestY), 0.12f, Ease.OutBack);
            Tw.Scale(transform, 1f, 0.14f, Ease.OutBack);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            float now = Time.unscaledTime;
            if (now - lastClick < DebounceSeconds) return;
            lastClick = now;
            if (!interactable)
            {
                if (OnDisabledClick != null)
                {
                    AudioService.Sfx(SfxNames.Invalid, 0.6f);
                    OnDisabledClick();
                }
                return;
            }
            if (!string.IsNullOrEmpty(ClickSound)) AudioService.Sfx(ClickSound);
            if (Haptic) Haptics.Light();
            OnClick?.Invoke();
        }

        /// <summary>Allows code (tutorials, tests) to trigger the button exactly as a tap would.</summary>
        public void SimulateClick()
        {
            OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        }
    }
}
