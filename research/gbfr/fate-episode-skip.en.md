# Fate Episode Skip conditions

[한국어](fate-episode-skip.md) · [Research overview](README.en.md)

This note describes two conditions that restrict skipping previously unseen Fate Episode dialogue in the supported build, and how the mod changes their results under specific conditions. It is not a specification of every event or condition in the game. Static findings, runtime observations, and implementation policies are distinguished below; see the [overview](README.en.md) for the build identification and evidence categories.

## Target and environment

- Windows x64 `granblue_fantasy_relink.exe`, PE file version `2.0.6`, SHA-256 `4b3ace0fd03df5d3d08c6453e12df2279c6259abf7d2fb3622921fcbea775145`.
- The evaluation function is at RVA `0x1D53970`. Addresses and offsets below apply to this build only. An RVA is relative to the actual module load base, not a fixed absolute address.
- The findings use static analysis and limited runtime observations. Names describe roles inferred during analysis and are not claimed to be official symbols.

## Observed conditions and existing flow

**Runtime observation:** Condition identifier `0x4EA41E80` is associated with the Skip prompt, while `0x635542C2` is associated with entering the confirmation dialog after input. These are identifiers for conditions, not addresses. Canceling, reopening, and confirming use the existing game flow.

**Implementation policy:** The mod hooks the evaluation function at its entry and calls the original function first. For a target condition, it changes the original result from `3` to `2` only when the other checks also pass. It does not directly write completion flags or call reward functions. Other condition results and the existing confirmation and completion flow remain with the game. The meanings of `2` and `3` are not assumed to be a universal contract for all conditions.

## Conditions for overriding the result

**Implementation policy:** The hook must be enabled and the condition object's vtable must match the target type. The identifier must be one of the two values above, and the object's state and invert fields must both be `0`. A progress record matching a valid current Fate ID must exist; the mod treats it as unread only when the record's `0x02` flag bit is clear. It returns `2` only when the original result was `3`; if any check fails, it keeps the original result.

The target vtable is at RVA `0x5901748` and the progress manager pointer at RVA `0x7C21C58`. In this path, the manager field at `+0x1B38` must be `1`, and the current Fate ID is read at `+0x1B30`. The lookup follows buckets and linked nodes, is limited to 256 nodes, and checks the record ID again. These are the mod's read and rejection policies, not guarantees that every game data lifetime or memory access is safe.

## Build checks and lifetime

**Implementation policy:** The mod checks the executable SHA-256 and module bounds, then compares the first seven bytes of the evaluation function with `56 57 53 48 83 EC 30`. If a check fails or the hook controller is unavailable, it does not install the hook. A short matching byte sequence alone does not guarantee the same meaning in another build.

Suspending the mod disables the result override and leaves the original result in place. This does not imply that a button already on screen is immediately rebuilt. Complete module unloading while the game is running is unsupported so that the managed callback remains alive.

## Verification and limitations

**In-game observation:** Tests recorded for 0.1.0 covered first-time skips in Ferry's prologue and first episode, canceling and reopening the confirmation dialog, completion and unlocking the next episode, and progress after restarting. No additional reward notification appeared when replaying a completed episode, but exact changes in currency and stats were not measured. Release records state that all 17 compiled method and constructor IL bodies in 1.0.0 match those in the tested 0.1.0. Later 1.0.0 logs show loading and both conditions being allowed, but do not include the DLL hash used at the time or completion and reward results.

**Unverified:** These detailed samples do not establish behavior for every episode or reward. Battles and post-battle dialogue are outside the released mod's skip scope. Detailed in-game suspend and resume behavior was also outside the tests above.

## Rechecking after an update

Recheck the executable hash and module bounds, instruction boundaries and original code at the evaluation function, both conditions and result values, and the progress manager and record layouts. Then check representative unread and completed episodes, canceling and confirming the dialog, completion, and progress after a restart. Passing the build checks alone does not establish that condition meanings or object lifetimes are unchanged.
