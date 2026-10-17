using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PiratesDice.Core;
using PiratesDice.Game.Audio;
using PiratesDice.Game.Online;
using PiratesDice.Online;
using UnityEngine;
using UnityEngine.UI;

namespace PiratesDice.Game.UI
{
    /// <summary>
    /// Online play: pick a pirate name, then host a table, join one with its code, or pick one from the list
    /// of open tables. Once aboard it shows the crew and the table code until the captain sets sail.
    /// </summary>
    public sealed class OnlineView
    {
        const int MaxListed = 4;

        readonly OnlineTable _online;
        readonly GameConfig _config;
        readonly Action _onBack;
        public readonly GameObject Root;

        GameObject _joinPanel, _lobbyPanel;
        InputField _name, _code;
        Text _status, _tablesEmpty;
        Transform _tablesList;
        readonly List<Button> _busyButtons = new List<Button>();
        bool _busy;

        Text _lobbyCode, _lobbyInfo, _lobbyWaiting;
        Transform _crewList;
        GameObject _sizeRow;
        Text _sizeLabel;
        Button _setSail;

        public OnlineView(Transform column, OnlineTable online, GameConfig config, Action onBack)
        {
            _online = online;
            _config = config;
            _onBack = onBack;

            Root = UiKit.Rect("Online", column).gameObject;
            UiKit.Stretch((RectTransform)Root.transform);
            UiKit.Column(Root, 24, 60, TextAnchor.UpperCenter);

            Text title = UiKit.Label("Title", Root.transform, "Sail online", 100, Palette.Gold, style: FontStyle.Bold);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.WoodDark;
            outline.effectDistance = new Vector2(3, -3);
            UiKit.Shadowed(title);
            UiKit.Size(title, height: 150);

            _status = UiKit.Label("Status", Root.transform, "", 34, Palette.Parchment, style: FontStyle.Italic);
            UiKit.Shadowed(_status);
            UiKit.Size(_status, 960, 100);

            BuildJoinPanel();
            BuildLobbyPanel();

            _online.LobbyChanged += RefreshLobby;
            ShowStage();
            _ = RefreshTables();
        }

        public void Dispose()
        {
            _online.LobbyChanged -= RefreshLobby;
            UnityEngine.Object.Destroy(Root);
        }

        // ---------- building ----------

