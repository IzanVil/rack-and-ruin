using ServerGame.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ServerGame.UI
{
    public sealed class Bar
    {
        public readonly RectTransform Rect;
        public readonly Image Fill;
        readonly RectTransform _fillRect;

        public Bar(RectTransform rect, Image fill)
        {
            Rect = rect;
            Fill = fill;
            _fillRect = (RectTransform)fill.transform;
        }

        public void Set(float value01, Color color)
        {
            float v = Mathf.Clamp01(value01);
            _fillRect.anchorMin = new Vector2(0f, 0f);
            _fillRect.anchorMax = new Vector2(v, 1f);
            _fillRect.offsetMin = Vector2.zero;
            _fillRect.offsetMax = Vector2.zero;
            Fill.color = color;
            Fill.enabled = v > 0.0005f;
        }
    }

    /// <summary>Botón con fondo redondeado y etiqueta, con estados de color coherentes.</summary>
    public sealed class UiButton
    {
        public readonly RectTransform Rect;
        public readonly Button Button;
        public readonly Image Background;
        public readonly Text Label;
        public readonly Text SubLabel;

        Color _baseColor;

        public UiButton(RectTransform rect, Button button, Image background, Text label, Text subLabel)
        {
            Rect = rect;
            Button = button;
            Background = background;
            Label = label;
            SubLabel = subLabel;
            _baseColor = background.color;
        }

        public void OnClick(UnityAction action) => Button.onClick.AddListener(action);

        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            Background.color = color;
            Apply();
        }

        public void SetInteractable(bool value)
        {
            if (Button.interactable == value) return;
            Button.interactable = value;
            Apply();
        }

        void Apply()
        {
            var colors = Button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            colors.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.45f);
            colors.fadeDuration = 0.08f;
            Button.colors = colors;
            Background.color = _baseColor;
        }
    }

    // Constructores de widgets. La UI se monta por código, sin prefabs.
    public static class Ui
    {
        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            return rt;
        }

        public static Image NewPanel(string name, Transform parent, Color color, int radius = UiTheme.RadiusPanel)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            if (radius > 0)
            {
                img.sprite = TextureFactory.RoundedRect(radius);
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f;
            }
            else
            {
                img.sprite = TextureFactory.Plain;
            }
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text NewText(string name, Transform parent, string content, int size, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal, bool wrap = false)
        {
            var rt = NewRect(name, parent);
            var text = rt.gameObject.AddComponent<Text>();
            text.font = UiTheme.Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.text = content;
            text.alignment = anchor;
            text.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.supportRichText = true;
            return text;
        }

        public static UiButton NewButton(string name, Transform parent, string label, Color background,
            Color foreground, int fontSize = 15, int radius = UiTheme.RadiusSmall, bool withSubLabel = false)
        {
            var rt = NewRect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = TextureFactory.RoundedRect(radius);
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.color = background;
            img.raycastTarget = true;

            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            button.transition = Selectable.Transition.ColorTint;
            rt.gameObject.AddComponent<PointerCursorHint>().Button = button;

            var labelText = NewText("Label", rt, label, fontSize, foreground,
                withSubLabel ? TextAnchor.LowerLeft : TextAnchor.MiddleCenter, FontStyle.Bold);

            Text sub = null;
            if (withSubLabel)
            {
                // etiqueta arriba, detalle abajo
                var labelRect = labelText.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0.44f);
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(12f, 0f);
                labelRect.offsetMax = new Vector2(-12f, -5f);

                sub = NewText("Sub", rt, string.Empty, 12, UiTheme.TextMuted, TextAnchor.UpperLeft);
                var subRect = sub.rectTransform;
                subRect.anchorMin = Vector2.zero;
                subRect.anchorMax = new Vector2(1f, 0.44f);
                subRect.offsetMin = new Vector2(12f, 5f);
                subRect.offsetMax = new Vector2(-12f, 0f);
            }

            button.onClick.AddListener(Sfx.Click);

            var uiButton = new UiButton(rt, button, img, labelText, sub);
            uiButton.SetBaseColor(background);
            return uiButton;
        }

        public static Bar NewBar(string name, Transform parent, Color trackColor, int radius = 3)
        {
            var track = NewPanel(name, parent, trackColor, radius);
            var fill = NewPanel("Fill", track.rectTransform, UiTheme.Accent, radius);
            Stretch(fill.rectTransform);
            return new Bar(track.rectTransform, fill);
        }

        /// <summary>Etiqueta pequeña con fondo de color (estado de un servidor, avisos...).</summary>
        public static Text NewPill(string name, Transform parent, string content, Color background, Color foreground)
        {
            var panel = NewPanel(name, parent, background, UiTheme.RadiusPill);
            var text = NewText("Text", panel.rectTransform, content, 11, foreground,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform, 6f, 6f, 0f, 0f);
            return text;
        }

        public static RectTransform Stretch(RectTransform rt, float left = 0f, float right = 0f,
            float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Ancla la fila superior con una altura fija.</summary>
        public static RectTransform Top(RectTransform rt, float height, float offsetY = 0f,
            float left = 0f, float right = 0f)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, -height - offsetY);
            rt.offsetMax = new Vector2(-right, -offsetY);
            return rt;
        }

        public static RectTransform Bottom(RectTransform rt, float height, float offsetY = 0f,
            float left = 0f, float right = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.offsetMin = new Vector2(left, offsetY);
            rt.offsetMax = new Vector2(-right, height + offsetY);
            return rt;
        }

        public static RectTransform Left(RectTransform rt, float width, float offsetX = 0f,
            float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.offsetMin = new Vector2(offsetX, bottom);
            rt.offsetMax = new Vector2(offsetX + width, -top);
            return rt;
        }

        public static RectTransform Right(RectTransform rt, float width, float offsetX = 0f,
            float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.offsetMin = new Vector2(-width - offsetX, bottom);
            rt.offsetMax = new Vector2(-offsetX, -top);
            return rt;
        }

        /// <summary>Centra el rect con un tamaño fijo.</summary>
        public static RectTransform Center(RectTransform rt, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        /// <summary>Rect anclado a la esquina superior izquierda con posición y tamaño en píxeles.</summary>
        public static RectTransform Place(RectTransform rt, float x, float y, float width, float height)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(width, height);
            return rt;
        }

        public static VerticalLayoutGroup VBox(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.UpperLeft)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = align;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return layout;
        }

        public static HorizontalLayoutGroup HBox(RectTransform rt, float spacing, RectOffset padding = null,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childAlignment = align;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            return layout;
        }

        public static LayoutElement Fixed(Component target, float width = -1f, float height = -1f,
            float flexibleWidth = -1f)
        {
            var element = target.gameObject.GetComponent<LayoutElement>();
            if (element == null) element = target.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) element.preferredWidth = width;
            if (height >= 0f) element.preferredHeight = height;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            return element;
        }

        /// <summary>Línea divisoria de 1 px.</summary>
        public static Image Divider(Transform parent, Color color)
        {
            var img = NewPanel("Divider", parent, color, 0);
            Fixed(img, height: 1f);
            return img;
        }
    }
}
