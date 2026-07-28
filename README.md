# SpawnProtection

EXILED plugin for SCP: Secret Laboratory that protects newly spawned non-SCP players.

## Features

- Configurable full damage immunity after spawn
- Full immunity ends when the protected player damages another player
- Configurable team-damage protection after spawn
- Supports all non-SCP playable roles, including respawn waves and Tutorial
- SCP, Spectator and Overwatch roles are excluded
- Small configurable on-screen timer
- Public API for other plugins

## Default behaviour

- 15 seconds of full immunity
- Full immunity ends immediately after dealing damage
- 60 seconds of team-damage protection from the moment of spawn

## Build

The project targets .NET Framework 4.8 and uses `ExMod.Exiled` 9.14.2.

GitHub Actions builds the plugin automatically. Download `SpawnProtection.dll` from the workflow artifact and place it in the EXILED Plugins folder.

<!-- Temporary write-access test -->