        void BuildJoinPanel()
        {
            _joinPanel = Panel("Join");

            Section(_joinPanel.transform, "Your pirate name");
            _name = UiKit.Input("Name", _joinPanel.transform, "Your pirate name", OnlineTable.SavedName, Protocol.MaxNameLength, 48);
            UiKit.Size(_name, 760, 120);

            Spacer(_joinPanel.transform, 10);
            Button host = BusyButton(_joinPanel.transform, "Host a table", Palette.Blood, () => _ = Host(), 54);
            UiKit.Size(host, 640, 140);
            host.gameObject.AddComponent<Pulse>().Amount = 0.02f;

            Section(_joinPanel.transform, "Join a friend's table");
            RectTransform row = Row(_joinPanel.transform, 120);
            _code = UiKit.Input("Code", row, "Table code", "", 12, 48);
            _code.characterValidation = InputField.CharacterValidation.Alphanumeric;
            UiKit.Size(_code, 480, 120);
            UiKit.Size(BusyButton(row, "Join", Palette.Kelp, () => _ = JoinByCode(), 48), 240, 120);

            RectTransform listHeader = Row(_joinPanel.transform, 90);
            Text openLabel = UiKit.Label("Open", listHeader, "Open tables", 38, Palette.Parchment, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Shadowed(openLabel);
            UiKit.Size(openLabel, 500, 90);
            UiKit.Size(BusyButton(listHeader, "Refresh", Palette.Wood, () => _ = RefreshTables(), 36), 240, 90);

            RectTransform list = UiKit.Rect("Tables", _joinPanel.transform);
            UiKit.Column(list.gameObject, 12);
            UiKit.Size(list, 860, 4 * 110 + 3 * 12);
            _tablesList = list;
            _tablesEmpty = UiKit.Label("Empty", list, "", 32, Palette.Parchment, style: FontStyle.Italic);
            UiKit.Shadowed(_tablesEmpty);
            UiKit.Size(_tablesEmpty, 860, 110);

            Spacer(_joinPanel.transform, 10);
            UiKit.Size(UiKit.Button("Back", _joinPanel.transform, "Back", Palette.Wood, () => { AudioDirector.Play(Sfx.Click); _onBack(); }, 44), 360, 110);
        }

        void BuildLobbyPanel()
        {
            _lobbyPanel = Panel("Lobby");

            Section(_lobbyPanel.transform, "Table code");
            _lobbyCode = UiKit.Label("Code", _lobbyPanel.transform, "", 110, Palette.Gold, style: FontStyle.Bold);
            var outline = _lobbyCode.gameObject.AddComponent<Outline>();
            outline.effectColor = Palette.WoodDark;
            outline.effectDistance = new Vector2(3, -3);
            UiKit.Size(_lobbyCode, 900, 140);
            _lobbyInfo = UiKit.Label("Info", _lobbyPanel.transform, "", 32, Palette.Parchment);
            UiKit.Shadowed(_lobbyInfo);
            UiKit.Size(_lobbyInfo, 900, 90);

            Image crewBg = UiKit.Picture("Crew", _lobbyPanel.transform, Art.Parchment, Palette.Bone);
            UiKit.Size(crewBg, 860, 6 * 92 + 40);
            UiKit.Column(crewBg.gameObject, 8, 20, TextAnchor.UpperLeft);
            _crewList = crewBg.transform;

            RectTransform size = Row(_lobbyPanel.transform, 120);
            _sizeRow = size.gameObject;
            UiKit.Size(UiKit.Button("Fewer", size, "-", Palette.Wood, () => { AudioDirector.Play(Sfx.Click); _online.ChangeTableSize(-1); }, 72), 120, 120);
            _sizeLabel = UiKit.Label("Size", size, "", 40, Palette.Bone);
            UiKit.Shadowed(_sizeLabel);
            UiKit.Size(_sizeLabel, 560, 120);
            UiKit.Size(UiKit.Button("More", size, "+", Palette.Wood, () => { AudioDirector.Play(Sfx.Click); _online.ChangeTableSize(1); }, 72), 120, 120);

            _setSail = UiKit.Button("SetSail", _lobbyPanel.transform, "Set sail!", Palette.Blood, () => { AudioDirector.Play(Sfx.Coin); _online.SetSail(); }, 60);
            UiKit.Size(_setSail, 600, 150);
            _setSail.gameObject.AddComponent<Pulse>().Amount = 0.025f;
            _lobbyWaiting = UiKit.Label("Waiting", _lobbyPanel.transform, "Waiting for the captain to set sail...", 40, Palette.Parchment, style: FontStyle.Italic);
            UiKit.Shadowed(_lobbyWaiting);
            UiKit.Size(_lobbyWaiting, 900, 150);

            UiKit.Size(UiKit.Button("Leave", _lobbyPanel.transform, "Leave table", Palette.Wood, () => { AudioDirector.Play(Sfx.Click); _online.Leave(); }, 44), 420, 110);
        }

        GameObject Panel(string name)
        {
            RectTransform panel = UiKit.Rect(name, Root.transform);
            UiKit.Column(panel.gameObject, 20, 0, TextAnchor.UpperCenter);
            UiKit.Size(panel, 960, flexHeight: 1);
            return panel.gameObject;
        }

        static void Section(Transform parent, string text)
        {
            Text label = UiKit.Label("Section", parent, text, 38, Palette.Parchment, style: FontStyle.Bold);
            UiKit.Shadowed(label);
            UiKit.Size(label, 900, 56);
        }

        static RectTransform Row(Transform parent, float height)
        {
            RectTransform row = UiKit.Rect("Row", parent);
            UiKit.Row(row.gameObject, 20);
            UiKit.Size(row, height: height);
            return row;
        }

        static void Spacer(Transform parent, float height) => UiKit.Size(UiKit.Rect("Spacer", parent), height: height);

        Button BusyButton(Transform parent, string text, Color color, Action onClick, int fontSize)
        {
            Button button = UiKit.Button(text, parent, text, color, () =>
            {
                if (_busy) return;
                AudioDirector.Play(Sfx.Click);
                onClick();
            }, fontSize);
            _busyButtons.Add(button);
            return button;
        }

        // ---------- actions ----------

        async Task Host()
        {
            await Busy("Rigging a table...", () => _online.HostAsync(_config, _name.text));
        }

        async Task JoinByCode()
        {
            string code = _code.text.Trim();
            if (code.Length == 0)
            {
                _status.text = "Type the table code your captain shared.";
                return;
            }
            await Busy("Rowing over...", () => _online.JoinByCodeAsync(_config, _name.text, code));
        }

        async Task JoinById(string id)
        {
            await Busy("Rowing over...", () => _online.JoinByIdAsync(_config, _name.text, id));
        }

        async Task Busy(string message, Func<Task> work)
        {
            if (_busy) return;
            SetBusy(true, message);
            try
            {
                await work();
                if (!Root) return;
                _status.text = "";
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Online: {e}");
                if (!Root) return;
                _status.text = $"Couldn't reach the table: {OnlineTable.Describe(e)}";
            }
            finally
            {
                if (Root) SetBusy(false, null);
            }
            if (Root) ShowStage();
        }

        void SetBusy(bool busy, string message)
        {
            _busy = busy;
            foreach (Button b in _busyButtons) b.interactable = !busy;
            if (message != null) _status.text = message;
        }

        async Task RefreshTables()
        {
            if (_busy) return;
            UiKit.Clear(_tablesList);
            _tablesEmpty = UiKit.Label("Empty", _tablesList, "Looking for open tables...", 32, Palette.Parchment, style: FontStyle.Italic);
            UiKit.Shadowed(_tablesEmpty);
            UiKit.Size(_tablesEmpty, 860, 110);

            List<OpenTable> tables;
            try
            {
                tables = await OnlineSession.ListOpenTablesAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Online: {e}");
                if (Root && _tablesEmpty) _tablesEmpty.text = $"Couldn't look for tables: {OnlineTable.Describe(e)}";
                return;
            }
            if (!Root) return;

            UiKit.Clear(_tablesList);
            if (tables.Count == 0)
            {
                _tablesEmpty = UiKit.Label("Empty", _tablesList, "No open tables right now. Host one!", 32, Palette.Parchment, style: FontStyle.Italic);
                UiKit.Shadowed(_tablesEmpty);
                UiKit.Size(_tablesEmpty, 860, 110);
                return;
            }
            for (int i = 0; i < tables.Count && i < MaxListed; i++)
            {
                OpenTable table = tables[i];
                Image row = UiKit.Picture("Table", _tablesList, Art.Plank, Color.white);
                UiKit.Row(row.gameObject, 16, 12);
                UiKit.Size(row, 860, 110);
                Text name = UiKit.Label("Name", row.transform, $"{table.Name}  ·  {table.Players}/{table.MaxPlayers}", 34, Palette.Bone, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Shadowed(name);
                UiKit.Size(name, 600, 86);
                UiKit.Size(BusyButton(row.transform, "Join", Palette.Kelp, () => _ = JoinById(table.Id), 40), 180, 86);
            }
        }

        // ---------- lobby ----------

        void ShowStage()
        {
            bool aboard = _online.CurrentStage != OnlineTable.Stage.Idle;
            _joinPanel.SetActive(!aboard);
            _lobbyPanel.SetActive(aboard);
            if (aboard) RefreshLobby();
        }

        void RefreshLobby()
        {
            if (!Root) return;
            if (_online.CurrentStage == OnlineTable.Stage.Idle) return;
            if (!_lobbyPanel.activeSelf) ShowStage();

            _lobbyCode.text = _online.Code;
            _lobbyInfo.text = _online.IsHost
                ? "Share this code with your crew. Empty seats get AI pirates."
                : "You're aboard! The captain picks when to set sail.";

            UiKit.Clear(_crewList);
            for (int i = 0; i < _online.CrewNames.Count; i++)
            {
                string name = _online.CrewNames[i];
                RectTransform row = UiKit.Rect("Pirate", _crewList);
                UiKit.Row(row.gameObject, 16, 0, TextAnchor.MiddleLeft);
                UiKit.Size(row, 820, 84);
                Image face = UiKit.Picture("Portrait", row, Art.Portrait(name), Color.white, sliced: false);
                UiKit.Size(face, 80, 80);
                string label = i == 0 ? $"{name}  (captain)" : name;
                if (name == _online.MyName && !_online.IsHost) label += "  (you)";
                UiKit.Size(UiKit.Label("Name", row, label, 38, Palette.Ink, TextAnchor.MiddleLeft, FontStyle.Bold), 700, 84);
            }
            int ai = Math.Max(0, _online.TableSize - _online.CrewNames.Count);
            if (ai > 0)
                UiKit.Size(UiKit.Label("Ai", _crewList, $"+ {ai} AI pirate{(ai == 1 ? "" : "s")}", 34, Palette.Ink, TextAnchor.MiddleLeft, FontStyle.Italic), 820, 70);

            _sizeRow.SetActive(_online.IsHost);
            _sizeLabel.text = $"Table of {_online.TableSize}";
            _setSail.gameObject.SetActive(_online.IsHost);
            _lobbyWaiting.gameObject.SetActive(!_online.IsHost);
        }
    }
}
