using System;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEngine;

namespace AddressablesPlayerShaders.Editor
{
    [CreateAssetMenu(fileName = "BuildScriptPlayerShaders", menuName = "Addressables/Content Builders/Use Player Shaders (Experimental)")]
    public sealed class BuildScriptPlayerShaders : BuildScriptPackedMode
    {
        [Tooltip("JSON exported from the target Player project. A declaration, not proof of compiled variants.")]
        public TextAsset playerShaderManifest;

        public override string Name => "Use Player Shaders (Experimental)";

        protected override TResult BuildDataImplementation<TResult>(AddressablesDataBuilderInput builderInput)
        {
            var evidence = new BuildEvidence();
            try
            {
                if (playerShaderManifest == null) throw new InvalidOperationException("APS: Assign a Player shader manifest to this build script.");
                if (builderInput.PreviousContentState != null)
                    throw new InvalidOperationException("APS: Content updates are not supported by this experiment. Use New Build with an isolated output path.");
                if (builderInput.Target != EditorUserBuildSettings.activeBuildTarget)
                    throw new InvalidOperationException("APS: Switch the Editor to the intended build target before running this builder.");
                var manifest = ShaderManifest.Parse(playerShaderManifest.text);
                if (manifest.status == "publisher-draft")
                    Debug.LogWarning("APS: Using a publisher draft. Player shader availability has not been established; do not publish these experimental bundles.");
                manifest.ValidateEnvironment(builderInput.Target);
                evidence.manifestHash = ShaderIdentity.Hash(Encoding.UTF8.GetBytes(playerShaderManifest.text));
                var shaders = manifest.Resolve();
                ContentShaderScan.Run(builderInput.AddressableSettings, manifest, evidence);
                if (evidence.rootAssetCount == 0) throw new InvalidOperationException("APS: No included Addressable assets to build.");

                TResult result;
                using (new ShaderBuildGuard(manifest, evidence))
                {
                    using (new GraphicsSettingsScope(shaders))
                        result = base.BuildDataImplementation<TResult>(builderInput);
                    evidence.graphicsSettingsRestored = true;
                }
                if (result == null) throw new InvalidOperationException("APS: Addressables returned no build result.");
                if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
                if (!evidence.packingHookRan || !evidence.sbpCacheDisabled || evidence.compilationRequests.Count != 0)
                    throw new InvalidOperationException("APS: Required build evidence is missing or shader compilation was requested. Do not publish these outputs.");
                evidence.status = "build-completed-awaiting-binary-and-player-verification";
                Debug.Log("APS: Content build completed with no observed shader compilation requests. Binary bundle and standalone Player verification remain required. Report: Library/AddressablesPlayerShaders/latest-build.json");
                return result;
            }
            catch (Exception exception)
            {
                evidence.status = "failed";
                evidence.error = exception.ToString();
                Debug.LogError("APS: " + exception.Message);
                return AddressableAssetBuildResult.CreateResult<TResult>(null, 0, exception.Message);
            }
            finally
            {
                // A surviving journal is evidence of incomplete restoration, regardless of the build result.
                evidence.graphicsSettingsRestored = !System.IO.File.Exists(GraphicsSettingsScope.JournalPath);
                evidence.Save("latest-build.json");
            }
        }
    }
}
