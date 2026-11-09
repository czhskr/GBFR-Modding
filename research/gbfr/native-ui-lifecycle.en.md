# Native UI: ownership, execution context, and cleanup

[한국어](native-ui-lifecycle.md) · [Research overview](README.en.md)

This note describes an effect-free Image path in the existing UI engine and the mod implementation using it. It is not a general creation API for all UI components or rendering paths. The overview defines the target build and evidence labels.

## Object, component, and manager ownership

**Static finding:** Object factories and unattached Image factories use owning wrappers. In an observed attach path, ownership transfers into the Object's component array and the original wrapper pointer is cleared. Attached Canvas/Image lifetimes are connected to the Object's release path; a separately stored component address does not itself grant additional ownership.

The UI owner's staging map at `+0x220` and live map at `+0x1E0` both hold ownership. The observed native handoff moves the same node under the owner's `+0x260` lock; it is not evidence of cloning the Object or wrapper. A staging node left by a duplicate key may be cleaned up through its wrapper, so duplicate registration is not assumed to be a harmless retry.

Parent/child hierarchy attachment, registration with a manager, and inclusion in the active Canvas list are separate relationships. None guarantees the other two. Image's ancestor CanvasGroup reference at `+0x108` must also be distinguished from the actual rendering Canvas.

**Implementation policy:** Only mod-owned Objects are registered in the live map, and only the mod's components and commands are tracked. A base-class vtable briefly assigned inside a constructor or during a destructor is not enough to establish the final derived object's member layout.

## Why a fixed UI thread or global stop point cannot be assumed

**Runtime observation:** Samples show that UI creation, controller updates, and destruction cannot be assumed to run on a single fixed thread. Even after a UI work join, where a set of UI tasks has completed, resource work and resource-mutex ownership overlapped. Native destruction also overlapped other rendering work.

Being after a UI update does not imply that all UI, resource, and render work has stopped. An intermediate unlock of a recursive mutex is not proof of final release or completion of all work.

**Implementation policy:** The service callback combines these conditions:

1. Avoid reentry and tracked UI, render, resource, and Object-destructor intervals.
2. Check mutex type `0x102` at resource manager `+0x8E8` and attempt a native try-lock once. In the observed path, return `3` means defer and `0` means success; unlock only after a successful lock.
3. Attempt the UI owner's `+0x260` lock once via CAS. If it fails, defer that invocation's work.
4. During component creation and resource binding, the owner lock may be released while the resource lock remains held; the owner lock is checked again before publication.

This lock order and active-interval tracking are contracts of this implementation, not evidence that the owner lock alone protects every native hierarchy operation.

## Read, hook, and call sites

All addresses below are **RVAs** for the target executable. Role names come from the public C# bindings and are not official symbols.

The service bridge attaches at the **interior instruction** `0x1CBD6F`, not a normal function entry. It preserves integer registers, FLAGS, legacy FX state, and Win64 stack/shadow space. There is no evidence that it can be reused at an arbitrary site where upper YMM lanes are live.

Entry hooks used to observe execution and lifetime:

- UI update `0x2136FA0`, render path `0x1A97D0`, resource work `0x8D78D0`.
- Object destructor `0x2135180`, Canvas release `0xBDF5E0`, Image destructor `0x3342DB0`.
- Image render `0x3342FB0`, Image command creation `0x3345060`, command consumer `0x43EF740`.
- Lock-on controller update `0x26939D0`.

Separate native paths called for creation, registration, and deletion include Object `0x26181C0`, Canvas `0xBDF3A0`, Image `0x3340DC0`, attach `0x2618DA0`, initial Image binding `0x3345D60`, owning-map insert `0x2520180`, and deletion request `0x261CD70`. Addresses alone do not provide correct arguments, ownership, or execution context.

The custom atlas request entry hook is `0x25374A0`; the readiness-check call is `0x2539460`. See [NativeBindings](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeBindings.cs) for expected byte prefixes and delegates, [GameHooks](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/GameHooks.cs) for the hook set, and [NativeServiceBridge](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeServiceBridge.cs) for interior-bridge limitations.

**Static finding:** In Tayu'itar's common recovery function, the instruction immediately following the fourth leg's current-value write is at RVA `0x2D35B84`. At that point RSI holds the object and EAX the value calculated by the game and written to the legs.

