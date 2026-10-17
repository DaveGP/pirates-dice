using PiratesDice.Core;
using PiratesDice.Game.Audio;
using PiratesDice.Game.Online;
using PiratesDice.Game.UI;
using UnityEngine;

namespace PiratesDice.Game
{
    /// <summary>
    /// Entry point. Builds the camera, canvas and screens from code and switches between the menu, the online
    /// lobby and the table. It starts itself in any scene, so pressing Play in an empty scene runs the game.
    /// </summary>
    public sealed class PiratesDiceApp : MonoBehaviour
    {
        Transform _column;
        TableController _table;
        OnlineTable _online;
        MenuView _menu;
        OnlineView _onlineView;
        TableView _tableView;
        GameConfig _config = new GameConfig();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<PiratesDiceApp>() != null) return;
            new GameObject("Pirate's Dice").AddComponent<PiratesDiceApp>();
        }

        void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;
            // Keep the connection alive while another window has focus, such as a second test client.
            Application.runInBackground = true;

            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.SetParent(transform, false);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Sea;
                cam.orthographic = true;
            }

            gameObject.AddComponent<AudioDirector>();

            Canvas canvas = UiKit.CreateCanvas("UI");
            canvas.transform.SetParent(transform, false);

            Image(UiKit.Stretch(UiKit.Rect("Sea", canvas.transform)), Palette.Sea);

            // A fixed 1080x1920 column of tavern-table wood; the canvas scaler fits it to any screen.
            RectTransform column = UiKit.Rect("Column", canvas.transform);
            column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(1080, 1920);
            var wood = column.gameObject.AddComponent<UnityEngine.UI.Image>();
            wood.sprite = Art.Wood;
            wood.type = UnityEngine.UI.Image.Type.Tiled;
            wood.pixelsPerUnitMultiplier = 0.5f;
            _column = column;

            _table = gameObject.AddComponent<TableController>();
            _online = new OnlineTable(OnlineSession.Ensure(transform), _table);
            _online.GameStarted += ShowOnlineTable;
            _online.Ended += reason => ShowMenu(reason);
            ShowMenu();
        }

        void OnApplicationQuit() => _online?.Leave();

        static void Image(RectTransform rt, Color color) => rt.gameObject.AddComponent<UnityEngine.UI.Image>().color = color;

        void ClearScreens()
        {
            if (_menu != null) Destroy(_menu.Root);
            _menu = null;
            _onlineView?.Dispose();
            _onlineView = null;
            _tableView?.Dispose();
            _tableView = null;
        }

        void ShowMenu() => ShowMenu(null);

        void ShowMenu(string notice)
        {
            _table.StopTurns();
            ClearScreens();
            _menu = new MenuView(_column, _config, StartGame, StartTutorial, ShowOnline, notice);
        }

        void StartGame(GameConfig config)
        {
            _config = config;
            ClearScreens();
            _table.StartGame(config);
            _tableView = new TableView(_column, _table, ShowMenu, () => StartGame(_config));
        }

        void StartTutorial()
        {
            ClearScreens();
            _table.StartTutorial(TutorialScript.Standard(), _config.PlayerName);
            _tableView = new TableView(_column, _table, ShowMenu, () => StartGame(_config));
        }

        void ShowOnline()
        {
            ClearScreens();
            _onlineView = new OnlineView(_column, _online, _config, ShowMenu);
        }

        void ShowOnlineTable()
        {
            ClearScreens();
            // Leaving goes back to the menu through the online table's Ended event.
            _tableView = new TableView(_column, _table, () => _online.Leave(), () => _online.SetSail());
        }
    }
}
