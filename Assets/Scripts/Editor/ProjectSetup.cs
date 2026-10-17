using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PiratesDice.Editor
{
    /// <summary>
    /// First-open setup: creates the main scene, puts it in Build Settings, and sets the player settings for
    /// a portrait mobile and web game. Runs once automatically; rerun from the Pirate's Dice menu any time.
    /// </summary>
    [InitializeOnLoad]
    static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string DoneKey = "PiratesDice.ProjectSetup.Done";

        static ProjectSetup()
        {
            if (SessionState.GetBool(DoneKey, false)) return;
            SessionState.SetBool(DoneKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(ScenePath) || !EditorBuildSettings.scenes.Any(s => s.path == ScenePath))
                    Run();
            };
        }

        [MenuItem("Pirate's Dice/Set Up Project")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            if (!File.Exists(ScenePath))
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                cam.tag = "MainCamera";
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.11f, 0.16f);
                cam.orthographic = true;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            PlayerSettings.productName = "Pirate's Dice";
            PlayerSettings.companyName = "DaveGP";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.davegp.piratesdice");
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.davegp.piratesdice");

            AssetDatabase.SaveAssets();
            Debug.Log("Pirate's Dice: main scene and player settings are ready. Press Play to start.");
        }
    }
}
