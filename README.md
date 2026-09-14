# Addressables Player Shaders

An experimental Unity package for content projects whose shaders are supplied by the Player.

**Goal:** build Addressable materials, textures and meshes without compiling target shader variants or shipping shader bytecode from the content project. The package is generic; it has no Curation Engine or shader-vendor dependency.

**Status: `0.1.0-exp.1` — implementation ready for first Unity test, not a verified shader externalization solution.** Source/API inspection targets Unity 6.6, Addressables 4.0.1 and Scriptable Build Pipeline 4.0.0. No Unity compilation, Editor tests, bundle build, binary inspection or standalone Player run has been performed by the authoring environment. A successful experimental build is not a bytecode-free certification.

## Install

In Unity Package Manager, choose **Install package from Git URL**:

```text
https://github.com/jeremedia/addressables-player-shaders.git#main
```

For reproducible testing, replace `main` with the commit SHA you installed and record it with your results. The package root is the repository root; no `?path=` suffix is needed. Its declared dependencies are Addressables 4.0.1 and SBP 4.0.0. Inspect Package Manager resolution if your project already requests other versions.

## First test: materials publisher only

This is the shortest experiment for an asset-publishing project such as `ce-asset` / `ce-materials`. It does not require altering the production Player first.

1. Use a test copy or isolated Addressables profile. Set **Build Path and Load Path** to test locations and disable any automatic upload/publish step. This builder writes to the ordinary Addressables paths you configure. Existing output files can be overwritten by a build; failed builds can leave partial outputs.
2. Start with one material as the only included Addressable entry. Use a simple shader for the mechanism test, then repeat with the actual material shader. Direct shader entries, compute shaders and ShaderVariantCollections are rejected. Exclude unrelated groups for this first run.
3. Select the intended build target in Unity. Run **Tools > Addressables Player Shaders > Export Publisher Draft from Included Content**. Save the JSON under `Assets/Editor/` and let Unity import it.
4. Run **Tools > Addressables Player Shaders > Create and Select Publisher Build Script**. Assign the JSON TextAsset to **Player Shader Manifest** on the selected asset.
5. In Addressables Groups, use **Build > New Build > Use Player Shaders (Experimental)**. Do not use Update a Previous Build. If your own publisher wrapper explicitly creates a stock `BuildScriptPackedMode`, it bypasses this extension: run the Addressables menu for the first test, or change that wrapper to use the selected `ActivePlayerDataBuilder`.
6. Open `Library/AddressablesPlayerShaders/latest-build.json`. Keep this report, Unity's Editor.log, the Addressables Build Layout/report and the isolated bundles. Do not upload the bundles to a production catalog.

The draft combines the publisher's existing Always Included Shaders with the shaders discovered in included content. It records `status: publisher-draft`, explicitly warns on build and **does not claim that any Player contains those shaders**. Shader discovery may import assets or compile Editor previews; this experiment concerns target variant compilation during bundle builds.

### Interpret the first result

| Result | Meaning / next action |
| --- | --- |
| `APS: Shader compilation requested ...` | Unity scheduled variants despite the external-reference settings. The guard throws before those callback variants compile. Keep the report and log; the proposed native route is not validated for this configuration. Do not strip everything and treat that as success. |
| Unmapped shader or unsupported artifact | A dependency was not part of the declaration, including possibly a fallback or generated shader. Inspect the reported name and identity. Add a compatible Player declaration only if the Player really supplies it. |
| Cannot classify SBP object | The conservative dependency check encountered a native/generated object it could not load. Report its identifier; this is an instrumentation limitation, not evidence that the object is a shader. |
| `build-completed-awaiting-binary-and-player-verification` | No nonempty shader-compilation callback was observed and required hooks ran. Inspect the bundles and test the Player next. |
| No APS report/log | Your build path probably bypassed this builder. Confirm the active builder and the publisher wrapper. |

The report intentionally retains `bytecodeAbsenceVerified: false` and `standalonePlayerVerified: false`. It lists scheduled Shader objects because an object can be a reference record rather than shader bytecode; object presence alone is not a failure.

## Player-backed test

1. Install the package in the target Player project too. Use the same Unity patch, build target, graphics APIs, shader assets with their `.meta` identities, and compatible render-pipeline/shader package versions.
2. Verify the relevant shaders are in the Player's **Project Settings > Graphics > Always Included Shaders**. The exporter reads that project-wide list and does not change it. Build Profile Graphics overrides are not supported or validated by this first version; disable such overrides in both test projects so they actually use the project settings being exported/applied.
3. Run **Export Player Shader Manifest** there and copy its JSON into the publisher. This is a **declaration**, not proof that the Player retained all needed variants. Assign this manifest instead of the publisher draft, then rebuild isolated content.
4. Import the **Player Material Verification** sample through Package Manager in the Player project. Put its component in a regular Player scene. Set `catalogUrl` to the isolated catalog (or leave it empty if your app already loads that catalog), `materialAddress` to your test key, `expectedPlayerShader` to the direct Player shader asset, and `displayRenderer` to a visible test mesh.
5. Build and run the Player. The component compares the loaded material's shader to the **actual referenced Player shader object**. It does not call Shader.Find, replace the shader, clone the material or rebind anything. It then displays the material for visual inspection. Enable Unity's strict shader variant matching and inspect logs/rendering for missing variants.

