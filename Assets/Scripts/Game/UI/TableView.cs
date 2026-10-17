using System;
using System.Collections;
using System.Collections.Generic;
using PiratesDice.Core;
using PiratesDice.Game.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace PiratesDice.Game.UI
{
    /// <summary>
    /// The table screen: opponents' cups, the standing bid, your dice, and the bid picker. It reads state from
    /// the <see cref="TableController"/> and sends your moves through the local seat; it never decides outcomes.
    /// In the tutorial a coach panel replaces the log and steers each move.
    /// </summary>
    public sealed class TableView
    {
        const int LogLines = 5;

        readonly TableController _table;
        readonly Action _onMenu;
        readonly Action _onPlayAgain;
        public readonly GameObject Root;

        Text _round, _bidWho, _bid, _action, _log, _myTitle, _quantity, _hint;
        Image _banner;
        Button _soundButton;
        readonly List<SeatCard> _cards = new List<SeatCard>();
        readonly List<DieView> _myDice = new List<DieView>();
        Transform _myDiceRow;
        readonly Button[] _faceButtons = new Button[6];
        readonly DieView[] _faceDice = new DieView[6];
        Button _minus, _plus, _bidButton, _liarButton, _spotButton;
        readonly List<string> _logLines = new List<string>();
        string _lastLogged;
        bool _rolling;

        int _face = 2, _qty = 1;

        // Tutorial coach.
        GameObject _logPanel, _coachPanel;
        Text _coachText;
        Button _coachNext;
        int _introPage;

        // Reveal overlay.
        GameObject _overlay;
        CanvasGroup _overlayGroup, _overlayButtons;
        Text _overlayTitle, _overlayText, _overlayCoach;
        Transform _overlayRows;
        Button _overlayPrimary, _overlaySecondary;
        Coroutine _revealRoutine, _rollRoutine;

        public TableView(Transform column, TableController table, Action onMenu, Action onPlayAgain)
        {
            _table = table;
            _onMenu = onMenu;
            _onPlayAgain = onPlayAgain;

            Root = UiKit.Rect("Table", column).gameObject;
            UiKit.Stretch((RectTransform)Root.transform);
            UiKit.Column(Root, 16, 28);

            BuildHeader();
            BuildOpponents();
            BuildBanner();
            BuildLogAndCoach();
            BuildMyCup();
            BuildPicker();
            BuildActions();
            BuildOverlay();

            _table.Changed += Refresh;
            _table.Revealed += ShowReveal;
            _table.RoundStarted += OnRoundStarted;
            _table.BidPlaced += OnBidPlaced;
            _table.LocalPlayer.TurnStarted += OnMyTurn;

            if (_table.Tutorial != null) ShowIntroPage(0);
            Refresh();
            OnRoundStarted();
            if (_table.LocalPlayer.IsWaiting) OnMyTurn();
        }

        public void Dispose()
        {
            _table.Changed -= Refresh;
            _table.Revealed -= ShowReveal;
            _table.RoundStarted -= OnRoundStarted;
            _table.BidPlaced -= OnBidPlaced;
            _table.LocalPlayer.TurnStarted -= OnMyTurn;
            Tween.Stop(_revealRoutine);
            Tween.Stop(_rollRoutine);
            UnityEngine.Object.Destroy(Root);
        }

        // ---------- building ----------

        void BuildHeader()
        {
            RectTransform header = UiKit.Rect("Header", Root.transform);
            UiKit.Row(header.gameObject, 16);
            UiKit.Size(header, height: 90);
            _round = UiKit.Label("Round", header, "", 34, Palette.Parchment, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Shadowed(_round);
            UiKit.Size(_round, flexWidth: 1, height: 90);
            _soundButton = UiKit.Button("Sound", header, "", Palette.Wood, ToggleSound, 32);
            UiKit.Size(_soundButton, 200, 84);
            UiKit.Size(UiKit.Button("Menu", header, _table.IsOnline ? "Leave" : "Menu", Palette.Wood, () => _onMenu(), 36), 180, 84);
            RefreshSoundButton();
        }

        void BuildOpponents()
        {
            RectTransform grid = UiKit.Rect("Opponents", Root.transform);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(330, 230);
            layout.spacing = new Vector2(15, 15);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(grid, height: 475);

            for (int seat = 1; seat < _table.Seats.Count; seat++)
                _cards.Add(new SeatCard(grid, seat, _table.NameOf(seat)));
        }

        void BuildBanner()
        {
            _banner = UiKit.Picture("Banner", Root.transform, Art.Parchment, Palette.Bone);
            UiKit.Size(_banner, height: 190);
            UiKit.Column(_banner.gameObject, 2, 16, TextAnchor.MiddleCenter);
            _bidWho = UiKit.Label("Who", _banner.transform, "", 32, Palette.Ink);
            UiKit.Size(_bidWho, height: 44);
            _bid = UiKit.Label("Bid", _banner.transform, "", 72, Palette.Blood, style: FontStyle.Bold);
            UiKit.Size(_bid, height: 86);
            _action = UiKit.Label("Action", _banner.transform, "", 30, Palette.Ink, style: FontStyle.Italic);
            UiKit.Size(_action, height: 40);
        }

        void BuildLogAndCoach()
        {
            RectTransform slot = UiKit.Rect("LogSlot", Root.transform);
            UiKit.Size(slot, height: 200);

            Image log = UiKit.Panel("Log", slot, new Color(0, 0, 0, 0.35f));
            UiKit.Stretch(log.rectTransform);
            _log = UiKit.Label("Lines", log.transform, "", 30, Palette.Parchment, TextAnchor.UpperLeft);
            UiKit.Stretch(_log.rectTransform, 20);
            _logPanel = log.gameObject;

            Image coach = UiKit.Picture("Coach", slot, Art.Parchment, Palette.Bone);
            UiKit.Stretch(coach.rectTransform);
            UiKit.Row(coach.gameObject, 16, 16, TextAnchor.MiddleLeft);
            Image portrait = UiKit.Picture("Portrait", coach.transform, Art.Portrait("Quartermaster"), Color.white, sliced: false);
            UiKit.Size(portrait, 130, 130);
            _coachText = UiKit.Label("Text", coach.transform, "", 32, Palette.Ink, TextAnchor.MiddleLeft);
            UiKit.Size(_coachText, flexWidth: 1, height: 170);
            _coachNext = UiKit.Button("Next", coach.transform, "Next", Palette.Kelp, () => ShowIntroPage(_introPage + 1), 36);
            UiKit.Size(_coachNext, 150, 100);
            _coachPanel = coach.gameObject;

            bool tutorial = _table.Tutorial != null;
            _logPanel.SetActive(!tutorial);
            _coachPanel.SetActive(tutorial);
        }

        void BuildMyCup()
        {
            Image panel = UiKit.Picture("MyCup", Root.transform, Art.Plank, Color.white);
            UiKit.Size(panel, height: 250);
            UiKit.Column(panel.gameObject, 10, 18, TextAnchor.MiddleCenter);
            _myTitle = UiKit.Label("Title", panel.transform, "", 36, Palette.Parchment, style: FontStyle.Bold);
            UiKit.Shadowed(_myTitle);
            UiKit.Size(_myTitle, height: 50);
            RectTransform row = UiKit.Rect("Dice", panel.transform);
            UiKit.Row(row.gameObject, 22);
            UiKit.Size(row, height: 140);
            _myDiceRow = row;
        }

        void BuildPicker()
        {
            Image panel = UiKit.Panel("Picker", Root.transform, new Color(0, 0, 0, 0.35f));
            UiKit.Size(panel, height: 280);
            UiKit.Column(panel.gameObject, 14, 14, TextAnchor.MiddleCenter);

            RectTransform qtyRow = UiKit.Rect("Quantity", panel.transform);
            UiKit.Row(qtyRow.gameObject, 24);
            UiKit.Size(qtyRow, height: 106);
            _minus = UiKit.Button("Minus", qtyRow, "-", Palette.Wood, () => { _qty--; ClampPicker(); RefreshPicker(); AudioDirector.Play(Sfx.Click); }, 64);
            UiKit.Size(_minus, 120, 106);
            _quantity = UiKit.Label("Qty", qtyRow, "", 50, Palette.Bone, style: FontStyle.Bold);
            UiKit.Shadowed(_quantity);
            UiKit.Size(_quantity, 460, 106);
            _plus = UiKit.Button("Plus", qtyRow, "+", Palette.Wood, () => { _qty++; ClampPicker(); RefreshPicker(); AudioDirector.Play(Sfx.Click); }, 64);
            UiKit.Size(_plus, 120, 106);

            RectTransform faceRow = UiKit.Rect("Faces", panel.transform);
            UiKit.Row(faceRow.gameObject, 22);
            UiKit.Size(faceRow, height: 130);
            for (int i = 0; i < 6; i++)
            {
                int face = i + 1;
                Button b = UiKit.Button("Face" + face, faceRow, "", Palette.WoodDark, () => { SelectFace(face); AudioDirector.Play(Sfx.Click); });
                UiKit.Size(b, 130, 130);
                var die = new DieView(b.transform, 100);
                RectTransform rt = die.Body.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(100, 100);
                die.Body.raycastTarget = false;
                die.SetFace(face);
                _faceButtons[i] = b;
                _faceDice[i] = die;
            }
        }

        void BuildActions()
        {
            RectTransform row = UiKit.Rect("Actions", Root.transform);
            UiKit.Row(row.gameObject, 24);
            UiKit.Size(row, height: 140);
            _bidButton = UiKit.Button("Bid", row, "Bid", Palette.Gold, () => Submit(SeatDecision.Raise(new Bid(_qty, _face))), 50);
            UiKit.Size(_bidButton, 320, 136);
            _liarButton = UiKit.Button("Liar", row, "Liar!", Palette.Blood, () => Submit(SeatDecision.Liar()), 52);
            UiKit.Size(_liarButton, 320, 136);
            _spotButton = UiKit.Button("SpotOn", row, "Spot on!", Palette.Kelp, () => Submit(SeatDecision.SpotOn()), 50);
            UiKit.Size(_spotButton, 320, 136);
            foreach (Button b in new[] { _bidButton, _liarButton, _spotButton })
                b.gameObject.AddComponent<Pulse>().enabled = false;

            _hint = UiKit.Label("Hint", Root.transform, "", 30, Palette.Parchment);
            UiKit.Shadowed(_hint);
            UiKit.Size(_hint, height: 46);
        }

        void BuildOverlay()
        {
            Image shade = UiKit.Panel("Reveal", Root.transform, Palette.Shade, rounded: false);
            UiKit.Size(shade).ignoreLayout = true;
            UiKit.Stretch(shade.rectTransform);
            _overlay = shade.gameObject;
            _overlayGroup = _overlay.AddComponent<CanvasGroup>();

            Image card = UiKit.Picture("Card", shade.transform, Art.Parchment, Palette.Bone);
            RectTransform rt = card.rectTransform;
            rt.anchorMin = new Vector2(0.04f, 0.1f);
            rt.anchorMax = new Vector2(0.96f, 0.9f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            UiKit.Column(card.gameObject, 16, 40, TextAnchor.UpperCenter);

            _overlayTitle = UiKit.Label("Title", card.transform, "", 92, Palette.Blood, style: FontStyle.Bold);
            UiKit.Size(_overlayTitle, height: 120);
            _overlayText = UiKit.Label("Text", card.transform, "", 38, Palette.Ink);
            UiKit.Size(_overlayText, height: 160);

            RectTransform rows = UiKit.Rect("Seats", card.transform);
            UiKit.Column(rows.gameObject, 12);
            UiKit.Size(rows, flexHeight: 1);
            _overlayRows = rows;

            _overlayCoach = UiKit.Label("Coach", card.transform, "", 34, Palette.Kelp, style: FontStyle.Italic);
            UiKit.Size(_overlayCoach, height: 130);

            RectTransform buttons = UiKit.Rect("Buttons", card.transform);
            UiKit.Row(buttons.gameObject, 30);
            UiKit.Size(buttons, height: 150);
            _overlayButtons = buttons.gameObject.AddComponent<CanvasGroup>();
            _overlayPrimary = UiKit.Button("Primary", buttons, "", Palette.Blood, null, 48);
            UiKit.Size(_overlayPrimary, 460, 140);
            _overlaySecondary = UiKit.Button("Secondary", buttons, _table.IsOnline ? "Leave" : "Menu", Palette.Wood, () => _onMenu(), 48);
            UiKit.Size(_overlaySecondary, 300, 140);

            _overlay.SetActive(false);
        }

        // ---------- events ----------

        void OnRoundStarted()
        {
            if (_table.Engine == null) return;
            // Online the host deals by itself, so a new round clears the last reveal.
            if (_table.IsOnline && _overlay.activeSelf)
            {
                Tween.Stop(_revealRoutine);
                _revealRoutine = null;
                _overlay.SetActive(false);
            }
            Tween.Stop(_rollRoutine);
            _rollRoutine = Tween.Run(RollMyDice());
            foreach (SeatCard card in _cards) card.ShakeCup();
        }

        IEnumerator RollMyDice()
        {
            _rolling = true;
            RefreshMyCup(_table.Engine);
            AudioDirector.Play(Sfx.Rattle);
            foreach (DieView die in _myDice)
                if (die.Body.gameObject.activeSelf) Tween.Shake(die.Body.rectTransform, 14f, 0.6f);

            for (float t = 0; t < 0.65f; t += 0.07f)
            {
                foreach (DieView die in _myDice) die.SetFace(UnityEngine.Random.Range(1, 7));
                yield return new WaitForSeconds(0.07f);
            }

            _rolling = false;
            _rollRoutine = null;
            if (!Root) yield break;
            AudioDirector.Play(Sfx.Thud, 0.8f);
            RefreshMyCup(_table.Engine);
            foreach (DieView die in _myDice)
                if (die.Body.gameObject.activeSelf) Tween.Punch(die.Body.transform, 0.1f, 0.2f);
        }

        void OnBidPlaced(int seat, Bid bid)
        {
            AudioDirector.Play(Sfx.Bid, 0.8f);
            Tween.Punch(_banner.transform, 0.06f, 0.3f);
            if (seat > 0 && seat - 1 < _cards.Count) _cards[seat - 1].Bump();
        }

        void ToggleSound()
        {
            AudioDirector audio = AudioDirector.Instance;
            if (audio == null) return;
            bool on = !(audio.MusicOn || audio.SfxOn);
            audio.MusicOn = on;
            audio.SfxOn = on;
            RefreshSoundButton();
        }

        void RefreshSoundButton()
        {
            AudioDirector audio = AudioDirector.Instance;
            bool on = audio != null && (audio.MusicOn || audio.SfxOn);
            UiKit.SetButtonText(_soundButton, on ? "Sound: On" : "Sound: Off");
        }

        // ---------- tutorial coach ----------

        void ShowIntroPage(int page)
        {
            TutorialScript script = _table.Tutorial;
            if (script == null) return;
            _introPage = page;
            if (page < script.Intro.Length)
            {
                _table.TutorialPaused = true;
                _coachText.text = script.Intro[page];
                _coachNext.gameObject.SetActive(true);
                Tween.Punch(_coachPanel.transform, 0.04f, 0.25f);
            }
            else
            {
                _table.TutorialPaused = false;
                _coachNext.gameObject.SetActive(false);
                if (_table.LocalPlayer.IsWaiting) OnMyTurn();
            }
            Refresh();
        }

        void RefreshCoach()
        {
            if (_table.Tutorial == null || _table.TutorialPaused) return;
            TutorialMove move = _table.CurrentTutorialMove;
            string text = move?.Coach ?? "";
            if (_coachText.text != text)
            {
                _coachText.text = text;
                if (text.Length > 0) Tween.Punch(_coachPanel.transform, 0.04f, 0.25f);
            }
        }

        // ---------- refreshing ----------

        void Refresh()
        {
            GameEngine engine = _table.Engine;
            if (engine == null) return;

            string rules = engine.OnesWild ? "1s are wild" : "1s are NOT wild";
            _round.text = $"Round {engine.RoundNumber}  ·  {engine.TotalDice} dice  ·  {rules}";

            foreach (SeatCard card in _cards) card.Refresh(_table);

            if (engine.CurrentBid != null)
            {
                _bidWho.text = $"{_table.NameOf(engine.CurrentBidder)} bids";
                _bid.text = $"{engine.CurrentBid.Value}";
            }
            else
            {
                _bidWho.text = "";
                _bid.text = "No bid yet";
            }

            bool myTurn = engine.Phase == TablePhase.Bidding && engine.CurrentSeat == TableController.LocalSeat;
            _action.text = engine.Phase == TablePhase.Bidding
                ? myTurn ? "Your turn, captain." : $"{_table.NameOf(engine.CurrentSeat)} is thinking..."
                : "";

            if (_table.LastAction != _lastLogged && _table.LastAction != null)
            {
                _lastLogged = _table.LastAction;
                _logLines.Add(_table.LastAction);
                while (_logLines.Count > LogLines) _logLines.RemoveAt(0);
                _log.text = string.Join("\n", _logLines);
            }

            RefreshCoach();
            RefreshMyCup(engine);
            RefreshPicker();
        }

        void RefreshMyCup(GameEngine engine)
        {
            SeatState me = engine.Seats[TableController.LocalSeat];
            _myTitle.text = me.IsOut
                ? "You walked the plank. Watch the crew finish the game."
                : $"Your cup ({me.DiceCount} {(me.DiceCount == 1 ? "die" : "dice")})";

            while (_myDice.Count < me.Dice.Count) _myDice.Add(new DieView(_myDiceRow, 130));
            for (int i = 0; i < _myDice.Count; i++)
            {
                bool shown = i < me.Dice.Count;
                _myDice[i].Body.gameObject.SetActive(shown);
                if (!shown || _rolling) continue;
                int d = me.Dice[i];
                bool counts = engine.CurrentBid != null &&
                              (d == engine.CurrentBid.Value.Face || (engine.OnesWild && d == 1 && engine.CurrentBid.Value.Face != 1));
                _myDice[i].SetFace(d, highlight: counts);
            }
        }

        void OnMyTurn()
        {
            PlayerView view = _table.LocalPlayer.CurrentView;
            if (view == null) return;

            TutorialMove move = _table.CurrentTutorialMove;
            if (move != null && move.Seat == TableController.LocalSeat && move.Decision.Kind == DecisionKind.Bid)
            {
                _face = move.Decision.Bid.Face;
                _qty = move.Decision.Bid.Quantity;
            }
            else
            {
                _face = view.CurrentBid?.Face ?? BestFace(view);
                if (view.MinimumQuantity(_face) == 0) _face = FirstLegalFace(view);
                _qty = Math.Max(1, view.MinimumQuantity(_face));
            }
            Refresh();
        }

        void SelectFace(int face)
        {
            PlayerView view = _table.LocalPlayer.CurrentView;
            _face = face;
            if (view != null && _table.LocalPlayer.IsWaiting)
            {
                int min = view.MinimumQuantity(face);
                if (min > 0 && (_qty < min || !view.IsValidBid(new Bid(_qty, face)))) _qty = min;
            }
            ClampPicker();
            RefreshPicker();
        }

        void ClampPicker()
        {
            int total = _table.Engine?.TotalDice ?? 1;
            _qty = Mathf.Clamp(_qty, 1, Math.Max(1, total));
        }

        void RefreshPicker()
        {
            LocalSeatController me = _table.LocalPlayer;
            bool active = me.IsWaiting && me.CurrentView != null && !_table.TutorialPaused;
            PlayerView view = me.CurrentView;

            var bid = new Bid(_qty, _face);
            bool valid = active && view.IsValidBid(bid);
            _quantity.text = $"{bid}";
            _quantity.color = valid || !active ? Palette.Bone : new Color(1f, 0.6f, 0.5f);

            _minus.interactable = active && _qty > 1;
            _plus.interactable = active && _qty < (_table.Engine?.TotalDice ?? 0);
            for (int i = 0; i < 6; i++)
            {
                int face = i + 1;
                bool legal = active && view.MinimumQuantity(face) > 0;
                _faceButtons[i].interactable = legal;
                _faceButtons[i].image.color = face == _face ? Palette.Gold : Palette.WoodDark;
                _faceDice[i].SetFace(face, dim: !legal);
            }

            _bidButton.interactable = valid;
            UiKit.SetButtonText(_bidButton, valid ? $"Bid {bid}" : "Bid");
            _liarButton.interactable = active && view.CanCallLiar;
            _spotButton.interactable = active && view.CanCallSpotOn;
            _spotButton.gameObject.SetActive(_table.Engine == null || _table.Engine.Rules.SpotOn);

            // In the tutorial, pulse the one button the coach wants pressed.
            TutorialMove move = _table.CurrentTutorialMove;
            bool coached = active && move != null && move.Seat == TableController.LocalSeat;
            _bidButton.GetComponent<Pulse>().enabled = coached && move.Decision.Kind == DecisionKind.Bid;
            _liarButton.GetComponent<Pulse>().enabled = coached && move.Decision.Kind == DecisionKind.Liar;
            _spotButton.GetComponent<Pulse>().enabled = coached && move.Decision.Kind == DecisionKind.SpotOn;

            if (!active) _hint.text = "";
            else if (coached) _hint.text = "Follow the coach's advice for this lesson.";
            else if (view.CurrentBid == null) _hint.text = view.OnesWild ? "Open the bidding. You can't open on 1s while they're wild." : "Open the bidding.";
            else if (!valid) _hint.text = $"Raise the quantity, or pick a higher face. Lowest for {_face}s: {Math.Max(view.MinimumQuantity(_face), 0)}.";
            else _hint.text = "Raise, call Liar!, or call Spot on!";
        }

        void Submit(SeatDecision decision)
        {
            if (_table.LocalPlayer.Submit(decision))
            {
                RefreshPicker();
                return;
            }
            if (_table.Tutorial != null && _table.LocalPlayer.IsWaiting)
            {
                Tween.Shake((RectTransform)_coachPanel.transform, 3f, 0.35f);
                AudioDirector.Play(Sfx.Click, 1f, 0.6f);
            }
        }

        static int BestFace(PlayerView view)
        {
            int best = view.OnesWild ? 2 : 1, bestCount = -1;
            for (int face = 1; face <= 6; face++)
            {
                if (view.MinimumQuantity(face) == 0) continue;
                int count = BidRules.CountMatches(view.MyDice, face, view.OnesWild);
                if (count >= bestCount) { best = face; bestCount = count; }
            }
            return best;
        }

        static int FirstLegalFace(PlayerView view)
        {
            for (int face = 6; face >= 1; face--)
                if (view.MinimumQuantity(face) > 0) return face;
            return 6;
        }

        // ---------- reveal ----------

        void ShowReveal(RoundResult result)
        {
            Tween.Stop(_revealRoutine);
            _revealRoutine = Tween.Run(RevealSequence(result));
        }

        IEnumerator RevealSequence(RoundResult result)
        {
            bool gameOver = result.Winner >= 0;
            bool tutorialDone = _table.TutorialFinished;

            _overlay.SetActive(true);
            _overlay.transform.SetAsLastSibling();
            _overlayGroup.alpha = 0;
            _overlayButtons.alpha = 0;
            _overlayButtons.interactable = false;
            Tween.Fade(_overlayGroup, 1, 0.2f);

            _overlayTitle.text = result.Kind == ChallengeKind.Liar ? "Liar!" : "Spot on!";
            _overlayTitle.color = Palette.Blood;
            _overlayText.text = "";
            _overlayCoach.text = "";
            Tween.PopIn(_overlayTitle.transform, 0.35f);
            AudioDirector.Play(result.Kind == ChallengeKind.Liar ? Sfx.Liar : Sfx.SpotOn);

            // Lift the cups one seat at a time.
            UiKit.Clear(_overlayRows);
            yield return new WaitForSeconds(0.45f);
            var rowDice = new Dictionary<int, List<DieView>>();
            Transform affectedRow = null;
            for (int seat = 0; seat < result.RevealedDice.Length; seat++)
            {
                int[] dice = result.RevealedDice[seat];
                if (dice.Length == 0) continue;
                RectTransform row = UiKit.Rect("Seat", _overlayRows);
                UiKit.Row(row.gameObject, 12, 0, TextAnchor.MiddleLeft);
                UiKit.Size(row, height: 84);

                Image portrait = UiKit.Picture("Portrait", row, seat == TableController.LocalSeat ? Art.Portrait("You") : Art.Portrait(_table.NameOf(seat)), Color.white, sliced: false);
                UiKit.Size(portrait, 72, 72);
                Color nameColor = seat == result.AffectedSeat ? Palette.Blood : Palette.Ink;
                UiKit.Size(UiKit.Label("Name", row, _table.NameOf(seat), 32, nameColor, TextAnchor.MiddleLeft, FontStyle.Bold), 270, 84);

                var views = new List<DieView>();
                foreach (int d in dice)
                {
                    bool counts = d == result.Bid.Face || (result.OnesWereWild && d == 1 && result.Bid.Face != 1);
                    var die = new DieView(row, 70);
                    die.SetFace(d, highlight: counts, dim: !counts);
                    views.Add(die);
                }
                rowDice[seat] = views;
                if (seat == result.AffectedSeat) affectedRow = row;

                Tween.PopIn(row, 0.25f);
                AudioDirector.Play(Sfx.Thud, 0.6f);
                yield return new WaitForSeconds(0.28f);
            }

            yield return new WaitForSeconds(0.25f);
            _overlayText.text = _table.Describe(result);
            Tween.PopIn(_overlayText.transform, 0.25f);
            yield return new WaitForSeconds(0.6f);

            // The consequence: a die goes overboard, or one comes back.
            if (result.DiceChange < 0 && rowDice.TryGetValue(result.AffectedSeat, out List<DieView> lost) && lost.Count > 0)
            {
                Tween.Overboard(lost[lost.Count - 1].Body);
                AudioDirector.Play(Sfx.Splash);
                yield return new WaitForSeconds(0.8f);
            }
            else if (result.DiceChange > 0 && affectedRow != null)
            {
                var gained = new DieView(affectedRow, 70);
                gained.SetFace(0, highlight: true);
                Tween.PopIn(gained.Body.transform, 0.35f);
                AudioDirector.Play(Sfx.Coin);
                yield return new WaitForSeconds(0.5f);
            }

            if (gameOver)
            {
                bool iWon = result.Winner == TableController.LocalSeat;
                _overlayTitle.text = iWon ? "The treasure is yours!" : $"{_table.NameOf(result.Winner)} wins!";
                _overlayTitle.color = iWon ? Palette.Kelp : Palette.Blood;
                Tween.PopIn(_overlayTitle.transform, 0.4f);
                AudioDirector.Play(iWon ? Sfx.Win : Sfx.Lose);
                if (iWon) Tween.CoinShower((RectTransform)_overlay.transform);
            }

            TutorialRound tutorialRound = _table.CurrentTutorialRound;
            if (tutorialRound != null)
            {
                _overlayCoach.text = tutorialDone ? $"{tutorialRound.RevealCoach}\n{_table.Tutorial.Outro}" : tutorialRound.RevealCoach;
                Tween.PopIn(_overlayCoach.transform, 0.3f);
            }

            _overlayPrimary.onClick.RemoveAllListeners();
            _overlayPrimary.interactable = true;
            if (_table.IsOnline)
            {
                // The host deals the next round itself, and only the host can start a new game.
                bool canRestart = gameOver && !_table.IsMirror;
                UiKit.SetButtonText(_overlayPrimary, canRestart ? "Sail again" : gameOver ? "Waiting for the captain" : "Next round soon...");
                _overlayPrimary.interactable = canRestart;
                if (canRestart) _overlayPrimary.onClick.AddListener(() => _onPlayAgain());
            }
            else if (gameOver || tutorialDone)
            {
                UiKit.SetButtonText(_overlayPrimary, tutorialDone ? "Play a real game" : "Play again");
                _overlayPrimary.onClick.AddListener(() => _onPlayAgain());
                if (tutorialDone) Tween.CoinShower((RectTransform)_overlay.transform, 20);
            }
            else
            {
                UiKit.SetButtonText(_overlayPrimary, "Next round");
                _overlayPrimary.onClick.AddListener(() =>
                {
                    Tween.Stop(_revealRoutine);
                    _overlay.SetActive(false);
                    _table.NextRound();
                });
            }

            Tween.Fade(_overlayButtons, 1, 0.25f);
            _overlayButtons.interactable = true;
            _revealRoutine = null;
        }

        /// <summary>One opponent's place at the table: portrait, name, cup with dice count, and their last move.</summary>
        sealed class SeatCard
        {
            readonly int _seat;
            readonly Image _bg, _portrait, _skull, _cup;
            readonly Text _name, _count, _status;
            readonly Pulse _pulse;

            public SeatCard(Transform parent, int seat, string name)
            {
                _seat = seat;
                _bg = UiKit.Picture("Seat" + seat, parent, Art.Parchment, Palette.Bone);
                UiKit.Column(_bg.gameObject, 6, 12, TextAnchor.MiddleCenter);
                _pulse = _bg.gameObject.AddComponent<Pulse>();
                _pulse.enabled = false;

                RectTransform top = UiKit.Rect("Top", _bg.transform);
                UiKit.Row(top.gameObject, 10, 0, TextAnchor.MiddleLeft);
                UiKit.Size(top, height: 76);
                _portrait = UiKit.Picture("Portrait", top, Art.Portrait(name), Color.white, sliced: false);
                UiKit.Size(_portrait, 74, 74);
                _skull = UiKit.Picture("Skull", _portrait.transform, Art.Skull, Color.white, sliced: false);
                UiKit.Stretch(_skull.rectTransform, 6);
                _skull.gameObject.SetActive(false);
                _name = UiKit.Label("Name", top, name, 30, Palette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Size(_name, flexWidth: 1, height: 76);

                RectTransform mid = UiKit.Rect("Cup", _bg.transform);
                UiKit.Row(mid.gameObject, 12, 0, TextAnchor.MiddleCenter);
                UiKit.Size(mid, height: 74);
                _cup = UiKit.Picture("CupImage", mid, Art.Cup, Color.white, sliced: false);
                UiKit.Size(_cup, 62, 72);
                _count = UiKit.Label("Count", mid, "", 32, Palette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Size(_count, 150, 74);

                _status = UiKit.Label("Status", _bg.transform, "", 28, Palette.Ink, style: FontStyle.Italic);
                UiKit.Size(_status, height: 40);
            }

            public void ShakeCup() => Tween.Shake(_cup.rectTransform, 16f, 0.6f);

            public void Bump() => Tween.Punch(_bg.transform, 0.08f, 0.3f);

            public void Refresh(TableController table)
            {
                GameEngine engine = table.Engine;
                SeatState seat = engine.Seats[_seat];
                _name.text = table.NameOf(_seat);
                _count.text = seat.IsOut ? "" : $"{seat.DiceCount} {(seat.DiceCount == 1 ? "die" : "dice")}";
                _cup.color = seat.IsOut ? new Color(1, 1, 1, 0.3f) : Color.white;
                _skull.gameObject.SetActive(seat.IsOut);

                bool turn = engine.Phase == TablePhase.Bidding && engine.CurrentSeat == _seat;
                _pulse.enabled = turn;
                if (seat.IsOut)
                {
                    _bg.color = new Color(0.6f, 0.55f, 0.5f);
                    _status.text = "Walked the plank";
                }
                else
                {
                    _bg.color = turn ? new Color(1f, 0.86f, 0.5f) : Palette.Bone;
                    _status.text = turn ? "Thinking..." : table.SeatLastActions[_seat] ?? "";
                }
            }
        }
    }
}
