using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

namespace AddressablesPlayerShaders.Editor
{
    internal static class ContentShaderScan
    {
        internal static Shader[] Run(AddressableAssetSettings settings, ShaderManifest manifest, BuildEvidence evidence)
        {
            if (settings == null) throw new InvalidOperationException("APS: No Addressables settings.");
            var allowed = manifest?.shaders.ToDictionary(s => s.Key, StringComparer.Ordinal);
            var shaders = new HashSet<Shader>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var group in settings.groups)
            {
                if (group == null || !group.IncludeInBuild) continue;
                var schema = group.GetSchema<BundledAssetGroupSchema>();
                if (schema == null) throw new InvalidOperationException("APS: Only AssetBundle groups are supported: " + group.Name);
                var entries = new List<AddressableAssetEntry>();
                foreach (var entry in group.entries) entry.GatherAllAssets(entries, true, true, false);
                foreach (var entry in entries)
                {
                    if (entry.IsFolder || string.IsNullOrEmpty(entry.AssetPath)) continue;
                    evidence.rootAssetCount++;
                    var mainType = AssetDatabase.GetMainAssetTypeAtPath(entry.AssetPath);
                    if (mainType == typeof(Shader) || mainType == typeof(ComputeShader) || mainType == typeof(ShaderVariantCollection))
                        throw new InvalidOperationException("APS: A shader or variant collection is explicitly Addressable: " + entry.AssetPath + ". Remove its Addressables entry; the Player owns it.");
                    foreach (var path in AssetDatabase.GetDependencies(entry.AssetPath, true))
                    {
                        if (!seen.Add(path)) continue;
                        // Avoid loading large textures/meshes just to classify them.
                        var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                        if (type == typeof(ComputeShader) || type == typeof(ShaderVariantCollection))
                            throw new InvalidOperationException("APS: Unsupported shader artifact dependency: " + path + " (from " + entry.AssetPath + ").");
                        if (type == typeof(Shader)) Add(AssetDatabase.LoadAssetAtPath<Shader>(path), entry.AssetPath);
                        // Embedded model materials and prefab material references are included by dependency enumeration.
                        if (type == typeof(Material) || path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                            foreach (var material in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>()) Add(material.shader, path);
                    }
                }
            }
            evidence.shaderNames = shaders.Select(s => s.name).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            return shaders.OrderBy(s => s.name, StringComparer.Ordinal).ToArray();

            void Add(Shader shader, string owner)
            {
                if (shader == null) throw new InvalidOperationException("APS: Missing shader in " + owner);
                if (!shaders.Add(shader)) return;
                var id = ShaderIdentity.Capture(shader);
                evidence.discoveredShaders.Add(id);
                if (allowed != null && !allowed.ContainsKey(id.Key))
                    throw new InvalidOperationException($"APS: Shader {id.name} ({id.Key}) used by {owner} is absent from the Player manifest.");
            }
        }
    }
}
