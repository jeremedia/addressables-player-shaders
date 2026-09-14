using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AddressablesPlayerShaders.Editor
{
    // Uses Unity's serialized settings field because no public setter exposes this list.
    // Fail explicitly if Unity changes that field. A journal survives Editor termination.
    internal sealed class GraphicsSettingsScope : IDisposable
    {
        internal const string SettingsPath = "ProjectSettings/GraphicsSettings.asset";
        internal const string JournalPath = "Library/AddressablesPlayerShaders/graphics-settings.backup";
        internal const string AppliedHashPath = "Library/AddressablesPlayerShaders/graphics-settings.applied-sha256";
        readonly byte[] original;
        readonly Shader[] originalShaders;
        bool disposed;

        internal static SerializedObject Open()
        {
            if (SessionState.GetBool("APS.RestartAfterRecovery", false))
                throw new InvalidOperationException("APS: Restart the Editor after recovering Graphics settings from disk.");
            var assets = AssetDatabase.LoadAllAssetsAtPath(SettingsPath);
            if (assets.Length == 0) throw new InvalidOperationException("APS: Cannot open GraphicsSettings.asset.");
            return new SerializedObject(assets[0]);
        }

        internal static SerializedProperty List(SerializedObject settings)
        {
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            if (list == null || !list.isArray)
                throw new InvalidOperationException("APS: Unity's Always Included Shaders serialization is unsupported.");
            return list;
        }

        internal static Shader[] ReadShaders()
        {
            using (var settings = Open())
            {
                var list = List(settings);
                var shaders = new Shader[list.arraySize];
                for (var i = 0; i < shaders.Length; i++) shaders[i] = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                return shaders;
            }
        }

        internal GraphicsSettingsScope(Shader[] shaders)
        {
            if (File.Exists(JournalPath))
                throw new InvalidOperationException("APS: A Graphics settings recovery journal exists. Use Tools > Addressables Player Shaders > Restore Interrupted Build Settings before building again.");
            // Persist pending settings first so restore does not discard user edits.
            using (var settings = Open()) AssetDatabase.SaveAssetIfDirty(settings.targetObject);
            originalShaders = ReadShaders();
            original = File.ReadAllBytes(SettingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(JournalPath));
            File.WriteAllBytes(JournalPath + ".tmp", original);
            File.Move(JournalPath + ".tmp", JournalPath);
            try
            {
                using (var settings = Open())
                {
                    var list = List(settings);
                    list.arraySize = shaders.Length;
                    for (var i = 0; i < shaders.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = shaders[i];
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssetIfDirty(settings.targetObject);
                }
                File.WriteAllText(AppliedHashPath, ShaderIdentity.Hash(File.ReadAllBytes(SettingsPath)));
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            // ProjectSettings is not an ordinary imported asset. Restore live serialized state explicitly.
            using (var settings = Open())
            {
                var list = List(settings);
                list.arraySize = originalShaders.Length;
                for (var i = 0; i < originalShaders.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = originalShaders[i];
                settings.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssetIfDirty(settings.targetObject);
            }
            RestoreBytes(original);
            disposed = true;
        }

        internal static void RestoreInterrupted()
        {
            if (!File.Exists(JournalPath)) throw new InvalidOperationException("APS: No interrupted build settings to restore.");
            // Do not silently overwrite settings edited by a person after a crash.
            var current = ShaderIdentity.Hash(File.ReadAllBytes(SettingsPath));
            var original = File.ReadAllBytes(JournalPath);
            if (current != ShaderIdentity.Hash(original) &&
                (!File.Exists(AppliedHashPath) || current != File.ReadAllText(AppliedHashPath)))
                throw new InvalidOperationException("APS: Graphics settings changed since the interrupted build. Compare the current file with " + JournalPath + " and restore manually; the backup has been preserved.");
            RestoreBytes(original);
            SessionState.SetBool("APS.RestartAfterRecovery", true);
        }

        static void RestoreBytes(byte[] bytes)
        {
            File.WriteAllBytes(SettingsPath, bytes);
            File.Delete(JournalPath);
            if (File.Exists(AppliedHashPath)) File.Delete(AppliedHashPath);
        }
    }
}
