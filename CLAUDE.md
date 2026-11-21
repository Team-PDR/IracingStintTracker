# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Windows-only WPF tray app (`net10.0-windows10.0.19041.0`, single project `StintTracker.csproj`) that reads iRacing telemetry via `IRSDKSharper`, detects race/stint/lap/pit events, and posts them to a remote REST API. There is no test project and no linter configured.

## Commands

```powershell
dotnet build StintTracker.csproj
dotnet run --project StintTracker.csproj
dotnet publish StintTracker.csproj -p:PublishProfile=ClickOnceProfile   # profiles in Properties/PublishProfiles
```

Telemetry only flows while iRacing is running; otherwise the app sits in "Waiting for iRacing".

## Configuration

`appsettings.json` (copied to output dir; **listed in `.gitignore`** yet present in the working tree) holds `ApiBaseUrl`/`ApiKey` (production) and `TestApiBaseUrl`/`LocalApiKey` (local server at 127.0.0.1:8000). `Gui/MainWindow.xaml.cs` currently reads only `ApiBaseUrl`/`ApiKey`, so switch to the local API by changing which keys are read. Don't commit real keys.

## Architecture

Event pipeline: **TelemetryManager → Subscribers → Channel → ApiClient**.

- `Core/TelemetryManager.cs` — wraps `IRacingSdk`; on each telemetry tick builds an immutable `TelemetrySnapshot` and raises events (`RaceStarted`, `RaceFinished`, `UserStartedRace`, `LapCompleted`, `PitRoadEntered/Exited`, `PitBoxEntered/Exited`, `CarEntered/Exited`, `SdkConnectionChange`). It owns all state-edge detection (previous-tick flags like `wasOnPitRoad`) and resets it in `OnStopped`. Lap events are delayed by `LapTickDelay` ticks so `LapLastLapTime` is valid before `LapCompleted` fires.
- `Core/Subscribers/*` — each derives from `RaceEventSubscriber`, subscribes to manager events, and turns snapshots into DTOs written to a shared `Channel<ITrackerDto>`. Car-specific events are only subscribed between `RaceStarted` and `RaceFinished` (`SubscribeToCarEvents`/`UnsubscribeFromCarEvents`). `StintSubscriber` creates a stint on race start/pit-box exit, ends it on pit-box entry/race finish, and emits laps.
- `Core/Dtos/*` — `*PostDto` (create) and `*PatchDto` (update) implementing the marker interface `ITrackerDto`.
- `Core/API/ApiClient.cs` — single consumer of the channel (`Run`); dispatches by DTO type in `SendToApi`. It is **stateful**: it remembers `sessionId`, `carId` and the server-assigned `stintId` from the last create response, and builds URLs from them (`/sessions/{id}/cars/{id}/stints/{id}`, `/stints/{id}/laps`). Ordering through the channel therefore matters — a lap or stint patch is skipped if no stint id has been returned yet. Auth is an `X-API-Key` header; `/health` is checked once at construction.
- `Core/StintTrackerService.cs` — composition root: wires manager, subscribers and `ApiClient`, and re-exposes status as events (`OnIracingRunningChanged`, `OnTrackerStatusChanged`, `OnApiConnectionChanged`) for the UI. Defines `TrackerStatus`.
- `Gui/` — `App.xaml.cs` has a custom `Main` and a named mutex for single-instance; `MainWindow` loads config, starts the service, and marshals status events to the UI via `Dispatcher.Invoke`. Tray icon via `Hardcodet.NotifyIcon.Wpf`, toasts via `Microsoft.Toolkit.Uwp.Notifications`.

Logging is plain `Console.WriteLine` throughout.
