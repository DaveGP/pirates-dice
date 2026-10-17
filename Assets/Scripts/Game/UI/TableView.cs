using System;
using System.Collections.Generic;
using System.Linq;
using PiratesDice.Core;
using UnityEngine;
using UnityEngine.UI;

namespace PiratesDice.Game.UI
{
    /// <summary>
    /// The table screen: opponents' cups, the standing bid, your dice, and the bid picker. It reads state from
    /// the <see cref="TableController"/> and sends your moves through the local seat; it never decides outcomes.
    /// </summary>
    public sealed class TableView
    {
        const int LogLines = 5;

        readonly TableController _table;
        readonly Action _onMenu;
        readonly Action _onPlayAgain;
        public readonly GameObject Root;

        Text _round, _bid, _action, _log, _myTitle, _quantity, _hint;
        readonly List<SeatCard> _cards = new List<SeatCard>();
        readonly List<DieView> _myDice = new List<DieView>();
        Transform _myDiceRow;
        readonly Button[] _faceButtons = new Button[6];
        readonly DieView[] _faceDice = new DieView[6];
        Button _minus, _plus, _bidButton, _liarButton, _spotButton;
        readonly List<string> _logLines = new List<string>();
        string _lastLogged;

        int _face = 2, _qty = 1;

        GameObject _overlay;
        Text _overlayTitle, _overlayText;
        Transform _overlayRows;
        Button _overlayPrimary, _overlaySecondary;

        public TableView(Transform column, TableController table, Action onMenu, Action onPlayAgain)
        {
            _table = table;
            _onMenu = onMenu;
            _onPlayAgain = onPlayAgain;

            Root = UiKit.Rect("Table", column).gameObject;
            UiKit.Stretch((RectTransform)Root.transform);
            UiKit.Column(Root, 18, 28);

            BuildHeader();
            BuildOpponents();
            BuildBanner();
            BuildLog();
            BuildMyCup();
            BuildPicker();
            BuildActions();
            BuildOverlay();

            _table.Changed += Refresh;
            _table.Revealed += ShowReveal;
            _table.LocalPlayer.TurnStarted += OnMyTurn;
            Refresh();
            if (_table.LocalPlayer.IsWaiting) OnMyTurn();
        }

        public void Dispose()
        {
            _table.Changed -= Refresh;
            _table.Revealed -= ShowReveal;
            _table.LocalPlayer.TurnStarted -= OnMyTurn;
            UnityEngine.Object.Destroy(Root);
        }

        // ---------- building ----------

