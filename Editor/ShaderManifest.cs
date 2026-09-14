using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace AddressablesPlayerShaders.Editor
{
    [Serializable]
    public sealed class ShaderManifest
    {
        public int schemaVersion = 1;
        public string status = "declaration-only";
        public string unityVersion;
        public string buildTarget;
        public string renderPipelineType;
        public string[] graphicsApis;
        public ShaderIdentity[] shaders;

        public static ShaderManifest Parse(string json)
        {
            var manifest = JsonUtility.FromJson<ShaderManifest>(json);
            if (manifest == null || manifest.schemaVersion != 1 || manifest.shaders == null || manifest.shaders.Length == 0)
                throw new InvalidOperationException("APS: Expected a schemaVersion 1 Player shader manifest with at least one shader.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (manifest.status != "declaration-only" && manifest.status != "publisher-draft")
                throw new InvalidOperationException("APS: Unsupported manifest status. This version accepts declarations, not claims of verified compiled variants.");
            foreach (var shader in manifest.shaders)
            {
                if (shader == null || string.IsNullOrWhiteSpace(shader.name) ||
                    string.IsNullOrWhiteSpace(shader.guid) || shader.guid.Length != 32 ||
                    shader.guid.Any(c => !Uri.IsHexDigit(c)) || shader.localId == 0)
                    throw new InvalidOperationException("APS: Manifest contains an invalid shader identity.");
                if (!keys.Add(shader.Key) || !names.Add(shader.name))
                    throw new InvalidOperationException("APS: Duplicate shader identity or name: " + shader.name);
                if (shader.sourceHash != "unity-builtin" && (shader.sourceHash == null || shader.sourceHash.Length != 64 || shader.sourceHash.Any(c => !Uri.IsHexDigit(c))))
                    throw new InvalidOperationException("APS: Missing or invalid source fingerprint: " + shader.name);
            }
            if (string.IsNullOrEmpty(manifest.unityVersion) || string.IsNullOrEmpty(manifest.buildTarget) ||
                string.IsNullOrEmpty(manifest.renderPipelineType) || manifest.graphicsApis == null || manifest.graphicsApis.Length == 0)
                throw new InvalidOperationException("APS: Manifest is missing its Unity, target, pipeline or graphics API declaration.");
            return manifest;
        }

        public void ValidateEnvironment(BuildTarget target)
        {
            if (unityVersion != Application.unityVersion || buildTarget != target.ToString() || renderPipelineType != PipelineType())
                throw new InvalidOperationException($"APS: Manifest environment mismatch. Expected {unityVersion}, {buildTarget}, {renderPipelineType}; current {Application.unityVersion}, {target}, {PipelineType()}.");
            var current = PlayerSettings.GetGraphicsAPIs(target).Select(api => api.ToString());
            if (!new HashSet<string>(graphicsApis, StringComparer.Ordinal).SetEquals(current))
                throw new InvalidOperationException("APS: Publisher graphics APIs differ from the Player manifest.");
        }

        public static string PipelineType()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline == null ? "BuiltIn" : pipeline.GetType().FullName;
        }

        public Shader[] Resolve()
        {
            return shaders.Select(identity =>
            {
                var path = AssetDatabase.GUIDToAssetPath(identity.guid);
                if (string.IsNullOrEmpty(path))
                    throw new InvalidOperationException("APS: Cannot resolve shader GUID in this project: " + identity.name + " (" + identity.guid + ").");
                var shader = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Shader>()
                    .FirstOrDefault(candidate => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(candidate, out string guid, out long id) && guid == identity.guid && id == identity.localId);
                if (shader == null)
                    throw new InvalidOperationException($"APS: Player shader {identity.name} ({identity.Key}) does not exist in this project. Install the same shader assets with preserved .meta GUIDs.");
                var local = ShaderIdentity.Capture(shader);
                if (local.name != identity.name || local.sourceHash != identity.sourceHash)
                    throw new InvalidOperationException($"APS: Shader name or source fingerprint differs from Player declaration: {identity.name} at {path}.");
                return shader;
            }).ToArray();
        }
    }

    [Serializable]
    public sealed class ShaderIdentity
    {
        public string name;
        public string guid;
        public long localId;
        public string sourceHash;
        public string path;
        public string Key => guid + ":" + localId;

        public static ShaderIdentity Capture(Shader shader)
        {
            if (shader == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(shader, out string guid, out long localId))
                throw new InvalidOperationException("APS: Shader has no persistent asset identity.");
            var path = AssetDatabase.GetAssetPath(shader);
            return new ShaderIdentity { name = shader.name, guid = guid, localId = localId, path = path, sourceHash = Fingerprint(path) };
        }

        // A source-content fingerprint, not an imported artifact hash (which may vary across projects).
        // Unity's dependency enumeration does not guarantee discovery of every dynamic/generated include.
        // Runtime verification and matching shader package versions remain required.
        static string Fingerprint(string path)
        {
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) && !path.StartsWith("Packages/", StringComparison.Ordinal))
                return "unity-builtin";
            var sources = AssetDatabase.GetDependencies(path, true)
                .Where(p => p == path || new[] { ".shader", ".shadergraph", ".shadersubgraph", ".hlsl", ".cginc", ".glslinc" }.Contains(Path.GetExtension(p)))
                .OrderBy(p => p, StringComparer.Ordinal);
            var records = new List<string>();
            foreach (var source in sources)
            {
                var diskPath = source;
                if (source.StartsWith("Packages/", StringComparison.Ordinal))
                {
                    var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(source);
                    if (package == null) throw new InvalidOperationException("APS: Cannot resolve package source: " + source);
                    diskPath = Path.Combine(package.resolvedPath, source.Substring(package.assetPath.Length).TrimStart('/'));
                }
                if (!File.Exists(diskPath)) throw new InvalidOperationException("APS: Cannot fingerprint shader source: " + source);
                records.Add(AssetDatabase.AssetPathToGUID(source) + ":" + Hash(File.ReadAllBytes(diskPath)));
            }
            return Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", records)));
        }

        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
    }
}
