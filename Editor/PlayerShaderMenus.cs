using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace AddressablesPlayerShaders.Editor
{
    public static class PlayerShaderMenus
    {
        const string Menu = "Tools/Addressables Player Shaders/";

        [MenuItem(Menu + "Export Player Shader Manifest")]
        public static void ExportPlayerManifest()
        {
            Export(GraphicsSettingsScope.ReadShaders(), "declaration-only", "player-shaders.json");
        }

        [MenuItem(Menu + "Export Publisher Draft from Included Content")]
        public static void ExportPublisherDraft()
        {
            var evidence = new BuildEvidence();
            var found = ContentShaderScan.Run(AddressableAssetSettingsDefaultObject.Settings, null, evidence);
            evidence.status = "scan-only";
            evidence.Save("latest-scan.json");
            Export(GraphicsSettingsScope.ReadShaders().Concat(found).ToArray(), "publisher-draft", "publisher-shaders-DRAFT.json");
            Debug.LogWarning("APS: This draft only declares shaders found in the publisher. It does NOT establish that any Player supplies them. Use it only for an isolated publisher build experiment.");
        }

        static void Export(Shader[] shaders, string status, string filename)
        {
            if (File.Exists(GraphicsSettingsScope.JournalPath))
                throw new InvalidOperationException("APS: Restore interrupted build settings before exporting a manifest.");
            var target = EditorUserBuildSettings.activeBuildTarget;
            var manifest = new ShaderManifest
            {
                status = status,
                unityVersion = Application.unityVersion,
                buildTarget = target.ToString(),
                renderPipelineType = ShaderManifest.PipelineType(),
                graphicsApis = PlayerSettings.GetGraphicsAPIs(target).Select(api => api.ToString()).ToArray(),
                shaders = shaders.Where(shader => shader != null).Distinct().Select(ShaderIdentity.Capture).ToArray()
            };
            var json = JsonUtility.ToJson(manifest, true);
            ShaderManifest.Parse(json); // Validate before writing.
            var path = EditorUtility.SaveFilePanel("Export shader declaration (not compiled variant proof)", "", filename, "json");
            if (string.IsNullOrEmpty(path)) return;
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            Debug.Log("APS: Exported " + status + " manifest to " + path);
        }

        [MenuItem(Menu + "Create and Select Publisher Build Script")]
        public static void CreatePublisherBuilder()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) throw new InvalidOperationException("APS: Create Addressables settings first.");
            var builder = ScriptableObject.CreateInstance<BuildScriptPlayerShaders>();
            var path = AssetDatabase.GenerateUniqueAssetPath("Assets/BuildScriptPlayerShaders.asset");
            AssetDatabase.CreateAsset(builder, path);
            settings.AddDataBuilder(builder);
            settings.ActivePlayerDataBuilderIndex = settings.DataBuilders.IndexOf(builder);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Selection.activeObject = builder;
            Debug.Log("APS: Selected experimental content builder. Assign its Player Shader Manifest field. Use isolated build/load paths before building.");
        }

        [MenuItem(Menu + "Restore Interrupted Build Settings")]
        public static void RestoreInterruptedBuildSettings()
        {
            GraphicsSettingsScope.RestoreInterrupted();
            Debug.LogWarning("APS: Restored pre-build Graphics settings on disk. Restart the Editor before continuing so all settings reload.");
        }

        [MenuItem(Menu + "Reveal Latest Build Report")]
        public static void RevealLatestBuildReport()
        {
            var path = "Library/AddressablesPlayerShaders/latest-build.json";
            if (!File.Exists(path)) throw new InvalidOperationException("APS: No build report yet.");
            EditorUtility.RevealInFinder(Path.GetFullPath(path));
        }

        // Use -executeMethod AddressablesPlayerShaders.Editor.PlayerShaderMenus.BuildForCI.
        // The caller chooses the output paths and uploads only after checking this process exit/result.
        public static void BuildForCI()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null || !(settings.ActivePlayerDataBuilder is BuildScriptPlayerShaders))
                throw new InvalidOperationException("APS: The active Addressables builder is not Use Player Shaders.");
            AddressableAssetSettings.BuildPlayerContent(out var result);
            if (result == null || !string.IsNullOrEmpty(result.Error))
                throw new InvalidOperationException("APS: Content build failed: " + result?.Error);
        }
    }
}
