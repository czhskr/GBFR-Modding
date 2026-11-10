# Part durability and damage observations

[한국어](part-durability-observations.md) · [Research overview](README.en.md) · [Lock-on and part state](lock-on-and-part-state.en.md)

This note records samples of initial boss-part durability and processing paths identified by static analysis. Values are from specific encounters or configurations, not fixed values or formulas for all quests, difficulties, or variants. The target is Windows x64 `granblue_fantasy_relink.exe`, PE version `2.0.6`, SHA-256 `4b3ace0fd03df5d3d08c6453e12df2279c6259abf7d2fb3622921fcbea775145`.

## Crocodile-family samples

**Runtime observation:** In one Quakadile `Em0500` encounter, undamaged current values were `1500` for the horn and `1200` for each arm.

**Runtime observation:** In one Blizzadile `Em0501` sample, normal parameter values `[1500, 1200, 1200]` matched the current values at the start of the encounter. The alternative parameter was `[5000, 5000, 5000]`, but that path was not selected. These values are not established for every quest.

**Static finding and runtime observation:** In samples from this family, the three current values corresponded to integers at actor offsets `+0x2A98`, `+0x2A9C`, and `+0x2AA0`. Lock-on selection ID `9` mapped to right-arm state index `1`, and a right-arm hit sample used damage mask `0x4`. A different attack while locked to the right arm produced the left-arm mask `0x8`. The selected target and the part actually hit must not be treated as identical.

## Additional boss samples

The following are individual samples from the development records, not values to apply to every quest, variant, or form with the same name. **Runtime** means current values read from game memory, **configuration** means parameter values or stored initial values, and **HUD** means an on-screen display was checked. A HUD observation for one sample does not establish it for other parts or variants. Confirming a value does not establish natural break behavior.

- **Quakadile, Blizzadile, Infernadile:** Horn `1500`, each arm `1200`. **Runtime and HUD:** undamaged parts on all three. The Blizzadile sample also matched normal configuration. **Frosthelm Ragnalia:** each of the three parts `5000`; **runtime, alternate configuration, and HUD** were compared.
- **Ancient Dragon and elemental wyrms:** Head `1500` was checked against **configuration, runtime, and HUD**. **Alternate configuration:** Ancient, fire, ice, earth, and wind `3000`; white and black `6000`. **Runtime** checks covered fire and wind at `3000`, and white and Endbringer Ragnalia at `6000`. The remaining alternate configuration values are not generalized as runtime observations.
- **Behemoth, Ennugi, and Gorefiend Ragnalia:** Head `14000`. **Runtime, configuration, and HUD:** checked in each sample.
- **Pyet-A:** Head and each arm `3300`, each leg `6000`, body `3000`, tail `5000`. **Runtime, stored initial values, and HUD:** all seven parts were checked. **Pyet-A, Blood Moon and Moonfall Ragnalia:** `9000`, `14500`, `7600`, `11000` for the same part groups, respectively. In both samples, all seven **runtime values, stored initial values, and alternate configuration** matched. Separate **HUD** checks covered only the head of each sample.
- **Furycane and Furycane Nihilla:** Quadruped legs `400` each, arms `600` each, head `500`. **Runtime and stored initial values:** checked in both forms. Head **HUD** checks used a temporary development lock-on.
- **Vulkan Bolla:** Arms `250` each. **Runtime, stored initial values, and HUD:** initial and post-submerge recovery samples were checked. Mechanic selection IDs `3840` and `282` were `350` each; **runtime, stored initial values, and HUD** were checked. Recovery after reactivation of these two points remains unverified.
- **Angra Mainyu:** Both horns `800` each in modes `0/1`, and `1500` each in mode `2`. **Runtime, stored initial values, and configuration:** compared in form-specific samples.
- **Tayu'itar and Tayu'itar Mk II:** Initial leg `140` was checked with **runtime and HUD**; first recovery `200` with **runtime**. For second recovery `300`, the normal sample established the cycle maximum and 100% **HUD**; Mk II established post-recovery **runtime and HUD**. Another Mk II stage showed `150`, so `300` is not generalized to every recovery state.
- **Proto Bahamut and Primeval Proto Bahamut:** Head `17000` is an **initial runtime value**. Primeval was read in two quests; **HUD** was checked in one.

## Damage conditions and exceptions

**Static finding:** The inspected Quakadile part-damage path compares the attack mask with part-specific masks, subtracts a computed value from current durability, and clamps it at zero. Some conditions impose a minimum of 1 instead. The complete inputs and formula of the called damage calculation function are not established here.

**Static finding:** Actor `+0x14E0` is used in a condition that gates entry into the part-damage path above. It is not established that this field exclusively represents one battle state in all situations.

## Unverified stun and calculation relationships

The part-durability samples do not establish how an attack's stun value affects part durability, whether the two values are equivalent, or a general formula deriving part damage from body damage. Further tests must compare attack-specific values and actual part loss in the same sample while accounting for state, difficulty, and damage restrictions.

**Configuration observation:** Furycane and Furycane Nihilla configuration includes an entry named `stunDamageRate = 1.5`. The runtime consumer of that entry and changes to the stun gauge before and after the same hit have not been established. This value alone does not show that `1.5` is applied to part damage or that stun and part damage are equivalent.

The mod's percentage divides current durability by a trustworthy reference when available. This is a display policy, not the game's damage or stun formula. See [reference values and recovery](lock-on-and-part-state.en.md#reference-values-and-recovery) for reference values, recovery cycles, and the distinction between zero and completed break processing.
