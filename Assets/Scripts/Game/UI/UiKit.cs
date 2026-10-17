using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PiratesDice.Game.UI
{
    /// <summary>Pirate palette for the placeholder UI.</summary>
    public static class Palette
    {
        public static readonly Color Sea = new Color(0.05f, 0.11f, 0.16f);
        public static readonly Color Wood = new Color(0.29f, 0.18f, 0.10f);
        public static readonly Color WoodDark = new Color(0.18f, 0.11f, 0.06f);
        public static readonly Color Parchment = new Color(0.93f, 0.86f, 0.70f);
        public static readonly Color Ink = new Color(0.16f, 0.10f, 0.06f);
        public static readonly Color Gold = new Color(0.91f, 0.70f, 0.24f);
        public static readonly Color Blood = new Color(0.66f, 0.15f, 0.12f);
        public static readonly Color Kelp = new Color(0.20f, 0.45f, 0.30f);
        public static readonly Color Bone = new Color(0.97f, 0.95f, 0.89f);
        public static readonly Color Disabled = new Color(0.45f, 0.40f, 0.35f);
        public static readonly Color Shade = new Color(0f, 0f, 0f, 0.72f);
    }

    /// <summary>
    /// Builds uGUI from code so milestone 1 needs no hand-authored prefabs or scenes. The final art pass in
    /// milestone 2 replaces this with designed screens.
    /// </summary>
    public static class UiKit
    {
        static Font _font;
        static Sprite _circle;
        static Sprite _rounded;

        public static Font Font => _font ? _font : (_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        public static Sprite Circle => _circle ? _circle : (_circle = MakeCircle(64));
        public static Sprite Rounded => _rounded ? _rounded : (_rounded = MakeRounded(64, 14));

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // The layout is a 1080x1920 portrait column. Expand keeps all of it on screen: full width on a
            // phone, centred with sea on either side in a landscape browser.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            EnsureEventSystem();
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rt, float inset = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        public static Image Panel(string name, Transform parent, Color color, bool rounded = true)
        {
            RectTransform rt = Rect(name, parent);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            if (rounded)
            {
                image.sprite = Rounded;
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            RectTransform rt = Rect(name, parent);
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = align;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        public static Button Button(string name, Transform parent, string text, Color color, Action onClick, int fontSize = 44)
        {
            Image bg = Panel(name, parent, color);
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());

            Text label = Label("Label", bg.transform, text, fontSize, Palette.Bone, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 8);
            return button;
        }

        public static void SetButtonText(Button button, string text) =>
            button.GetComponentInChildren<Text>().text = text;

        public static VerticalLayoutGroup Column(GameObject go, float spacing, int padding = 0,
            TextAnchor align = TextAnchor.UpperCenter)
        {
            var layout = go.AddComponent<VerticalLayoutGroup>();
            Configure(layout, spacing, padding, align);
            return layout;
        }

        public static HorizontalLayoutGroup Row(GameObject go, float spacing, int padding = 0,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            Configure(layout, spacing, padding, align);
            return layout;
        }

        static void Configure(HorizontalOrVerticalLayoutGroup layout, float spacing, int padding, TextAnchor align)
        {
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = align;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        public static LayoutElement Size(Component c, float width = -1, float height = -1, float flexWidth = -1, float flexHeight = -1)
        {
            if (!c.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0) le.preferredWidth = width;
            if (height >= 0) le.preferredHeight = height;
            if (flexWidth >= 0) le.flexibleWidth = flexWidth;
            if (flexHeight >= 0) le.flexibleHeight = flexHeight;
            return le;
        }

        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
        }

        static Sprite MakeCircle(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(radius - d)));
            }
            tex.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        }
    }
}
