# Lock-on target identification and part-break state

[한국어](lock-on-and-part-state.md) · [Research overview](README.en.md)

The lock-on selection ID, the part actually hit, durability state, and completed break processing are separate relationships. There is no evidence for applying one common part-HP array to every boss. The target build and evidence labels are defined in the overview.

## Identifying the controlled actor and target

**Runtime observation and implementation policy:** The first entry in the player registry cannot be assumed to be the controlled character when AI companions are present. The implementation finds the entry matching the engine's local controlled actor and checks that its owner chain returns to the local scene. Offline cases with AI companions were observed; online ownership is unverified.

**Implementation policy:** Part-state reads and display are disabled in sessions identified as online. This restriction does not establish other online compatibility or safety properties.

The implementation reads the following globals for this build; addresses are RVAs:

- Local scene handle: ID at `0x7035CA0`, scene pointer at `0x7035CA8`, stamp at `0x7035CB0`.
- Local controlled actor: `0x7035D00`.
- Scene manager: `0x7020988`; manager `+0x20` points to the scene-slot array and `+0x48` to the stamp array.
- Player registry range: `0x7362690..0x7362698`.
- Registry entry `+0x230` is compared with the controlled actor and its ownership is checked. No match or multiple matches cause the read to be rejected.
- The validated entry's `+0x108` supplies the target scene and `+0x118` the selection ID.

Handle validation compares more than the ID: the scene and stamp in slot `ID-1`, and the scene's `+0x38` ID, `+0x40` self pointer, `+0x48` stamp, and `+0x70` actor. It also rechecks that the target and source relationships did not change during the read.

**Interpretation:** These checks reject address reuse and target changes during observation; they do not guarantee an atomic snapshot of every field. Clearing initial-value proof and display state when the world or display generation changes is a mod policy. [NativeStateReader](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeStateReader.cs) implements these relationships. It reads memory rather than accumulating the damage from every attack callback.

## Selection, state, and hit identifiers

**Static findings and runtime observations:** A lock ID, lock-vector row, state-array index, collision identifier, and damage mask are not the same numbering scheme.

- For crocodile types `Em0500/0501/0502`, selection IDs `2628`, `9`, and `13` map to state indices `0`, `1`, and `2`.
- For Pyet-A, selection `772` maps to state `6`, and selection `901` to state `0`.
- Quadruped Furycane selection `5` has a static path and selection sample connecting it to state `8`. Actual head durability decrease and break transitions for normal and Nihilla forms were observed using a temporary development lock-on. The static damage mask `0x1` alone does not establish that decrease.

One part's state can decrease while another remains locked on. Visual reach, incoming-hit records, and state decrease must be distinguished; multiple collision rows can also feed one state. The implementation treats the lock vector at `actor+0x788..+0x790` as 48-byte rows. Row selection IDs and the active byte at `+0x29` help identify temporarily selectable parts. An ID is not assumed to be unique across all types.

### Interpreting selection ID `-1`

**Research observation:** Excavallion's reactor `Em7401` had a valid target and exactly one active lock row, both with ID `-1`. A stamped parent handle resolved to an exact `Em7400` object. Thus selection ID alone cannot universally identify a lost target.

The reactor's own ExHp matched a path reading current qword at `actor+0x160` and maximum qword at `actor+0x168`. The sample did not observe completed break, reset, or the origin of initialization arguments. This is a separate research observation, not an example of the mod's ordinary part array; the existing HP bar already represents its break progress, so an additional indicator was not added. It does not establish the UI or break meaning of every Excavallion child type.

## Zero current value and completed break

**Static findings and runtime observations:** Some bosses have separate processed bytes or break-history records. Tiny positive floats, current reaching zero before a processed marker, a value held at one by protection, and initialization writing zero must be distinguished.

**Implementation policy and observed scope:**

