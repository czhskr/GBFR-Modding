# Part durability and damage observations

[한국어](part-durability-observations.md) · [Research overview](README.en.md) · [Lock-on and part state](lock-on-and-part-state.en.md)

This note records samples of initial boss-part durability and processing paths identified by static analysis. Values are from specific encounters or configurations, not fixed values or formulas for all quests, difficulties, or variants. The target is Windows x64 `granblue_fantasy_relink.exe`, PE version `2.0.6`, SHA-256 `4b3ace0fd03df5d3d08c6453e12df2279c6259abf7d2fb3622921fcbea775145`.

## Crocodile-family samples

**Runtime observation:** In one Stone-dile `Em0500` encounter, undamaged current values were `1500` for the horn and `1200` for each arm.

**Runtime observation:** In one Blizzardile `Em0501` sample, normal parameter values `[1500, 1200, 1200]` matched the current values at the start of the encounter. The alternative parameter was `[5000, 5000, 5000]`, but that path was not selected. These values are not established for every quest.

**Static finding and runtime observation:** In samples from this family, the three current values corresponded to integers at actor offsets `+0x2A98`, `+0x2A9C`, and `+0x2AA0`. Lock-on selection ID `9` mapped to right-arm state index `1`, and a right-arm hit sample used damage mask `0x4`. A different attack while locked to the right arm produced the left-arm mask `0x8`. The selected target and the part actually hit must not be treated as identical.

## Additional boss samples

The following are individual samples from the development records, not values to apply to every quest, variant, or form with the same name. **Runtime current values**, **configuration values**, and **HUD observations** are distinct evidence; confirming a value does not establish natural break or recovery for that part.

- **Stone-dile, Blizzardile, Infernadile:** Normal samples show horn `1500`, each arm `1200`. One Blizzardile variant sample used an alternate path with `5000` for each of the three parts.
- **Ancient Dragon and elemental wyrms:** Normal head configuration is `1500`. Alternate configuration is `3000` for Ancient, fire, ice, earth, and wind, and `6000` for light and dark.
- **Behemoth and Ennugi:** Head initial value `14000` was observed.
- **Pyet-A:** Normal samples show head and each arm `3300`, each leg `6000`, body `3000`, and tail `5000`. Two named variant samples used alternate values of `9000`, `14500`, `7600`, and `11000` in the same order.
- **Furycane and Nihilla:** Quadruped samples show each leg `400`, each arm `600`, and head `500`.
- **Vulkan Bolla:** Samples show each arm `250`, and mechanic selection IDs `3840` and `282` at `350` each. Recovery after reactivation of the mechanic points remains unverified.
- **Angra Mainyu:** Samples show two horns connected to the head selection, each `800` in modes `0/1` and each `1500` in mode `2`.
- **Tayu'itar:** Normal and Mk II leg samples show initial `140`, first recovery `200`, and second recovery `300`. Later cycles must not automatically use the first reference.
- **Proto Bahamut:** Head `17000` was observed. Two Chaos++ quests had the same initial value.

## Damage conditions and exceptions

**Static finding:** The inspected Stone-dile part-damage path compares the attack mask with part-specific masks, subtracts a computed value from current durability, and clamps it at zero. Some conditions impose a minimum of 1 instead. The complete inputs and formula of the called damage calculation function are not established here.

**Static finding:** Actor `+0x14E0` is used in a condition that gates entry into the part-damage path above. It is not established that this field exclusively represents one battle state in all situations.

## Unverified stun and calculation relationships

The part-durability samples do not establish how an attack's stun value affects part durability, whether the two values are equivalent, or a general formula deriving part damage from body damage. Further tests must compare attack-specific values and actual part loss in the same sample while accounting for state, difficulty, and damage restrictions.

**Configuration observation:** Furycane and Nihilla configuration includes an entry named `stunDamageRate = 1.5`. The runtime consumer of that entry and changes to the stun gauge before and after the same hit have not been established. This value alone does not show that `1.5` is applied to part damage or that stun and part damage are equivalent.

The mod's percentage divides current durability by a trustworthy reference when available. This is a display policy, not the game's damage or stun formula. See [reference values and recovery](lock-on-and-part-state.en.md#reference-values-and-recovery) for reference values, recovery cycles, and the distinction between zero and completed break processing.
