using System.IO;
using System.IO.Compression;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PiratesDice.Editor
{
    /// <summary>
    /// One-click web release: applies the web player settings, builds, and zips the result ready to attach to a
    /// GitHub release, which publishes it to GitHub Pages.
    /// </summary>
    static class WebBuild
    {
        const string OutputFolder = "Builds/Web";
        const string ZipPath = "Builds/pirates-dice-web.zip";

        [MenuItem("Pirate's Dice/Apply Web Settings")]
        public static void ApplySettings()
        {
            var target = NamedBuildTarget.WebGL;

            // GitHub Pages can't send the Content-Encoding header Unity's compressed files need, so the build
            // carries its own small decompressor. Gzip unpacks quickly that way; Brotli would be slow.
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:PiratesDice";

            // The game catches errors from the online services and garbled messages, so thrown exceptions stay.
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
            PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric;

            PlayerSettings.stripEngineCode = true;
            PlayerSettings.stripUnusedMeshComponents = true;
            // The online services and Netcode find parts of themselves by reflection, which heavier stripping
            // can remove. The game is small, so the size saved isn't worth that risk.
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.Minimal);
            PlayerSettings.SetIl2CppCodeGeneration(target, Il2CppCodeGeneration.OptimizeSize);

            AssetDatabase.SaveAssets();
            Debug.Log("Pirate's Dice web settings saved:\n"
                + $"  compression = {PlayerSettings.WebGL.compressionFormat}, decompression fallback = {PlayerSettings.WebGL.decompressionFallback}\n"
                + $"  template = {PlayerSettings.WebGL.template}, data caching = {PlayerSettings.WebGL.dataCaching}\n"
                + $"  exceptions = {PlayerSettings.WebGL.exceptionSupport}, debug symbols = {PlayerSettings.WebGL.debugSymbolMode}\n"
                + $"  strip engine code = {PlayerSettings.stripEngineCode}, managed stripping = {PlayerSettings.GetManagedStrippingLevel(target)}\n"
                + $"  IL2CPP code generation = {PlayerSettings.GetIl2CppCodeGeneration(target)}");
        }

        [MenuItem("Pirate's Dice/Build for Web")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Stop Play mode before building.");
                return;
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                EditorUtility.DisplayDialog("Web build support is missing",
                    "Add the Web Build Support module to this Unity version in Unity Hub (Installs > Add modules), then try again.", "OK");
                return;
            }

            ProjectSetup.Run();
            ApplySettings();

            if (Directory.Exists(OutputFolder)) Directory.Delete(OutputFolder, true);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = OutputFolder,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Web build {report.summary.result}: see the errors above.");
                return;
            }

            Zip(OutputFolder, ZipPath);
            long bytes = new FileInfo(ZipPath).Length;
            Debug.Log($"Web build ready: {ZipPath} ({bytes / (1024f * 1024f):0.0} MB). Attach it to a new GitHub release to publish it.");
            EditorUtility.RevealInFinder(ZipPath);
        }

        /// <summary>Zips a folder's contents (not the folder itself), so index.html sits at the top of the zip.</summary>
        static void Zip(string folder, string zipPath)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            string root = Path.GetFullPath(folder);
            using (FileStream file = File.Create(zipPath))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    string name = path.Substring(root.Length + 1).Replace('\\', '/');
                    // The build files are already compressed, so light compression is plenty.
                    ZipArchiveEntry entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Fastest);
                    using (Stream input = File.OpenRead(path))
                    using (Stream output = entry.Open())
                        input.CopyTo(output);
                }
            }
        }
    }
}