- **Crocodiles, dragons, Behemoth:** Current value and break history are separate, and changes in parameter, stamp, or break state trigger reevaluation of the reference value. Head display, decrease after damage, and retargeting were checked for six high-difficulty dragons; natural break was checked only in representative samples.
- **Pyet-A:** Seven selection/state relationships are handled. Current and processing history are considered together; zero without history does not immediately become a broken icon. A representative natural tail break does not validate every part and mode or recovery.
- **Furycane:** Quadruped and humanoid layouts are separate. Humanoid armor and quadruped leg break/recovery, and some quadruped arm damage/recovery, were observed. A temporary development lock-on confirmed normal and Nihilla head break transition. Natural recovery after removing the temporary unlock was observed for Nihilla. Natural reselection, HUD after recovery, and every selected arm's break display remain unverified.
- **Tayu'itar head unit:** Current value, its initial-value source, and a processed marker are combined; zero alone does not mean break processing is complete. Leg-cycle handling is described below.
- **Proto Bahamut:** Selection `2048` combines an integer state with initial value and processing history from the same owner. Two Primeval Proto Bahamut quests yielded an initial value of `17,000`; HUD was checked in one. Protection can hold the value at one, and initialization zero is distinguished from a processed break. Persistent state before and after processing and the actual broken display were observed, but the exact order of all intermediate frames was not measured.
- **Angra Mainyu:** One head selection `5` maps to two horn states: states 0/1 in modes 0/1 and 2/3 in mode 2. One horn's damage or break must not be combined with the other's. Both processed breaks and the final display were observed in the later form, but not every individual hit's left/right screen transition.
- **Vulkan Bolla:** Float current/initial values are read from shell objects after parent/child handle, part definition, and model relationship checks. Zero is interpreted together with the native processing path. Both arms were observed recovering after breaking and a submerge/resurface sequence. Special selections `3840` and `282` were observed independently taking damage, reaching zero, and deactivating; initial values and display restoration after reactivation remain unverified.

See [PartStatePolicy](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/PartStatePolicy.cs) for selection and display decisions and [NativeStateReader](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeStateReader.cs) for ownership, fields, and reference-value checks. This is not an exhaustive list of breakable parts in the game.

## Reference values and recovery

The current value when observation begins is not necessarily the maximum: the part may already be damaged, or difficulty, form, protection, or another mod may affect it. A value resembling HP in a parameter is not by itself proof that it is the runtime denominator for that part.

**Implementation policy:** A percentage uses `current / reference` only with a verified positive reference, either the initial value or the verified maximum for the current recovery cycle. The fractional part is discarded; a positive current that would truncate to zero is displayed as at least 1%. A completed processed break hides the percentage. These are UI policies, not definitions of the game's internal durability.

Proof is tied to the current world/generation, scene, actor, ID, stamp, type, and parameter relationship. A single global denominator is not reused across monsters, and the same address with a different stamp or state relationship is treated as a different instance. Recovery requires distinguishing restored current value, cleared processed flags, and temporary lock availability; remembering only that a part broke in the past is insufficient.

### Difficulty and reference selection

**Implementation policy:** Crocodile, dragon, and Pyet-A readers choose normal or alternate parameter paths according to actor flags and execution mode. Even with flag `0x40`, mode `1` and submode below `3` select the normal path. The crocodile path also checks a parameter flag indicating whether alternate values are enabled. Mode, parameter, and target relationships are rechecked around reads; the percentage is hidden if the reference cannot be established. These rules apply only to the type-specific paths in [NativeStateReader](https://github.com/czhskr/GBFR-Modding/blob/ef42ea2987883b69ed50ef9a17f72b838a95948e/Part-Break-Indicator/NativeStateReader.cs).

**Runtime observation:** Head display, decrease after damage, and retargeting were checked for six high-difficulty dragons. The two Primeval Proto Bahamut quests and single HUD check described above do not establish natural break behavior for every difficulty or variant.

### Tayu'itar leg recovery cycles

**Static finding and runtime observation:** In one normal-form sample, leg current reset to `200` while the stored initial value remained `140`. The original stored value therefore cannot be assumed to be the maximum for every later recovery cycle. HUD showed 100% immediately after the second recovery in both normal and Mk II samples.

**Implementation policy:** Normal and Mk II legs use the stored value for the initial cycle. For the first recovery, a configured candidate is used only when one valid candidate explains the observations. For later recoveries, a maximum captured by an optional hook is checked against the same object, stamp, recovery cycle, and four leg states. If no reliable reference is available, only the icon is shown. Carnage legs are excluded from the indicator; the head unit is handled separately.

**Unverified:** In-game display from the third recovery onward has not been checked. Do not generalize one sample's recovery multipliers to other objects or branches. The capture point and registers are documented in [native UI and hooks](native-ui-lifecycle.en.md#read-hook-and-call-sites).

## Open questions and update checks

The connection between physical states and directly lockable parts of Rock Golem and Bahamut Versa remains unresolved; this is not a conclusion that their parts cannot break. Entries in parameters, reward tables, or factory aliases alone do not establish all accessible bosses and breakable parts. The mod displays directly lockable supported parts; other parts, such as bodies, may still be breakable in the game.

After an update, recheck the local actor's writers/consumers and ownership chain, handle/stamp reuse, exact derived types, parent/child relationships, lock availability and state mappings, difficulty/form-specific initialization, and processed break, protection, and recovery paths. Do not generalize a base layout observed at a common destructor to every derived boss.
