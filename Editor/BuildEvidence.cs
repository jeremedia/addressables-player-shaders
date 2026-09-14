using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AddressablesPlayerShaders.Editor
{
    [Serializable]
    internal sealed class BuildEvidence
    {
        public string packageVersion = "0.1.0-exp.1";
        public string unityVersion = Application.unityVersion;
        public string addressablesVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptPackedMode).Assembly)?.version;
        public string sbpVersion = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityEditor.Build.Pipeline.ContentPipeline).Assembly)?.version;
        public string startedUtc = DateTime.UtcNow.ToString("O");
        public string finishedUtc;
        public string manifestHash;
        public string status = "not-built";
        public string error;
        public int rootAssetCount;
        public string[] shaderNames;
        public List<ShaderIdentity> discoveredShaders = new List<ShaderIdentity>();
        public List<string> sbpShaderReferences = new List<string>();
        public List<string> scheduledShaderObjects = new List<string>();
        public List<string> compilationRequests = new List<string>();
        public bool scriptsHookRan;
        public bool dependencyHookRan;
        public bool packingHookRan;
        public bool sbpCacheDisabled;
        public bool graphicsSettingsRestored;
        public bool bytecodeAbsenceVerified = false;
        public bool standalonePlayerVerified = false;
        public string evidenceLimit = "Build callbacks are instrumentation, not a binary bundle audit. Native shader caches may suppress callbacks. Shader reference records may be legitimate. Verify a cold-cache build and a standalone Player separately.";

        internal void Save(string filename)
        {
            finishedUtc = DateTime.UtcNow.ToString("O");
            var folder = "Library/AddressablesPlayerShaders";
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, filename), JsonUtility.ToJson(this, true));
        }
    }
}