        void BuildHeader()
        {
            RectTransform header = UiKit.Rect("Header", Root.transform);
            UiKit.Row(header.gameObject, 20);
            UiKit.Size(header, height: 96);
            _round = UiKit.Label("Round", header, "", 40, Palette.Parchment, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Size(_round, flexWidth: 1, height: 96);
            UiKit.Size(UiKit.Button("Menu", header, "Menu", Palette.Wood, () => _onMenu(), 38), 220, 90);
        }

        void BuildOpponents()
        {
            RectTransform grid = UiKit.Rect("Opponents", Root.transform);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(320, 220);
            layout.spacing = new Vector2(20, 20);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            layout.childAlignment = TextAnchor.MiddleCenter;
            UiKit.Size(grid, height: 460);

            for (int seat = 1; seat < _table.Seats.Count; seat++)
                _cards.Add(new SeatCard(grid, seat));
        }

        void BuildBanner()
        {
            Image banner = UiKit.Panel("Banner", Root.transform, Palette.WoodDark);
            UiKit.Size(banner, height: 200);
            UiKit.Column(banner.gameObject, 6, 18, TextAnchor.MiddleCenter);
            _bid = UiKit.Label("Bid", banner.transform, "", 64, Palette.Gold, style: FontStyle.Bold);
            UiKit.Size(_bid, height: 90);
            _action = UiKit.Label("Action", banner.transform, "", 36, Palette.Parchment);
            UiKit.Size(_action, height: 60);
        }

        void BuildLog()
        {
            Image panel = UiKit.Panel("Log", Root.transform, new Color(0, 0, 0, 0.25f));
            UiKit.Size(panel, height: 200);
            _log = UiKit.Label("Lines", panel.transform, "", 32, Palette.Parchment, TextAnchor.UpperLeft);
            UiKit.Stretch(_log.rectTransform, 20);
        }

        void BuildMyCup()
        {
            Image panel = UiKit.Panel("MyCup", Root.transform, Palette.Wood);
            UiKit.Size(panel, height: 250);
            UiKit.Column(panel.gameObject, 10, 18, TextAnchor.MiddleCenter);
            _myTitle = UiKit.Label("Title", panel.transform, "", 38, Palette.Parchment, style: FontStyle.Bold);
            UiKit.Size(_myTitle, height: 50);
            RectTransform row = UiKit.Rect("Dice", panel.transform);
            UiKit.Row(row.gameObject, 22);
            UiKit.Size(row, height: 140);
            _myDiceRow = row;
        }

        void BuildPicker()
        {
            Image panel = UiKit.Panel("Picker", Root.transform, new Color(0, 0, 0, 0.25f));
            UiKit.Size(panel, height: 290);
            UiKit.Column(panel.gameObject, 16, 16, TextAnchor.MiddleCenter);

            RectTransform qtyRow = UiKit.Rect("Quantity", panel.transform);
            UiKit.Row(qtyRow.gameObject, 24);
            UiKit.Size(qtyRow, height: 110);
            _minus = UiKit.Button("Minus", qtyRow, "-", Palette.Wood, () => { _qty--; ClampPicker(); RefreshPicker(); }, 64);
            UiKit.Size(_minus, 120, 110);
            _quantity = UiKit.Label("Qty", qtyRow, "", 48, Palette.Bone, style: FontStyle.Bold);
            UiKit.Size(_quantity, 460, 110);
            _plus = UiKit.Button("Plus", qtyRow, "+", Palette.Wood, () => { _qty++; ClampPicker(); RefreshPicker(); }, 64);
            UiKit.Size(_plus, 120, 110);

            RectTransform faceRow = UiKit.Rect("Faces", panel.transform);
            UiKit.Row(faceRow.gameObject, 22);
            UiKit.Size(faceRow, height: 130);
            for (int i = 0; i < 6; i++)
            {
                int face = i + 1;
                Button b = UiKit.Button("Face" + face, faceRow, "", Palette.WoodDark, () => SelectFace(face));
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
            UiKit.Size(row, height: 150);
            _bidButton = UiKit.Button("Bid", row, "Bid", Palette.Gold, () => Submit(SeatDecision.Raise(new Bid(_qty, _face))), 52);
            UiKit.Size(_bidButton, 320, 140);
            _liarButton = UiKit.Button("Liar", row, "Liar!", Palette.Blood, () => Submit(SeatDecision.Liar()), 52);
            UiKit.Size(_liarButton, 320, 140);
            _spotButton = UiKit.Button("SpotOn", row, "Spot on!", Palette.Kelp, () => Submit(SeatDecision.SpotOn()), 52);
            UiKit.Size(_spotButton, 320, 140);

            _hint = UiKit.Label("Hint", Root.transform, "", 32, Palette.Parchment);
            UiKit.Size(_hint, height: 50);
        }

        void BuildOverlay()
        {
            Image shade = UiKit.Panel("Reveal", Root.transform, Palette.Shade, rounded: false);
            UiKit.Size(shade).ignoreLayout = true;
            UiKit.Stretch(shade.rectTransform);
            _overlay = shade.gameObject;

            Image card = UiKit.Panel("Card", shade.transform, Palette.Parchment);
            RectTransform rt = card.rectTransform;
            rt.anchorMin = new Vector2(0.04f, 0.12f);
            rt.anchorMax = new Vector2(0.96f, 0.88f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            UiKit.Column(card.gameObject, 18, 40, TextAnchor.UpperCenter);

            _overlayTitle = UiKit.Label("Title", card.transform, "", 88, Palette.Blood, style: FontStyle.Bold);
            UiKit.Size(_overlayTitle, height: 120);
            _overlayText = UiKit.Label("Text", card.transform, "", 40, Palette.Ink);
            UiKit.Size(_overlayText, height: 170);

            RectTransform rows = UiKit.Rect("Seats", card.transform);
            UiKit.Column(rows.gameObject, 12);
            UiKit.Size(rows, flexHeight: 1);
            _overlayRows = rows;

            RectTransform buttons = UiKit.Rect("Buttons", card.transform);
            UiKit.Row(buttons.gameObject, 30);
            UiKit.Size(buttons, height: 150);
            _overlayPrimary = UiKit.Button("Primary", buttons, "", Palette.Blood, null, 52);
            UiKit.Size(_overlayPrimary, 420, 140);
            _overlaySecondary = UiKit.Button("Secondary", buttons, "Menu", Palette.Wood, () => _onMenu(), 52);
            UiKit.Size(_overlaySecondary, 320, 140);

            _overlay.SetActive(false);
        }

        // ---------- refreshing ----------

        void Refresh()
        {
            GameEngine engine = _table.Engine;
            if (engine == null) return;

            string rules = engine.OnesWild ? "1s are wild" : "1s are not wild";
            _round.text = $"Round {engine.RoundNumber}  ·  {engine.TotalDice} dice on the table  ·  {rules}";

            foreach (SeatCard card in _cards) card.Refresh(_table);

            if (engine.CurrentBid != null)
            {
                _bid.text = $"{_table.NameOf(engine.CurrentBidder)}: {engine.CurrentBid.Value}";
            }
            else
            {
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
                if (shown)
                {
                    int d = me.Dice[i];
                    bool counts = engine.CurrentBid != null &&
                                  (d == engine.CurrentBid.Value.Face || (engine.OnesWild && d == 1 && engine.CurrentBid.Value.Face != 1));
                    _myDice[i].SetFace(d, highlight: counts);
                }
            }
        }

        void OnMyTurn()
        {
            PlayerView view = _table.LocalPlayer.CurrentView;
            if (view == null) return;
            _face = view.CurrentBid?.Face ?? BestFace(view);
            if (view.MinimumQuantity(_face) == 0) _face = FirstLegalFace(view);
            _qty = Math.Max(1, view.MinimumQuantity(_face));
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
            bool active = me.IsWaiting && me.CurrentView != null;
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

            if (!active) _hint.text = "";
            else if (view.CurrentBid == null) _hint.text = view.OnesWild ? "Open the bidding. You can't open on 1s while they're wild." : "Open the bidding.";
            else if (!valid) _hint.text = $"Raise the quantity, or pick a higher face. Lowest for {_face}s: {Math.Max(view.MinimumQuantity(_face), 0)}.";
            else _hint.text = "Raise, call Liar!, or call Spot on!";
        }

        void Submit(SeatDecision decision)
        {
            if (_table.LocalPlayer.Submit(decision)) RefreshPicker();
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
            _overlay.SetActive(true);
            _overlay.transform.SetAsLastSibling();

            bool gameOver = result.Winner >= 0;
            if (gameOver)
            {
                bool iWon = result.Winner == TableController.LocalSeat;
                _overlayTitle.text = iWon ? "The treasure is yours!" : $"{_table.NameOf(result.Winner)} wins!";
                _overlayTitle.color = iWon ? Palette.Kelp : Palette.Blood;
            }
            else
            {
                _overlayTitle.text = result.Kind == ChallengeKind.Liar ? "Liar!" : "Spot on!";
                _overlayTitle.color = Palette.Blood;
            }
            _overlayText.text = _table.Describe(result);

            UiKit.Clear(_overlayRows);
            for (int seat = 0; seat < result.RevealedDice.Length; seat++)
            {
                int[] dice = result.RevealedDice[seat];
                if (dice.Length == 0) continue;
                RectTransform row = UiKit.Rect("Seat", _overlayRows);
                UiKit.Row(row.gameObject, 12, 0, TextAnchor.MiddleLeft);
                UiKit.Size(row, height: 80);
                Color nameColor = seat == result.AffectedSeat ? Palette.Blood : Palette.Ink;
                UiKit.Size(UiKit.Label("Name", row, _table.NameOf(seat), 34, nameColor, TextAnchor.MiddleLeft, FontStyle.Bold), 330, 80);
                foreach (int d in dice)
                {
                    bool counts = d == result.Bid.Face || (result.OnesWereWild && d == 1 && result.Bid.Face != 1);
                    new DieView(row, 72).SetFace(d, highlight: counts, dim: !counts);
                }
            }

            _overlayPrimary.onClick.RemoveAllListeners();
            if (gameOver)
            {
                UiKit.SetButtonText(_overlayPrimary, "Play again");
                _overlayPrimary.onClick.AddListener(() => _onPlayAgain());
            }
            else
            {
                UiKit.SetButtonText(_overlayPrimary, "Next round");
                _overlayPrimary.onClick.AddListener(() =>
                {
                    _overlay.SetActive(false);
                    _table.NextRound();
                });
            }
        }

        /// <summary>One opponent's place at the table: name, how many dice are under the cup, and their last move.</summary>
        sealed class SeatCard
        {
            readonly int _seat;
            readonly Image _bg;
            readonly Text _name, _status;
            readonly Transform _cup;
            readonly List<Image> _hidden = new List<Image>();

            public SeatCard(Transform parent, int seat)
            {
                _seat = seat;
                _bg = UiKit.Panel("Seat" + seat, parent, Palette.Parchment);
                UiKit.Column(_bg.gameObject, 8, 14, TextAnchor.MiddleCenter);
                _name = UiKit.Label("Name", _bg.transform, "", 34, Palette.Ink, style: FontStyle.Bold);
                UiKit.Size(_name, height: 50);

                RectTransform cup = UiKit.Rect("Cup", _bg.transform);
                UiKit.Row(cup.gameObject, 8);
                UiKit.Size(cup, height: 50);
                _cup = cup;
                for (int i = 0; i < 5; i++)
                {
                    Image die = UiKit.Panel("Hidden", cup, Palette.WoodDark);
                    UiKit.Size(die, 42, 42);
                    _hidden.Add(die);
                }

                _status = UiKit.Label("Status", _bg.transform, "", 30, Palette.Ink);
                UiKit.Size(_status, height: 70);
            }

            public void Refresh(TableController table)
            {
                GameEngine engine = table.Engine;
                SeatState seat = engine.Seats[_seat];
                _name.text = table.NameOf(_seat);

                while (_hidden.Count < seat.DiceCount)
                {
                    Image die = UiKit.Panel("Hidden", _cup, Palette.WoodDark);
                    UiKit.Size(die, 42, 42);
                    _hidden.Add(die);
                }
                for (int i = 0; i < _hidden.Count; i++) _hidden[i].gameObject.SetActive(i < seat.DiceCount);

                bool turn = engine.Phase == TablePhase.Bidding && engine.CurrentSeat == _seat;
                if (seat.IsOut)
                {
                    _bg.color = Palette.Disabled;
                    _status.text = "Walked the plank";
                }
                else
                {
                    _bg.color = turn ? Palette.Gold : Palette.Parchment;
                    _status.text = turn ? "Thinking..." : table.SeatLastActions[_seat] ?? "";
                }
            }
        }
    }
}
