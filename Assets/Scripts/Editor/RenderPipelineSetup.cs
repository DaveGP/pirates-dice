using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PiratesDice.Editor
{
    /// <summary>
    /// Puts the project on the Universal Render Pipeline. The game draws only a flat-coloured camera and a uGUI
    /// overlay, so there are no materials to convert: it needs a URP asset with a Universal Renderer, assigned in
    /// Graphics settings and every Quality level, trimmed of the 3D features the game never uses. Runs once
    /// automatically when the project is still on the Built-in pipeline; rerun from the Pirate's Dice menu any time.
    /// </summary>
    [InitializeOnLoad]
    static class RenderPipelineSetup
    {
        const string Folder = "Assets/Settings";
        const string PipelinePath = Folder + "/PiratesDice_URP.asset";
        const string RendererPath = Folder + "/PiratesDice_Renderer.asset";
        const string DoneKey = "PiratesDice.RenderPipelineSetup.Done";

        static RenderPipelineSetup()
        {
            if (SessionState.GetBool(DoneKey, false)) return;
            SessionState.SetBool(DoneKey, true);
            EditorApplication.delayCall += () =>
            {
                if (!(GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset)) Run();
            };
        }

        [MenuItem("Pirate's Dice/Set Up Render Pipeline")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            UniversalRenderPipelineAsset pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets", "Settings");
                var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
                if (renderer == null)
                {
                    renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                    AssetDatabase.CreateAsset(renderer, RendererPath);
                }
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            Trim(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);

            AssetDatabase.SaveAssets();
            Report(pipeline);
        }

        /// <summary>
        /// Turns off what a flat 2D overlay never draws: shadows, extra lights, HDR, anti-aliasing and the depth and
        /// opaque copies. That keeps the web build small and light on phones.
        /// </summary>
        static void Trim(UniversalRenderPipelineAsset pipeline)
        {
            var asset = new SerializedObject(pipeline);
            Set(asset, "m_SupportsHDR", false);
            Set(asset, "m_MSAA", 1);
            Set(asset, "m_RequireDepthTexture", false);
            Set(asset, "m_RequireOpaqueTexture", false);
            Set(asset, "m_MainLightShadowsSupported", false);
            Set(asset, "m_AdditionalLightShadowsSupported", false);
            Set(asset, "m_AdditionalLightsRenderingMode", 0); // Disabled
            asset.ApplyModifiedPropertiesWithoutUndo();

            ScriptableRendererData data = pipeline.rendererDataList.Length > 0 ? pipeline.rendererDataList[0] : null;
            if (data != null)
            {
                var renderer = new SerializedObject(data);
                Set(renderer, "m_RenderingMode", 0); // Forward: the simplest path, and fine for WebGL
                renderer.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(data);
            }
            EditorUtility.SetDirty(pipeline);
        }

        static void Set(SerializedObject target, string field, bool value)
        {
            SerializedProperty p = target.FindProperty(field);
            if (p != null) p.boolValue = value;
            else Debug.LogWarning($"Pirate's Dice: {target.targetObject.name} has no '{field}' setting; left as it was.");
        }

        static void Set(SerializedObject target, string field, int value)
        {
            SerializedProperty p = target.FindProperty(field);
            if (p != null) p.intValue = value;
            else Debug.LogWarning($"Pirate's Dice: {target.targetObject.name} has no '{field}' setting; left as it was.");
        }

        static void Report(UniversalRenderPipelineAsset pipeline)
        {
            var levels = new System.Text.StringBuilder();
            bool allSet = GraphicsSettings.defaultRenderPipeline == pipeline;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                RenderPipelineAsset assigned = QualitySettings.GetRenderPipelineAssetAt(i);
                allSet &= assigned == pipeline;
                levels.Append($"\n  {QualitySettings.names[i]} = {(assigned == null ? "none" : assigned.name)}");
            }
            bool hasRenderer = pipeline.rendererDataList.Length > 0 && pipeline.rendererDataList[0] != null;

            string summary = $"Pirate's Dice render pipeline: Graphics = {GraphicsSettings.defaultRenderPipeline?.name ?? "Built-in"}, "
                + $"renderer = {(hasRenderer ? pipeline.rendererDataList[0].name : "missing")}{levels}";
            if (allSet && hasRenderer) Debug.Log(summary);
            else Debug.LogError(summary + "\nSomething wasn't assigned: run Pirate's Dice > Set Up Render Pipeline again.");
        }
    }
}
