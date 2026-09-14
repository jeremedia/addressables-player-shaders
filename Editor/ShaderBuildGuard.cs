using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Content;
using UnityEditor.Build.Pipeline;
using UnityEditor.Build.Pipeline.Interfaces;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace AddressablesPlayerShaders.Editor
{
    internal sealed class ShaderBuildGuard : IDisposable
    {
        internal static ShaderBuildGuard Active { get; private set; }
        readonly BuildCallbacks previous;
        readonly HashSet<string> allowed;
        readonly BuildEvidence evidence;
        readonly HashSet<ObjectIdentifier> inspected = new HashSet<ObjectIdentifier>();
        bool disposed;

        internal ShaderBuildGuard(ShaderManifest manifest, BuildEvidence evidence)
        {
            if (Active != null) throw new InvalidOperationException("APS: Nested shader-policy builds are unsupported.");
            this.evidence = evidence;
            allowed = new HashSet<string>(manifest.shaders.Select(s => s.Key), StringComparer.Ordinal);
            previous = ContentPipeline.BuildCallbacks;
            if (previous == null) throw new InvalidOperationException("APS: SBP callbacks are unavailable.");
            ContentPipeline.BuildCallbacks = new BuildCallbacks
            {
                PostScriptsCallbacks = (parameters, results) =>
                {
                    var code = previous.PostScripts(parameters, results);
                    if (code < ReturnCode.Success) return code;
                    parameters.UseCache = false;
                    evidence.scriptsHookRan = true;
                    evidence.sbpCacheDisabled = true;
                    return code;
                },
                PostDependencyCallback = (parameters, dependencies) =>
                {
                    var code = previous.PostDependency(parameters, dependencies);
                    if (code < ReturnCode.Success) return code;
                    parameters.UseCache = false;
                    foreach (var asset in dependencies.AssetInfo.Values)
                        foreach (var id in asset.includedObjects.Concat(asset.referencedObjects)) Inspect(id, false);
                    foreach (var scene in dependencies.SceneInfo.Values)
                        foreach (var id in scene.referencedObjects) Inspect(id, false);
                    evidence.dependencyHookRan = true;
                    return code;
                },
                PostPackingCallback = (parameters, dependencies, writes) =>
                {
                    var code = previous.PostPacking(parameters, dependencies, writes);
                    if (code < ReturnCode.Success) return code;
                    if (!evidence.scriptsHookRan || !evidence.dependencyHookRan)
                        throw new BuildFailedException("APS: Expected SBP hooks did not execute. Refusing to write bundles.");
                    parameters.UseCache = false;
                    inspected.Clear();
                    foreach (var operation in writes.WriteOperations)
                    {
                        // Defense in depth: separate write hashes from ordinary builds even if another extension re-enables cache.
                        operation.DependencyHash = Hash128.Compute(operation.DependencyHash + "|APS-v1|" + evidence.manifestHash);
                        foreach (var obj in operation.Command.serializeObjects) Inspect(obj.serializationObject, true);
                    }
                    evidence.packingHookRan = true;
                    return code;
                },
                PostWritingCallback = (parameters, dependencies, writes, results) => previous.PostWriting(parameters, dependencies, writes, results)
            };
            Active = this;
        }

        void Inspect(ObjectIdentifier id, bool scheduled)
        {
            if (!inspected.Add(id)) return;
            // Deliberately inspect native SBP dependencies too, including generated/built-in shader objects.
            // This loads referenced objects once per phase and is an experimental validation cost.
            var obj = ObjectIdentifier.ToObject(id);
            if (obj == null)
                throw new BuildFailedException("APS: Cannot classify SBP object " + id + ". No shader-free claim can be made; inspect this dependency.");
            if (obj is ComputeShader || obj is ShaderVariantCollection)
                throw new BuildFailedException("APS: Unsupported shader artifact in SBP graph: " + obj.name + " (" + id + ").");
            if (!(obj is Shader shader)) return;
            var key = id.guid + ":" + id.localIdentifierInFile;
            if (!allowed.Contains(key))
                throw new BuildFailedException($"APS: SBP found undeclared shader {shader.name} ({key}). Include the matching Player shader in the manifest or remove this content dependency.");
            var record = shader.name + " (" + key + ")";
            (scheduled ? evidence.scheduledShaderObjects : evidence.sbpShaderReferences).Add(record);
            // A serialized Shader object can be an external reference record, not necessarily bytecode.
        }

        internal void RejectCompilation(string name, string kind, int count)
        {
            var message = $"APS: {kind} compilation requested for {name}: {count} variant(s). External-reference behavior did not bypass this shader. Build stopped before this callback's variants were compiled.";
            evidence.compilationRequests.Add(message);
            throw new BuildFailedException(message);
        }

        public void Dispose()
        {
            if (disposed) return;
            ContentPipeline.BuildCallbacks = previous;
            Active = null;
            disposed = true;
        }
    }

    public sealed class RejectContentShaderCompilation : IPreprocessShaders
    {
        public int callbackOrder => int.MinValue;
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (data.Count > 0) ShaderBuildGuard.Active?.RejectCompilation(shader.name, "Shader", data.Count);
        }
    }

    public sealed class RejectContentComputeCompilation : IPreprocessComputeShaders
    {
        public int callbackOrder => int.MinValue;
        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
        {
            if (data.Count > 0) ShaderBuildGuard.Active?.RejectCompilation(shader.name + "/" + kernelName, "Compute shader", data.Count);
        }
    }
}
