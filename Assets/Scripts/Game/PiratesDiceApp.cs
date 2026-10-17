using PiratesDice.Game.UI;
using UnityEngine;

namespace PiratesDice.Game
{
    /// <summary>
    /// Entry point. Builds the camera, canvas and screens from code and switches between the menu and the table.
    /// It starts itself in any scene, so pressing Play in an empty scene runs the game.
    /// </summary>
    public sealed class PiratesDiceApp : MonoBehaviour
    {
        Transform _column;
        TableController _table;
        MenuView _menu;
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

            if (Camera.main == null)
            {
                var cam = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.transform.SetParent(transform, false);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Sea;
                cam.orthographic = true;
            }

            Canvas canvas = UiKit.CreateCanvas("UI");
            canvas.transform.SetParent(transform, false);

            Image(UiKit.Stretch(UiKit.Rect("Sea", canvas.transform)), Palette.Sea);

            // A fixed 1080x1920 column; the canvas scaler fits it to any screen.
            RectTransform column = UiKit.Rect("Column", canvas.transform);
            column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.5f);
            column.sizeDelta = new Vector2(1080, 1920);
            Image(column, new Color(0.10f, 0.16f, 0.20f));
            _column = column;

            _table = gameObject.AddComponent<TableController>();
            ShowMenu();
        }

        static void Image(RectTransform rt, Color color) => rt.gameObject.AddComponent<UnityEngine.UI.Image>().color = color;

        void ShowMenu()
        {
            _table.StopTurns();
            _tableView?.Dispose();
            _tableView = null;
            if (_menu != null) Destroy(_menu.Root);
            _menu = new MenuView(_column, _config, StartGame);
        }

        void StartGame(GameConfig config)
        {
            _config = config;
            if (_menu != null) Destroy(_menu.Root);
            _menu = null;
            _tableView?.Dispose();

            _table.StartGame(config);
            _tableView = new TableView(_column, _table, ShowMenu, () => StartGame(_config));
        }
    }
}
