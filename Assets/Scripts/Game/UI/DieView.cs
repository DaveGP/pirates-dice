using UnityEngine;
using UnityEngine.UI;

namespace PiratesDice.Game.UI
{
    /// <summary>A painted die face with procedural pips as a fallback.</summary>
    public sealed class DieView
    {
        static readonly Vector2Int[][] Layouts =
        {
            new Vector2Int[0],
            new[] { new Vector2Int(1, 1) },
            new[] { new Vector2Int(0, 0), new Vector2Int(2, 2) },
            new[] { new Vector2Int(0, 0), new Vector2Int(1, 1), new Vector2Int(2, 2) },
            new[] { new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(0, 2), new Vector2Int(2, 2) },
            new[] { new Vector2Int(0, 0), new Vector2Int(2, 0), new Vector2Int(1, 1), new Vector2Int(0, 2), new Vector2Int(2, 2) },
            new[] { new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(0, 2), new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(2, 2) },
        };

        public readonly Image Body;
        readonly Image[] _pips = new Image[7];

        public DieView(Transform parent, float size)
        {
            Body = UiKit.Panel("Die", parent, Palette.Bone);
            UiKit.Size(Body, size, size);
            for (int i = 0; i < _pips.Length; i++)
            {
                RectTransform rt = UiKit.Rect("Pip", Body.transform);
                rt.sizeDelta = new Vector2(size * 0.2f, size * 0.2f);
                _pips[i] = rt.gameObject.AddComponent<Image>();
                _pips[i].sprite = UiKit.Circle;
                _pips[i].color = Palette.Ink;
                _pips[i].raycastTarget = false;
            }
        }

        public void SetFace(int face, bool highlight = false, bool dim = false)
        {
            Sprite sprite = Art.Die(face);
            Body.sprite = sprite != null ? sprite : UiKit.Rounded;
            Body.type = sprite != null ? Image.Type.Simple : Image.Type.Sliced;
            Body.color = highlight ? Palette.Gold : dim ? new Color(0.75f, 0.72f, 0.66f)
                : sprite != null ? Color.white : Palette.Bone;
            Vector2Int[] layout = Layouts[sprite != null ? 0 : Mathf.Clamp(face, 0, 6)];
            for (int i = 0; i < _pips.Length; i++)
            {
                bool on = i < layout.Length;
                _pips[i].gameObject.SetActive(on);
                if (!on) continue;
                var anchor = new Vector2(0.22f + 0.28f * layout[i].x, 0.78f - 0.28f * layout[i].y);
                RectTransform rt = _pips[i].rectTransform;
                rt.anchorMin = rt.anchorMax = anchor;
                rt.anchoredPosition = Vector2.zero;
                _pips[i].color = face == 1 ? Palette.Blood : Palette.Ink;
            }
        }
    }
}
