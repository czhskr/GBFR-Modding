# Part Break Indicator

[한국어](README.ko.md)

Shows the break status of supported boss parts when locked on. When a reliable reference value is available, it also displays remaining durability as a percentage.

The mod reads durability directly from the game and updates the indicator as supported parts take damage, break, or recover. For details, see [Features and limitations](FEATURES.md).

## Compatibility and installation

Developed and tested on the Steam PC version of Granblue Fantasy: Relink – Endless Ragnarok, game version 2.0.6. The indicator is available in offline solo play and with AI party members. It is disabled during online sessions because online behavior has not yet been fully verified. Online support may be considered in a future update.

1. Install [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II) and the [Endless Ragnarok Mod Loader](https://www.nexusmods.com/granbluefantasyrelink/mods/526).
2. Close the game, install the mod ZIP through Reloaded-II, and enable the mod and its dependencies.
3. Launch the game through Reloaded-II.

Close the game before removing or fully disabling the mod. Full unloading during gameplay is not supported.

Download the mod and see the supported boss list on [Nexus Mods](https://www.nexusmods.com/granbluefantasyrelink/mods/893).

## Source

Version 1.0.1 includes a startup safety fix that prevents resuming the indicator if required initialization fails. The fix was tested in isolation and in-game using a diagnostic build based on the same implementation.

This repository preserves the source and tests but excludes development assets and test data. Reloaded-II dependencies and additional local files are required to build and run all tests; the repository alone cannot reproduce the complete mod package.

## Development and AI disclosure

I developed this mod with assistance from Codex. I defined the goals and requirements, provided existing research and the development environment, guided development, and performed in-game testing. Using the REA toolkit, Codex carried out reverse engineering and runtime analysis and wrote most of the code. We reviewed observations and test results together.
