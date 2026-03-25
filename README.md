# vx-trigger

A lightweight Windows system tray application that fires triggers to notify swing recording software when a shot is detected. Compatability with golf simulator software that supports the GSPro OpenConnect API or ProTee Labs using log file monitoring.

## How It Works

vx-trigger detects shots from ProTee Labs using one of two methods:

- **Folder Watcher** — Monitors `%APPDATA%\Roaming\ProTeeUnited\Shots` for new shot directories created by ProTee Labs.
- **OpenConnect Listener** — Listens on a TCP port (default 921) for shot data sent directly from ProTee Labs via the OpenConnect protocol. This method can offer faster shot detection since data arrives over the network before it is written to disk. Do not use this mode alongside GSPro or Infinite Tees, as only one application can listen on the OpenConnect port at a time.

When a shot is detected, vx-trigger fires a configured trigger to notify swing recording software.

## Trigger Types

**Audio Trigger** — Plays a synthetic golf impact sound through a selected audio output device. The sound envelope (frequency, noise/tone decay, mix, duration) is configurable. Have tested with SwingCatalyst.

Download and install: [VB-CABLE](https://vb-audio.com/Cable/index.htm)

1. Select CABLE Input (VB-Audio Virtual Cable) as the output source in vx-trigger.
2. In your recording software, set the trigger input to CABLE Output (VB-Audio Virtual Cable). Adjust the threshold so the test sound triggers a swing recording.

**Network Trigger (UDP)** — Sends a UDP packet to a configured host and port. Compatible with [Kinovea 2025.1](https://www.kinovea.org/) and any other recording software that supports UDP trigger input.

## Requirements

- Windows 10/11
- [ProTee Labs](https://proteelaunchmonitors.com/) launch monitor software

## Installation

Download `vx-trigger.exe` from the [releases page](../../releases) and run it. No installer, no .NET runtime required — it's a self-contained executable.

The app will open the configuration window on first launch if no trigger has been configured yet.

## Usage

1. Run `vx-trigger.exe` — a colored circle appears in the system tray
2. Double-click the tray icon (or right-click → **Configure...**) to open settings
3. Choose a **shot detection** method:
   - **Folder Watcher** — set the shots directory (default: `%APPDATA%\Roaming\ProTeeUnited\Shots`)
   - **OpenConnect Listener** — set the listen port (default: 921). Configure ProTee Labs to send to this port.
4. Choose a trigger type and configure it, then click **Save**
5. The tray icon color reflects the current state (see below)

### Tray Icon Colors

| Color | Meaning |
|-------|---------|
| Green | Actively monitoring (Folder Watcher) or ProTee Labs connected (OpenConnect) |
| Orange | OpenConnect listener running, waiting for ProTee Labs to connect |
| Yellow | Configured but stopped |
| Gray | Not configured |

### Settings

Settings are saved to `Documents\vx-trigger\settings.json`

