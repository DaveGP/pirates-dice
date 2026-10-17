using System;
using PiratesDice.AI;
using UnityEngine;
using UnityEngine.UI;

namespace PiratesDice.Game.UI
{
    /// <summary>Main menu: pick how many AI pirates join, how sharp they are, and the house rules.</summary>
    public sealed class MenuView
    {
        readonly GameConfig _config;
        readonly Action<GameConfig> _onStart;
        Text _opponents;
        readonly Button[] _difficulty = new Button[3];
        Button _wild, _spotOn, _palifico;

        public readonly GameObject Root;

        public MenuView(Transform column, GameConfig config, Action<GameConfig> onStart)
        {
            _config = config;
            _onStart = onStart;

            Root = UiKit.Rect("Menu", column).gameObject;
            UiKit.Stretch((RectTransform)Root.transform);
            UiKit.Column(Root, 36, 60, TextAnchor.MiddleCenter);

            Text title = UiKit.Label("Title", Root.transform, "Pirate's Dice", 128, Palette.Gold, style: FontStyle.Bold);
            UiKit.Size(title, height: 170);
            Text tagline = UiKit.Label("Tagline", Root.transform, "Liar's Dice on the high seas", 46, Palette.Parchment, style: FontStyle.Italic);
            UiKit.Size(tagline, height: 80);

            Spacer(40);
            SectionLabel("Pirates at the table");
            RectTransform count = Row(130);
            UiKit.Size(UiKit.Button("Fewer", count, "-", Palette.Wood, () => ChangeOpponents(-1), 72), 130, 130);
            _opponents = UiKit.Label("Count", count, "", 52, Palette.Bone);
            UiKit.Size(_opponents, 520, 130);
            UiKit.Size(UiKit.Button("More", count, "+", Palette.Wood, () => ChangeOpponents(1), 72), 130, 130);

            SectionLabel("AI difficulty");
            RectTransform diff = Row(120);
            string[] names = { "Easy", "Normal", "Hard" };
            for (int i = 0; i < 3; i++)
            {
                var level = (AiDifficulty)i;
                _difficulty[i] = UiKit.Button(names[i], diff, names[i], Palette.Wood, () => { _config.Difficulty = level; Refresh(); });
                UiKit.Size(_difficulty[i], 280, 120);
            }

            SectionLabel("House rules");
            RectTransform rules = Row(120);
            _wild = RuleButton(rules, () => _config.Rules.WildOnes = !_config.Rules.WildOnes);
            _spotOn = RuleButton(rules, () => _config.Rules.SpotOn = !_config.Rules.SpotOn);
            _palifico = RuleButton(rules, () => _config.Rules.Palifico = !_config.Rules.Palifico);

            Spacer(60);
            Button start = UiKit.Button("SetSail", Root.transform, "Set sail!", Palette.Blood, () => _onStart(_config), 64);
            UiKit.Size(start, 640, 170);

            Text online = UiKit.Label("Online", Root.transform, "Online tables arrive in a later update.", 36, Palette.Parchment);
            UiKit.Size(online, height: 60);

            Refresh();
        }

        void ChangeOpponents(int delta)
        {
            _config.AiOpponents = Mathf.Clamp(_config.AiOpponents + delta, 1, Core.RuleSettings.MaxSeats - 1);
            Refresh();
        }

        void Refresh()
        {
            int pirates = _config.AiOpponents;
            _opponents.text = $"You + {pirates} AI pirate{(pirates == 1 ? "" : "s")}";
            for (int i = 0; i < _difficulty.Length; i++)
                _difficulty[i].image.color = (int)_config.Difficulty == i ? Palette.Gold : Palette.Wood;
            SetRule(_wild, "Wild 1s", _config.Rules.WildOnes);
            SetRule(_spotOn, "Spot on", _config.Rules.SpotOn);
            SetRule(_palifico, "Palifico", _config.Rules.Palifico);
        }

        static void SetRule(Button button, string name, bool on)
        {
            UiKit.SetButtonText(button, $"{name}: {(on ? "On" : "Off")}");
            button.image.color = on ? Palette.Kelp : Palette.Disabled;
        }

        Button RuleButton(Transform parent, Action toggle)
        {
            Button button = UiKit.Button("Rule", parent, "", Palette.Kelp, () => { toggle(); Refresh(); }, 38);
            UiKit.Size(button, 300, 120);
            return button;
        }

        RectTransform Row(float height)
        {
            RectTransform row = UiKit.Rect("Row", Root.transform);
            UiKit.Row(row.gameObject, 24);
            UiKit.Size(row, height: height);
            return row;
        }

        void SectionLabel(string text)
        {
            Text label = UiKit.Label("Section", Root.transform, text, 40, Palette.Parchment, style: FontStyle.Bold);
            UiKit.Size(label, height: 56);
        }

        void Spacer(float height) => UiKit.Size(UiKit.Rect("Spacer", Root.transform), height: height);
    }
}
