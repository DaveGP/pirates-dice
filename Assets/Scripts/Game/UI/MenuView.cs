using System;
using PiratesDice.AI;
using PiratesDice.Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace PiratesDice.Game.UI
{
    /// <summary>Main menu: pick how many AI pirates join, how sharp they are, and the house rules, or head online.</summary>
    public sealed class MenuView
    {
        readonly GameConfig _config;
        readonly Action<GameConfig> _onStart;
        Text _opponents;
        readonly Button[] _difficulty = new Button[3];
        Button _wild, _spotOn, _palifico, _music, _sfx;

        public readonly GameObject Root;

        /// <param name="notice">A line to show under the menu, such as why an online table closed.</param>
        public MenuView(Transform column, GameConfig config, Action<GameConfig> onStart, Action onTutorial, Action onOnline, string notice = null)
        {
            _config = config;
            _onStart = onStart;

            Root = UiKit.Rect("Menu", column).gameObject;
            UiKit.Stretch((RectTransform)Root.transform);
            UiKit.Column(Root, 26, 50, TextAnchor.MiddleCenter);

            Text title = UiKit.Label("Title", Root.transform, "Pirate's Dice", 128, Palette.Gold, style: FontStyle.Bold);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.WoodDark;
            outline.effectDistance = new Vector2(4, -4);
            UiKit.Shadowed(title);
            UiKit.Size(title, height: 170);
            Text tagline = UiKit.Label("Tagline", Root.transform, "Liar's Dice on the high seas", 46, Palette.Parchment, style: FontStyle.Italic);
            UiKit.Shadowed(tagline);
            UiKit.Size(tagline, height: 70);

            // The crew, waiting at the table.
            RectTransform crew = Row(120);
            for (int i = 0; i < Captains.All.Length; i++)
            {
                Image face = UiKit.Picture("Captain", crew, Art.Portrait(Captains.All[i].Name), Color.white, sliced: false);
                UiKit.Size(face, 116, 116);
                Tween.PopIn(face.transform, 0.35f, 0.1f + i * 0.08f);
            }

            RectTransform modes = Row(120);
            Button howTo = UiKit.Button("HowToPlay", modes, "How to play", Palette.Kelp, () => onTutorial(), 46);
            UiKit.Size(howTo, 440, 120);
            Button online = UiKit.Button("PlayOnline", modes, "Play online", Palette.Kelp, () => { AudioDirector.Play(Sfx.Click); onOnline(); }, 46);
            UiKit.Size(online, 440, 120);
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
                _difficulty[i] = UiKit.Button(names[i], diff, names[i], Palette.Wood, () => { _config.Difficulty = level; AudioDirector.Play(Sfx.Click); Refresh(); });
                UiKit.Size(_difficulty[i], 280, 120);
            }

            SectionLabel("House rules");
            RectTransform rules = Row(120);
            _wild = RuleButton(rules, () => _config.Rules.WildOnes = !_config.Rules.WildOnes);
            _spotOn = RuleButton(rules, () => _config.Rules.SpotOn = !_config.Rules.SpotOn);
            _palifico = RuleButton(rules, () => _config.Rules.Palifico = !_config.Rules.Palifico);

            SectionLabel("Sound");
            RectTransform sound = Row(110);
            _music = UiKit.Button("Music", sound, "", Palette.Kelp, () => { ToggleAudio(music: true); Refresh(); }, 38);
            UiKit.Size(_music, 400, 110);
            _sfx = UiKit.Button("Effects", sound, "", Palette.Kelp, () => { ToggleAudio(music: false); Refresh(); }, 38);
            UiKit.Size(_sfx, 400, 110);

            Spacer(20);
            Button start = UiKit.Button("SetSail", Root.transform, "Set sail!", Palette.Blood, () => { AudioDirector.Play(Sfx.Coin); _onStart(_config); }, 64);
            UiKit.Size(start, 640, 160);
            start.gameObject.AddComponent<Pulse>().Amount = 0.025f;

            Text noticeLabel = UiKit.Label("Notice", Root.transform, notice ?? "", 34, Palette.Gold, style: FontStyle.Italic);
            UiKit.Shadowed(noticeLabel);
            UiKit.Size(noticeLabel, 960, 50);

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
            AudioDirector audio = AudioDirector.Instance;
            SetRule(_music, "Music", audio != null && audio.MusicOn);
            SetRule(_sfx, "Sound effects", audio != null && audio.SfxOn);
        }

        static void ToggleAudio(bool music)
        {
            AudioDirector audio = AudioDirector.Instance;
            if (audio == null) return;
            if (music) audio.MusicOn = !audio.MusicOn;
            else audio.SfxOn = !audio.SfxOn;
            AudioDirector.Play(Sfx.Click);
        }

        static void SetRule(Button button, string name, bool on)
        {
            UiKit.SetButtonText(button, $"{name}: {(on ? "On" : "Off")}");
            button.image.color = on ? Palette.Kelp : Palette.Disabled;
        }

        Button RuleButton(Transform parent, Action toggle)
        {
            Button button = UiKit.Button("Rule", parent, "", Palette.Kelp, () => { toggle(); AudioDirector.Play(Sfx.Click); Refresh(); }, 38);
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
            Text label = UiKit.Label("Section", Root.transform, text, 38, Palette.Parchment, style: FontStyle.Bold);
            UiKit.Shadowed(label);
            UiKit.Size(label, height: 50);
        }

        void Spacer(float height) => UiKit.Size(UiKit.Rect("Spacer", Root.transform), height: height);
    }
}
