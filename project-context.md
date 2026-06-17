# HeartRateMonitor - Technical Project Context

This document provides a technical overview of the HeartRateMonitor (PulseStreamer) WPF application. It details the modular architecture, component responsibilities, file storage formats, and key feature implementations.

---

## Directory Structure & Modules

- **[App.xaml](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/App.xaml)** / **[App.xaml.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/App.xaml.cs)**: Main WPF application entry point.
- **[MainWindow.xaml](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/MainWindow.xaml)** / **[MainWindow.xaml.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/MainWindow.xaml.cs)**: Orchestrator UI, bindings, event handlers, and active training lifecycle controller. The main window focuses entirely on live tracking without complex tab systems.
- **[BleHeartRateService.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/BleHeartRateService.cs)**: Handles Bluetooth LE discovery and communication using WinRT APIs. Implements GC protection and Coast Mode.
- **[ChartRenderer.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/ChartRenderer.cs)**: Dedicated helper to render real-time and historical heart rate graphs onto WPF canvas elements, with grid overlays, heart rate zones background coloring, and horizontal pixel-gap downsampling.
- **[FitnessCalculator.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/FitnessCalculator.cs)**: Handles all metabolic calculations — Tanaka MaxHR, Keytel EE formula (HR ≥ 85), dynamic MET with Heart Rate Reserve (HR < 85), EPOC estimation, and HR zone management (bounds, colors, names).
- **[UserProfile.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/UserProfile.cs)**: Holds user parameters (age, weight, gender, resting HR), preferences, training goals, and last connected BLE device details.
- **[ProfileManager.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/ProfileManager.cs)**: Manages profile creation, deletion, loading, and persistence.
- **[SoundManager.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/SoundManager.cs)**: Synthesizes PCM beep waveforms at runtime, saves them as `.wav` files, and plays them back asynchronously using cached `SoundPlayer` instances.
- **[LiveHeartRateStreamer.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/LiveHeartRateStreamer.cs)**: Exposes real-time telemetry updates via a public static class and events, enabling future HTTP/WebSocket OBS overlays to stream data out of the core app without direct modifications.
- **[HistoryWindow.xaml](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/HistoryWindow.xaml)** / **[HistoryWindow.xaml.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/HistoryWindow.xaml.cs)**: A dedicated, independent window for browsing, analyzing, and exporting historical sessions.
- **[WorkoutSummaryWindow.xaml](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/WorkoutSummaryWindow.xaml)** / **[WorkoutSummaryWindow.xaml.cs](file:///c:/Users/Barczi/Documents/antigravity/peaceful-bell/WorkoutSummaryWindow.xaml.cs)**: Custom modern dialog presenting comprehensive post-workout summaries including calorie breakdown with EPOC afterburn card.
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
      "LastDeviceAddress": "40:06:A0:6C:C1:2F",
      "LastDeviceName": "Polar H10 923F45",
      "Age": 30,
      "Weight": 75.0,
      "RestingHR": 70,
      "Gender": "Male",
      "PreferredActivity": "Bieżnia",
      "ZoneGuardEnabled": false,
      "ZoneGuardTargetZone": 3,
      "ZoneGuardCheckIntervalSeconds": 30,
      "GoalType": "Brak",
      "TargetCalories": 500,
      "TargetDurationMinutes": 45
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
  "Name": "Bieżnia 10.06.2026 10:15",
  "History": [
    { "Time": 1.0, "Bpm": 72.0 },
    { "Time": 2.0, "Bpm": 75.0 }
  ],
  "AverageBpm": 74.0,
  "MinBpm": 72,
  "MaxBpm": 75,
  "LastBpm": 75,
  "DurationString": "00:00:02",
  "ZoneString": "Strefa: Rozgrzewka (Strefa 1: 94-112 BPM)",
  "StartTime": "2026-06-17T10:15:30",
  "CaloriesBurned": 0.15
}
```

---

## Critical Mechanisms

### 1. Calorie Calculation System (FitnessCalculator)
The calorie system uses a dual-algorithm approach with activity-specific tuning:

#### Keytel Formula (HR ≥ 85 BPM)
Gender-specific energy expenditure formula from *Keytel et al. (2005), European Journal of Applied Physiology*:
- **Male**: `(-55.0969 + 0.6309 × HR + 0.1988 × weight + 0.2017 × age) / 4.184` kcal/min
- **Female**: `(-20.4022 + 0.4472 × HR - 0.1263 × weight + 0.074 × age) / 4.184` kcal/min

Result is multiplied by an activity-specific correction factor (1.05 for running, 0.90 for cycling/VR, 1.12 for boxing).

#### Dynamic MET (HR < 85 BPM)
Instead of fixed MET values, the system calculates MET dynamically using Heart Rate Reserve:
```
HRR = (currentBPM - restingHR) / (maxHR - restingHR)
dynamicMET = minMET + (HRR × (maxMET - minMET))
kcal/min = dynamicMET × 3.5 × weight / 200
```
MET ranges come from the *Compendium of Physical Activities (2024)* — e.g., Treadmill: 3.5–9.8, Boxing: 4.0–10.5, VR Fitness: 3.0–7.0.

#### Max Heart Rate (Tanaka Formula)
Uses the more accurate Tanaka formula: `208 - 0.7 × age` instead of the classic `220 - age` (Tanaka et al. 2001, JACC).

#### EPOC Estimation (Afterburn)
After training ends, the system estimates Excess Post-Exercise Oxygen Consumption:
- **Intensity factor**: 3% (light, <65% maxHR) to 15% (HIIT, >85% maxHR)
- **Duration modifier**: scales up to 2× for workouts over 60 minutes
- Formula: `totalCalories × intensityFactor × min(duration/30, 2.0)`
- Displayed in a dedicated "EFEKT AFTERBURN (EPOC)" card in the workout summary popup

### 2. UI Thread & Settings Debouncing
To prevent application freezes caused by high-frequency UI events (like dragging a slider), file I/O operations inside `ProfileManager.SaveProfiles` and `MainWindow.SaveGlobalSettings` are debounced using `DispatcherTimer` set to 500ms.

### 3. BLE Stability & GC Protection
WinRT connection drops are prevented by holding strong references to `BluetoothLEDevice`, `GattDeviceService`, and `GattCharacteristic` in a static `_strongGcKeeper` list within `BleHeartRateService.cs`.

### 4. Coast Mode (6s Concealment)
Upon an OS-level disconnect or missing heart rate packets for >1.8 seconds, the service triggers **Coast Mode**:
- A supervisor timer fires once per second.
- It continues sending simulated heart rate measurements matching the last known BPM.
- The UI displays a warning: "Coast Mode (Słaby sygnał)".
- If a real heart rate packet is received within 6 seconds, Coast Mode is cancelled.
- If 6 seconds elapse, the service officially disconnects.

### 5. Training Assistant (Zone Guard) & Audio
- Monitors training in real-time. Every `CheckInterval` (15s, 30s, 45s, 60s), it checks if the current BPM falls into the profile's `TargetZone` (calculated dynamically from age using Tanaka: `208 - 0.7 × Age`).
- If BPM is too low: Asynchronously triggers `SoundManager.PlayAccelerate()`.
- If BPM is too high: Asynchronously triggers `SoundManager.PlayDecelerate()`.
- Beep sounds are implemented as unblocked, asynchronous tasks playing synthesized local `.wav` files.

### 6. Chart Downsampling
To draw Bezier graphs efficiently over hours of training, `ChartRenderer.DrawChart` filters the point array:
- If two adjacent points would map to a horizontal distance of less than `2.0` pixels, the middle points are skipped.
- This bounds rendering complexity to a few hundred points on canvas regardless of workout duration.

### 7. OBS Studio Integration (Live Streaming)
The `LiveHeartRateStreamer` class provides two mechanisms to expose active telemetry to external software like OBS Studio:
- **Local File System (Text Sources)**: Writes real-time values to `OBS_Bpm.txt`, `OBS_Calories.txt`, `OBS_Zone.txt`, and `OBS_Duration.txt` located in the `/obs/` application directory.
- **Local HTTP Server**: Spawns a background `HttpListener` on `http://127.0.0.1:8080/api/hr/` exposing a standard JSON object with telemetry, ideal for OBS Browser Source widgets.
