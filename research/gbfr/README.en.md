# Granblue Fantasy: Relink native structure research

[한국어](README.md)

These notes cover lock-on target identification, boss part state, and the lifetime of indicators built with the existing UI. They describe static analysis and limited runtime observations of one executable, not an engine API specification for every enemy, quest, or form.

- [Lock-on and part state](lock-on-and-part-state.en.md)
- [Native UI ownership and lifecycle](native-ui-lifecycle.en.md)

## Target and environment

The initial analysis was recorded on November 8, 2026. Later release observations are identified separately below.

- Windows x64 `granblue_fantasy_relink.exe`, disk SHA-256 `4b3ace0fd03df5d3d08c6453e12df2279c6259abf7d2fb3622921fcbea775145`.
- Disk size: 123,520,480 bytes; loaded module image size: 136,413,184 bytes.
- PE `FileVersion` and `ProductVersion`: `2.0.6`. These are executable metadata strings, not verified title-screen versions or Steam build IDs.
- Static analysis used an existing Ghidra Program and targeted function, instruction, and RTTI inspection. Runtime observation used Frida memory reads and narrowly scoped function observation. Exact tool versions were not tied to every earlier sample.
- Initial research implementation: [`70f96f8`](https://github.com/czhskr/GBFR-Modding/tree/70f96f8611cf9762bc9a6d7c42a590336ed3d478/Part-Break-Indicator). Part Break Indicator 1.0.0 release source: [`ef42ea2`](https://github.com/czhskr/GBFR-Modding/tree/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator). Unless stated otherwise, code links point to the release source. C# role names were assigned during analysis and are not assumed to be original debug symbols.
- Released `PartIndicator.dll` SHA-256: `edb33d5b9975efd163c75c322d09caecff69926bd8cd066f5f2911b3d89e6bc3`. Its version information contains the release source commit; the DLLs in the release ZIP, local Release build, and validation artifacts matched by hash.
- Source build environment: .NET SDK `10.0.400`, target `net9.0`, x64, and locally installed Reloaded-II/Reloaded.Hooks assemblies. This does not fully reproduce the environment of every earlier native observation.

These were not controlled compatibility experiments covering all game installations or combinations of mods. Evidence for reading, damage, break, recovery, and UI transitions must be considered separately.

## Evidence labels

- **Static finding:** instructions, RTTI, constructors, call paths, or configuration consumption inspected for the identified executable.
- **Runtime observation:** value changes tracked with an object's identity and lifetime token, or behavior seen on screen. Samples are limited and not exhaustive.
- **Implementation policy:** checks, hiding, caching, and waiting rules adopted by Part Break Indicator, not universal engine guarantees.
- **Interpretation or hypothesis:** an explanation joining evidence that has not been established as a direct contract.
- **Unverified:** cases or relationships for which evidence is not yet available. Failure to find an item does not prove it is absent.

An `RVA` is relative to the loaded module; add the actual module base. `actor+…`, `scene+…`, and `owner+…` are offsets from the specified object. The static image's preferred base `0x140000000` is not the actual address after ASLR.

## Method and scope

Part connections are checked against the selection ID, exact runtime type, state array or owned child, and the source of the initial value. Where possible, changes are tracked before and after attacks for the same scene ID, address, and lifetime token, then checked after natural break and recovery. A locked part is not automatically the part hit.

Rechecking identities before and after a read is an implementation defense against reused addresses and targets changing during observation; a successful memory read does not itself establish object type, ownership, or synchronization. In-process memory and hook tests are separate from observations in the game.

Quadruped Furycane head damage and break transitions were checked for the normal and Nihilla forms using a temporary development lock-on. Natural recovery was also observed for Nihilla after the temporary unlock was removed. Natural reselection and the HUD after recovery remain unverified; see [lock-on and part state](lock-on-and-part-state.en.md).

## Reproducing the public source

The repository contains C# source, project files, test source, and mod configuration, but excludes extracted game originals, the custom UI resources, some validation data, and raw captures. It cannot reproduce the complete release package or full test suite by itself.

The initial research source and test project compiled with locally installed Reloaded-II assemblies with zero errors and four `NU1900` warnings because the NuGet vulnerability data service was unreachable. Separate local records for the 1.0.0 release candidate report a Release build and 21 regression test groups. Locking on to Angra Mainyu's head and opening and closing the menu were also checked after installing the release ZIP. Those results used local data and already installed dependencies; they do not establish standalone reproduction from the public repository.

A test run with the public folder as its working directory stopped because `resource_validation.json`, required for percentage-resource validation, was missing. Subsequent tests were not recorded as complete. Tests requiring the custom Angra atlas also cannot be completed with the public material alone.

See the [project](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/PartIndicator.csproj), [test entry point](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/Tests/Program.cs), [percentage tests](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/Tests/PercentageTextTest.cs), and [custom resource tests](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/Tests/HeadAssetsTest.cs).

## Rechecking after a game update

1. Record the executable hash, image size, and PE version strings again. The same displayed version does not establish an identical binary.
2. Recheck function entry and interior hook points, instruction boundaries, calling conventions, RTTI, and constructor tables. A short matching byte prefix does not prove unchanged semantics.
3. Recheck scene handles, local actor ownership, and the relationships among selection, current value, initial value, and processed break state.
4. Recheck UI timing, lock order, resource replacement, final render references, and destruction paths.
5. Verify representative natural damage, break, recovery, lock-on changes, and scene transitions before stating support. Do not remove the build guard merely to bypass a failed check.

[BuildGuard](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/BuildGuard.cs) and [NativeBindings](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeBindings.cs) implement build identification and function-prefix checks. These are not a signature scanner that guarantees safety on another build.