Use a unique test material key and catalog location so an older catalog cannot satisfy the request accidentally. The component owns its handles and releases them on destruction; it does not remove a shared catalog's resource locator.

**Player cost:** Always Included Shaders can request every variant of a shader. Start with a small shader. Do not build a large third-party uber-shader with unrestricted variants just to test this mechanism. Player variant budgeting is a separate unresolved integration gate. This package does not configure stripping or prewarming.

## What this version implements

- A selectable Addressables content builder deriving from `BuildScriptPackedMode`; the normal builder remains available.
- Shader identity matching by GUID and local file ID, name and a source-content fingerprint. No name-only aliases or foreign-material conversion.
- An environment declaration for Unity version, target, graphics APIs and active pipeline type. Pipeline type equality is not full pipeline-setting equivalence.
- Publisher preflight scanning plus inspection of SBP dependency/write objects. Undeclared shaders, compute shaders and shader variant collections fail the experimental build.
- A temporary Always Included Shaders list taken from the manifest, restored after success or exception. Source materials and shader assets are not rewritten.
- SBP callbacks that disable dependency/write-cache reuse for this experiment, without deleting the global cache. Write operations also incorporate the manifest hash to separate them from ordinary cached builds. Native shader caches are separate.
- Early `IPreprocessShaders` / `IPreprocessComputeShaders` guards that reject nonempty compilation requests while this builder runs. They do not alter ordinary Player builds.
- A local JSON evidence report, Editor tests for manifest validation and state restoration, and an optional standalone material-verification component.

The SBP inspection currently loads native dependency objects once per phase. That adds Editor memory/time overhead; measure it after the one-material proof before testing a large catalog. The installed package is Editor-only; the runtime verification component is an opt-in sample with readable textual pass/fail output.

## Recovery and rollback

Normal completion restores both the live shader list and the original Graphics settings file bytes. A backup is written first to `Library/AddressablesPlayerShaders/graphics-settings.backup`. If Unity terminates during the build, subsequent policy builds refuse to run while that journal exists.

Use **Restore Interrupted Build Settings**, then restart Unity. Recovery refuses to overwrite a settings file that changed after the build; compare it with the backup and restore manually in that case. Do not delete a recovery journal without checking Graphics settings. No settings restoration can run after a hard process termination.

To stop using the experiment, select the original Addressables builder and restore the original test profile paths. Review the Graphics settings diff. Existing outputs remain until you remove or replace them deliberately.

## Tests and automation

To expose package Editor tests, add this entry to the consuming project's `Packages/manifest.json`:

```json
"testables": ["com.jeremedia.addressables-player-shaders"]
```

Run the `AddressablesPlayerShaders.Editor.Tests` assembly in Unity Test Runner. Tests temporarily change the Always Included list and verify restoration; run with no pending build or recovery journal.

For an already configured publisher, the batch-mode entry point is:

```text
-executeMethod AddressablesPlayerShaders.Editor.PlayerShaderMenus.BuildForCI
```

It requires this builder to be selected, checks the Addressables result and throws on failure. It does not upload content. The caller must check the Unity process exit and retain reports before advancing any publication step.

See [the validation matrix](Documentation~/validation.md) and [the design decisions](Documentation~/design.md).

## Why try this mechanism?

Unity documents that Always Included Shaders can cause bundles to store shader references instead of platform-specific shader code. That article uses the traditional AssetBundle API; it does not establish the result for this package's target SBP version. This repository makes that hypothesis testable without silently assuming it works.

- [Unity Support: shaders loaded from AssetBundles](https://support.unity.com/hc/en-us/articles/208380753-Shaders-are-pink-when-loaded-from-an-AssetBundle)
- [Unity 6.6 Graphics settings](https://docs.unity3d.com/6000.6/Documentation/Manual/class-GraphicsSettings.html)
- [Unity 6.6 shader preprocessing callbacks and cache caveats](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Build.IPreprocessShaders.OnProcessShader.html)
- [Addressables 4.0.1 build-script source (package mirror)](https://github.com/needle-mirror/com.unity.addressables/blob/4.0.1/Editor/Build/DataBuilders/BuildScriptPackedMode.cs)
- [SBP 4.0.0 callbacks (package mirror)](https://github.com/needle-mirror/com.unity.scriptablebuildpipeline/blob/4.0.0/Editor/Shared/BuildCallbacks.cs)

Unity/SBP sources and shader-vendor assets are not redistributed in this repository.
