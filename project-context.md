# HeartRateMonitor - Technical Project Context

This document provides a technical overview of the refactored HeartRateMonitor WPF application. It details the modular architecture, component responsibilities, file storage formats, and key feature implementations.

---

## Directory Structure & Modules

- **[App.xaml](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/App.xaml)** / **[App.xaml.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/App.xaml.cs)**: Main WPF application entry point.
- **[MainWindow.xaml](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/MainWindow.xaml)** / **[MainWindow.xaml.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/MainWindow.xaml.cs)**: Orchestrator UI, bindings, event handlers, and active training lifecycle controller. The main window focuses entirely on live tracking without complex tab systems.
- **[BleHeartRateService.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/BleHeartRateService.cs)**: Handles Bluetooth LE discovery and communication using WinRT APIs. Implements GC protection and Coast Mode.
- **[ChartRenderer.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/ChartRenderer.cs)**: Dedicated helper to render real-time and historical heart rate graphs onto WPF canvas elements, with grid overlays, heart rate zones background coloring, and horizontal pixel-gap downsampling.
- **[FitnessCalculator.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/FitnessCalculator.cs)**: Handles all metabolic calculations (Keytel and MET formulas for calories burned) and defines HR zone bounds, colors, and descriptions.
- **[UserProfile.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/UserProfile.cs)**: Holds user parameters (age, weight, gender), preferences, and last connected BLE device details.
- **[ProfileManager.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/ProfileManager.cs)**: Manages profile creation, deletion, loading, and persistence.
- **[SoundManager.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/SoundManager.cs)**: Synthesizes PCM beep waveforms at runtime, saves them as `.wav` files, and plays them back asynchronously using cached `SoundPlayer` instances.
- **[LiveHeartRateStreamer.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/LiveHeartRateStreamer.cs)**: Exposes real-time telemetry updates via a public static class and events, enabling future HTTP/WebSocket OBS overlays to stream data out of the core app without direct modifications.
- **[HistoryWindow.xaml](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/HistoryWindow.xaml)** / **[HistoryWindow.xaml.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/HistoryWindow.xaml.cs)**: A dedicated, independent window for browsing, analyzing, and exporting historical sessions.
- **[WorkoutSummaryWindow.xaml](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/WorkoutSummaryWindow.xaml)** / **[WorkoutSummaryWindow.xaml.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/WorkoutSummaryWindow.xaml.cs)**: Custom modern dialog presenting comprehensive post-workout summaries.
- **[AppLogger.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/AppLogger.cs)**: Centralized daily rolling text file logger.

---

## Storage & Files

### 1. Profiles (`profiles.json`)
Stores the list of all created user profiles and tracks the active profile.
```json
{
  "Profiles": [
    {
      "Id": "00000000-0000-0000-0000-000000000000",
      "Name": "Tomek",
      "ColorHex": "#FF3B30",
      "LastDeviceAddress": "40:06:A0:6C:C1:2F",
      "LastDeviceName": "Polar H10 923F45",
      "Age": 30,
      "Weight": 75.0,
      "Gender": "Male",
      "ColorIndex": 0,
      "PreferredActivity": "Bieżnia",
      "ZoneGuardEnabled": false,
      "ZoneGuardTargetZone": 3,
      "ZoneGuardCheckIntervalSeconds": 30
    }
  ],
  "ActiveProfileId": "00000000-0000-0000-0000-000000000000"
}
```

### 2. Settings (`settings.json`)
Stores global preferences (like showing grids and scale limits) and persists the ID of the last active profile.
```json
{
  "LastActiveProfileId": "00000000-0000-0000-0000-000000000000",
  "ShowGrid": true,
  "AutoYScale": true,
  "MaxY": 180.0
}
```

### 3. Workout Sessions (`sessions/session_{id}.json`)
Each session document logs full timeline history and final summaries:
```json
{
  "Id": "8a06e90d-271d-4467-bc1a-96ad6b921319",
  "Name": "Sesja Tomek 10:15:30",
  "History": [
    { "Time": 1.0, "Bpm": 72.0 },
    { "Time": 2.0, "Bpm": 75.0 }
  ],
  "AverageBpm": 74.0,
  "MinBpm": 72,
  "MaxBpm": 75,
  "LastBpm": 75,
  "DurationString": "00:00:02",
  "ZoneString": "Strefa: Rozgrzewka (Strefa 1: 95-114 BPM)",
  "StartTime": "2026-06-17T10:15:30",
  "CaloriesBurned": 0.15
}
```

---

## Critical Mechanisms

### 1. UI Thread & Settings Debouncing
To prevent application freezes caused by high-frequency UI events (like dragging a slider), file I/O operations inside `ProfileManager.SaveProfiles` and `MainWindow.SaveGlobalSettings` are debounced using `DispatcherTimer` set to 500ms. This prevents synchronous disk I/O and chart redraw loops from blocking the WPF main thread.

### 2. BLE Stability & GC Protection
WinRT connection drops are prevented by holding strong references to `BluetoothLEDevice`, `GattDeviceService`, and `GattCharacteristic` in a static `_strongGcKeeper` list within `BleHeartRateService.cs` for the duration of the connection.

### 3. Coast Mode (6s Concealment)
Upon an OS-level disconnect or missing heart rate packets for $>1.8$ seconds, the service triggers **Coast Mode**:
- A supervisor timer fires once per second.
- It continues sending simulated heart rate measurements matching the last known BPM.
- The UI displays a warning: "Coast Mode (Słaby sygnał)".
- If a real heart rate packet is received within 6 seconds, Coast Mode is cancelled and status reverts to "Connected".
- If 6 seconds elapse, the service officially disconnects, which halts the training session in the UI.

### 4. Training Assistant (Zone Guard) & Audio
- Monitors training in real-time. Every `CheckInterval` (15s, 30s, 45s, 60s), it checks if the current BPM falls into the profile's `TargetZone` (calculated dynamically from age: `220 - Age`).
- If BPM is too low: Asynchronously triggers `SoundManager.PlayAccelerate()`.
- If BPM is too high: Asynchronously triggers `SoundManager.PlayDecelerate()`.
- Beep sounds are implemented as unblocked, asynchronous tasks playing synthesized local `.wav` files via `SoundPlayer`.

### 5. Chart Downsampling
To draw Bezier graphs efficiently over hours of training, `ChartRenderer.DrawChart` filters the point array:
- If two adjacent points would map to a horizontal distance of less than `2.0` pixels, the middle points are skipped.
- This bounds rendering complexity to a few hundred points on canvas regardless of workout duration.

### 6. OBS Studio Integration (Live Streaming)
The `LiveHeartRateStreamer` class provides two mechanisms to expose active telemetry to external software like OBS Studio:
- **Local File System (Text Sources)**: Writes real-time values to `OBS_Bpm.txt`, `OBS_Calories.txt`, `OBS_Zone.txt`, and `OBS_Duration.txt` located in the `/obs/` application directory.
- **Local HTTP Server**: Spawns a background `HttpListener` on `http://127.0.0.1:8080/api/hr/` exposing a standard JSON object with telemetry, ideal for OBS Browser Source widgets.