**Implementation policy:** An optional interior hook reads this value and checks the supported build, instruction bytes, and object/cycle relationship. For later recovery cycles that rely on this hook, the leg percentage is hidden if a trustworthy reference is unavailable. The initial cycle and first recovery use separate reference-value paths, described in [Tayu'itar leg recovery cycles](lock-on-and-part-state.en.md#tayuitar-leg-recovery-cycles). The register state at this point must not be generalized to another instruction site.

## Render-command references and destruction

**Static finding:** In the inspected command producer, the material reference at `command+0x248` and effect-cache payload at `command+0x290` have different ownership behavior. Retaining a material is not proof that the effect payload is also retained. The inspected `+0x290` paths include a borrowed cache payload.

**Implementation policy:** The mod restricts its plain Image commands to the path where `+0x290 == 0`, and separately tracks each command from creation until its consumer returns. This cleanup policy is not generalized to arbitrary Images with effects.

Cleanup follows this sequence:

1. Disable the mod-owned Object to stop new display work.
2. Wait for at least three UI/render returns and an interval since recent Image use. This is an implementation policy, not a standalone fence.
3. Confirm that no tracked commands or active consumers remain, and that the Object has left the owner's Canvas list `+0x2F8..+0x300` and graphics caches `+0x298` and `+0x478`.
4. Request native deferred deletion. Free auxiliary wrapper memory only after Object/Canvas/Image destructors complete and the live map no longer contains the Object.

The absence of the mod's own key and components matters more than the overall manager count, since other game UI can be created meanwhile. The last consumer observed here is a CPU command-consumer return, not a verified GPU-completion fence. See [IndicatorRuntime](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/IndicatorRuntime.cs) and [NativeHud](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeHud.cs) for `BeginRelease`, `PollRelease`, and `Enter/Leave`. Hiding, retirement, and completed destruction are distinct states.

## Lock-on ring coordinates and screen conversion

The controller identified by RTTI as `ui::component::ControllerCursorLockOn` resolves a root whose screen coordinates are at `+0x150/+0x154`. The conversion uses the UI owner's scale and logical dimensions:

```text
x = pixelX / sx - width / 2
y = pixelY / sy + height / 2
```

**Implementation policy:** First check that the source belongs to the same completed UI frame and resource epoch and that its Object remains active in the live map. Only valid scale and dimensions and finite coordinates are accepted. Camera following is not evidence for screen-center-fixed coordinates.

**Runtime observation:** Limited samples compare native quad and screen coordinates; in-game checks cover lock-on following, loss, reconnection, and scene transitions. Vertex error and frame counts from an early native prototype are not attributed to a different C# run. GPU display latency was not measured.

Object size alone does not determine the final sprite quad: atlas trim/padding and UV conversion also matter. The implementation normalizes padding only on Images it owns and does not change original game Image fields. See [HudCoordinates](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/HudCoordinates.cs).

## Resource request context and readiness

The UI owner global `RVA 0x7C01858` and atlas-request context global `RVA 0x7C17858` are distinct; one must not be substituted for the other.

**Implementation policy:** The custom atlas is requested once in a context checked against the original loader callback's context and flags and current-thread resource-mutex ownership. A non-null material alone does not establish readiness: metadata, UVs, material identity, and native readiness are checked. A change in the tracked resource generation (resource epoch) or material invalidates readiness.

Even if the resource epoch changes, an already validated identical material is revalidated at the first safe UI frame. A replaced or not-yet-ready material stays hidden until validation succeeds. See [NativeHeadAssets](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeHeadAssets.cs).

**Runtime observation and limit:** After installing the release ZIP, opening and closing the menu while locked on to Angra Mainyu's head produced no observed indicator flicker. This observation is limited to that menu-transition scenario.

Atlas requests and hook/delegate lifetimes are retained until process exit. Suspending the module requests UI cleanup; it is not equivalent to completely unloading the module. No hot unregister of the custom atlas is claimed.

## Unverified scope and update checks

The observed plain-Image ownership, creation, consumption, and cleanup paths and tested UI transitions are limited. They do not guarantee the lifetime of all effects/caches, GPU completion, relative order at equal sort keys, 3D depth occlusion, arbitrary address reuse, long-running operation, online play, or actual module unload.

After an update, recheck RVAs and instruction boundaries as well as wrapper move semantics, map-node handoff and duplicate handling, ownership after component attachment, thread context and lock order, command-payload references, cache removal after consumption, destructor order, and atlas request/readiness contracts. Retaining a three-frame wait or matching a byte prefix alone does not establish that the rest of the contract is unchanged.
