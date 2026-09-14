# Design decisions

## Boundary

Materials, textures and geometry are content. Shader programs and the supported variant vocabulary belong to a Player release. Different Players may declare different shader sets. No Curation Engine APIs, private asset-project files, or vendor shader code are required by this package.

The first package is a diagnostic implementation of a candidate external-reference mechanism. It is deliberately not advertised as a production shader-free bundle builder.

## Generic build policy

Select the custom `Use Player Shaders (Experimental)` builder to opt in, or the original Addressables builder for normal behavior. The policy covers the entire build because the Graphics setting is project-wide and dependencies can cross Addressables groups. Per-group shader policies, content directories and content-update builds are out of scope for this version.

The preflight scan is conservative. The native SBP graph is checked again before serialization to catch dependencies not visible as ordinary material assets. This second scan loads objects, which can cost significant Editor memory on large catalogs. Unknown objects cause a diagnostic failure rather than an unsupported claim of completeness.

## Matching

Version 1 matches GUID plus local file ID and checks the declared name and source fingerprint. Preserve shader `.meta` files between projects. The fingerprint combines source-file bytes for the shader and shader-related dependencies returned by AssetDatabase. It excludes imported artifacts because importer outputs can differ between projects. The fingerprint is not a proof that every generated include was discovered, nor does it cover all pipeline settings or actual compiled variants.

The same shader name with another identity is rejected. Foreign shaders require a future material conversion extension that understands texture slots, scalar properties, keywords and render state. A generic string alias does not establish those semantics.

Built-in shaders are recorded with persistent identities and `unity-builtin` fingerprints. Their contents are tied to the exact Unity version declared in the manifest. Their resolution across the two projects is part of the first integration test.

## Build state and cache

The builder resolves the complete manifest, not just shaders found in one material. It applies the declared Always Included list for the build scope. Normal disposal restores the original list in memory and original settings bytes on disk. Crash recovery uses a journal and refuses to overwrite later settings edits. Recovery on disk requires an Editor restart.

SBP's static callback object is temporarily replaced by a wrapper that calls the previous callbacks first. A nesting guard prevents two simultaneous policy builds. A custom extension that installs its own callback context may bypass these hooks; a missing-hook check fails the build rather than claiming the policy executed.

The PostScripts hook sets `UseCache = false` before dependency tasks. The PostDependency and PostPacking hooks repeat it before subsequent tasks. This bypasses SBP reuse for the experiment without purging other builds. The packing hook also includes a policy/manifest discriminator in each write dependency hash. Native shader caching can still suppress preprocessing callbacks; use a disposable cold-cache project to test that path.

Preprocessing guards throw on a nonempty shader variant list. They do not clear the list and ship a stripped shader. A failure can leave partial Addressables outputs. Use isolated paths and make the publication workflow check the failed result.

## Evidence levels

1. **Publisher draft:** shaders discovered locally; no Player availability evidence.
2. **Player declaration:** exported Always Included list and source/environment identities; still no compiled variant proof.
3. **Instrumented build:** required hooks ran and no nonempty compile request was observed.
4. **Binary inspection:** all output bundles, including shared/built-in bundles, contain no shader program payloads. Not automated by version 1.
5. **Standalone Player proof:** loaded material resolves to the directly referenced Player shader object and renders all required states under strict variant matching. The sample assists with object identity only; it does not certify visual output or every variant.

Do not promote one level to another automatically. In particular, a Shader object or a `_unitybuiltinassets` bundle name does not establish that compiled shader bytecode is present. A zero callback count does not establish its absence.

## If the native mechanism fails

Record the first failing shader, native object identity, callback count, versions and build report. Determine whether the failure is a missing declaration, a pipeline dependency, an instrumentation limitation or genuine native shader compilation despite Always Included settings. Do not remove dependency nodes, zero shader variants, rewrite material references or add runtime Shader.Find rebinding as an unreviewed workaround.

A future alternate implementation may need a defined material representation and explicit runtime rebinding. That would be a separate mode with its own compatibility and loading contract.

## Gates before production

- Prove exact native behavior on the target Unity/Addressables/SBP versions.
- Automate binary shader payload auditing.
- Establish a bounded Player variant manifest and validate material keyword combinations against it, including lightmap/instancing states.
- Validate pipeline asset/quality settings and any generated shader include dependencies.
- Prove cold/warm cache behavior and all settings restoration paths.
- Support custom publisher integration without bypassing the policy.
- Add incremental content-update support only after full-build evidence is sound.
