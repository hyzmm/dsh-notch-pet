# DSH Notch Pet

A macOS notch companion for DSH. It peeks out of the hardware notch, reacts to the live agent state, and expands on hover to show the focused session's activity, elapsed time, task progress, and goal progress.

![Collapsed notch pet](docs/notch-pet-collapsed.png)

![Expanded notch pet](docs/notch-pet-expanded.png)

## Architecture

- `index.js` is the Host half. It folds live `session/event`, `agent/status`, `goal/changed`, and `agent/error` events into a small per-session state, then writes newline-delimited JSON to a private Unix socket.
- `native/DSHNotchPet.app` is a menu-less AppKit/SwiftUI panel positioned at the top center of the notched display. It reads the socket, draws the pet, and animates progress.
- The Host kills the panel when the plugin unloads. The panel also exits about 20 seconds after the socket disappears, so a crashed Host does not leave an orphan overlay.

The socket lives at `~/Library/Application Support/DSHNotchPet/pet-<pid>.sock` with mode `0600`. `current.json` in the same directory names the live socket for diagnostics.

## Build the native app

```sh
chmod +x native/build.sh
./native/build.sh
```

The script builds an ad-hoc-signed universal binary at `native/DSHNotchPet.app`. Re-run it after changing Swift sources. When DSH is already running, disable and re-enable the plugin to relaunch the panel with the new binary.

Logs:

- Host lifecycle: `~/Library/Logs/DSHNotchPet-host.log`
- Native panel: `~/Library/Logs/DSHNotchPet.log`

## Progress model

The pet shows the most recently active root session. A subagent-heavy run still contributes to the active count. The compact bar shows the current tool and turn time. The expanded panel shows:

- session title and activity
- current tool and elapsed turn time
- `todo_write` completion, falling back to goal rounds
- the current todo, goal objective, or last error

No percentage is invented when DSH has no countable unit. The bar stays indeterminate while the agent is active. When the agent is idle, the indeterminate bar is hidden and any determinate bar is frozen, so the notch does not keep flashing.

## Controls

- Hover: expand
- Click the pet: pin or unpin the panel
- Open DSH: focuses the running DSH desktop app; falls back to the web origin only when the desktop app is not installed
- Right click: open DSH, pin, or quit the pet

## Requirements

- macOS 14 or newer
- DSH Desktop or a DSH Host running inside a macOS GUI session
- Apple Silicon or Intel (the app is universal)
