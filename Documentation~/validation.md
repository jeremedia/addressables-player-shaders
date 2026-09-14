# First validation run

No Unity execution has been performed in the repository authoring environment. Complete these gates in a disposable publisher/Player pair and keep the evidence with the tested commit.

## Baseline

Record Unity patch, Addressables and SBP resolved versions, OS, target, graphics APIs, render-pipeline and shader package versions, active quality/profile settings, material key and keyword state. Record GraphicsSettings file hash before and after each run. Use distinct catalog URLs/material keys and isolated output directories.

Run a normal Addressables build for comparison. Keep Editor.log, Build Layout, duration and total bundle bytes. Then run the custom builder on the same material.

## Matrix

| Case | Expected evidence |
| --- | --- |
| Package import and five Editor tests | No compile errors; manifest rejection, guard cleanup, nesting protection, live/disk settings restoration pass. |
| One small shader/material, publisher draft | Preflight and SBP hooks execute. Either a precise compilation rejection or an instrumented build result. Never a Player availability claim. |
| One actual material shader | Same checks; identify fallback/UsePass dependencies rather than silently compiling them. |
| Player-exported manifest | Matching GUID/local IDs, source fingerprints and environment. |
| Missing/changed shader identity | Fail before bundle building. |
| Same name, different source/GUID | Fail; no implicit name alias. |
| Explicit Addressable Shader or SVC / compute dependency | Fail with asset context. |
| Deliberate build exception | Original Graphics settings restored; callbacks no longer guarded in the next normal Player build. |
| Interrupted Editor process | Journal exists; next policy build refuses; recovery preserves later edits or restores file and requires restart. Test only in a copy. |
| Content-update invocation | Rejected before building. |
| Standard build, then policy build | No reuse of standard SBP write results. |
| Repeated policy build | Hooks still execute; report records SBP cache bypass. Native shader cache behavior is separately observed. |
| Standalone Player material sample | Loaded shader equals the explicitly referenced Player shader object; no runtime substitution. |
| Required material keyword states | Correct visible output under strict variant matching; inspect Player.log for missing variants. |
| Mac Metal and intended Web target | Repeat independently; a platform result does not certify another. |

## Cold-cache check

Use a disposable project copy with fresh Unity Library/cache state for the decisive test. Native shader caches can suppress callbacks, even when SBP caching is disabled. Import/Editor preview compilation may occur before the content build; distinguish it from compilation inside the instrumented build interval.

The first mechanism experiment should use a small shader to keep this cost bounded. Repeat with the production material shader after the mechanism is understood. Do not clear the working project's caches as a routine workaround.

## Binary audit

Inspect every output bundle with a parser that supports the tested Unity serialized-file version. Include shared and built-in bundles. Record tool/version, bundle hashes and whether serialized Shader entries contain external reference metadata or actual compiled program payloads. Build Layout shader entries and bundle names alone do not answer that question. Version 1 includes no binary parser and cannot certify bytecode absence.

## Report back

For the first materials test, retain:

- `Library/AddressablesPlayerShaders/latest-build.json` and, for a draft scan, `latest-scan.json`.
- First APS error and the surrounding Editor.log build section.
- Package commit and resolved Unity/package versions.
- GraphicsSettings before/after diff or hashes.
- Build Layout and output bundle sizes; binary inspection if available.
- Standalone sample result and strict-variant log/visual result when available.

Reports may include private asset names and paths. Keep project-specific logs and manifests in the consuming project; this package never uploads them.
