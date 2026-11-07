# Unread Fate Episode Skip Unlocker

Skip unread Fate Episode dialogue using the game's usual Skip button.

[한국어](README.ko.md)

**Last tested:** November 7, 2026 — game version 2.0.6.

## Safety & Compatibility

- Changes only the conditions needed to enable Skip for unread Fate Episode dialogue.
- Uses the game's existing Skip and completion flow without directly editing save files or forcing rewards.
- Checks the game executable and target function before applying the hook. If validation fails, the hook is not installed.
- Leaves the original result unchanged when the conditions for applying the mod are not met.

## Testing & Limitations

Developed and tested on game version 2.0.6. Most Fate Episodes have been tested across a variety of scenarios, including:

- Unread and previously completed episodes, across multiple characters
- Skipping during dialogue and in episodes that include battles
- Completion rewards and saved progress after restarting the game
- Normal game behavior after disabling the mod

Pre-battle dialogue can be skipped, but battles and post-battle dialogue cannot currently be skipped.

## Planned Updates

Work on skipping post-battle dialogue will resume after my current mod project is complete.

## Requirements

- Granblue Fantasy: Relink
- [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II)
- [Endless Ragnarok Mod Loader](https://www.nexusmods.com/granbluefantasyrelink/mods/526)

## Downloads

[Download on Nexus Mods](https://www.nexusmods.com/granbluefantasyrelink/mods/890) ·
[GitHub Releases](https://github.com/czhskr/GBFR-Modding/releases)

## Development and AI Disclosure

I developed this mod with assistance from Codex. I defined the goals and
requirements, provided existing research and the development environment,
guided development, and tested the mod in-game.

Using the REA toolkit, Codex carried out reverse engineering and runtime
analysis, identified the conditions related to the Skip option, and wrote
most of the mod's code. We reviewed test observations and results together.
