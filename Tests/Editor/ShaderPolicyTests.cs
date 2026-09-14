using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEditor.Build.Pipeline;
using UnityEngine;
using AddressablesPlayerShaders.Editor;

namespace AddressablesPlayerShaders.Tests
{
    public sealed class ShaderPolicyTests
    {
        static ShaderManifest ValidManifest() => new ShaderManifest
        {
            unityVersion = Application.unityVersion,
            buildTarget = "StandaloneOSX",
            renderPipelineType = "BuiltIn",
            graphicsApis = new[] { "Metal" },
            shaders = new[] { new ShaderIdentity { name = "Test/Surface", guid = "0123456789abcdef0123456789abcdef", localId = 4800000, sourceHash = new string('a', 64) } }
        };

        [Test]
        public void ManifestRejectsAmbiguousNamesAndIdentities()
        {
            var manifest = ValidManifest();
            manifest.shaders = new[] { manifest.shaders[0], manifest.shaders[0] };
            Assert.Throws<InvalidOperationException>(() => ShaderManifest.Parse(JsonUtility.ToJson(manifest)));
            manifest.shaders[1] = new ShaderIdentity { name = "Test/Surface", guid = new string('b', 32), localId = 4800000, sourceHash = new string('a', 64) };
            Assert.Throws<InvalidOperationException>(() => ShaderManifest.Parse(JsonUtility.ToJson(manifest)));
        }

        [Test]
        public void ManifestRequiresEnvironmentAndNonemptyShaderSet()
        {
            Assert.Throws<InvalidOperationException>(() => ShaderManifest.Parse("{}"));
            var manifest = ValidManifest();
            manifest.graphicsApis = Array.Empty<string>();
            Assert.Throws<InvalidOperationException>(() => ShaderManifest.Parse(JsonUtility.ToJson(manifest)));
        }

        [Test]
        public void GuardRestoresCallbacksAfterCompilationFailure()
        {
            var before = ContentPipeline.BuildCallbacks;
            var report = new BuildEvidence();
            Assert.Throws<BuildFailedException>(() =>
            {
                using (var guard = new ShaderBuildGuard(ValidManifest(), report))
                    guard.RejectCompilation("Test/Surface", "Shader", 1);
            });
            Assert.That(ContentPipeline.BuildCallbacks, Is.SameAs(before));
            Assert.That(ShaderBuildGuard.Active, Is.Null);
            Assert.That(report.compilationRequests.Count, Is.EqualTo(1));
        }

        [Test]
        public void GuardRejectsNestedBuildWithoutReplacingOuterGuard()
        {
            using (var guard = new ShaderBuildGuard(ValidManifest(), new BuildEvidence()))
            {
                var callbacks = ContentPipeline.BuildCallbacks;
                Assert.Throws<InvalidOperationException>(() => new ShaderBuildGuard(ValidManifest(), new BuildEvidence()));
                Assert.That(ShaderBuildGuard.Active, Is.SameAs(guard));
                Assert.That(ContentPipeline.BuildCallbacks, Is.SameAs(callbacks));
            }
        }

        [Test]
        public void GraphicsSettingsRestoreOnException()
        {
            if (File.Exists(GraphicsSettingsScope.JournalPath)) Assert.Ignore("Recover an interrupted build before running this test.");
            var beforeShaders = GraphicsSettingsScope.ReadShaders();
            // Save any pending settings, as the real scope does, before comparing bytes.
            using (var settings = GraphicsSettingsScope.Open()) UnityEditor.AssetDatabase.SaveAssetIfDirty(settings.targetObject);
            var before = File.ReadAllBytes(GraphicsSettingsScope.SettingsPath);
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (new GraphicsSettingsScope(Array.Empty<Shader>()))
                {
                    Assert.That(GraphicsSettingsScope.ReadShaders(), Is.Empty);
                    throw new InvalidOperationException("Simulated build failure");
                }
            });
            Assert.That(File.ReadAllBytes(GraphicsSettingsScope.SettingsPath), Is.EqualTo(before));
            Assert.That(GraphicsSettingsScope.ReadShaders().SequenceEqual(beforeShaders), Is.True);
            Assert.That(File.Exists(GraphicsSettingsScope.JournalPath), Is.False);
        }
    }
}
