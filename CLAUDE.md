# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repo layout

Monorepo:
- `ingester/` — the WPF client (described below; paths in the client sections are relative to it).
- `ingester.Tests/` — xUnit tests for the client. Kept outside `ingester/` because the app project compiles every `.cs` file under its folder.
- `api/` — FastAPI backend (its own `app/` package, `requirements.txt`). Deployed to AWS Lambda by `.github/workflows/deploy-lambda.yml` on pushes to `main` that touch `api/**` — so never push work-in-progress to `main`.
- `docs/event-schema.md` — **the client↔server contract.** Change it, `ingester/Core/Events/TrackerEvent.cs` and `api/app/schemas/event_schemas.py` together.

Clients append immutable events to `POST /v1/events`; the server derives stints, pit stops, drive-throughs and laps from them on read (`api/app/services/derivation.py`, `GET /v1/sessions/{id}/cars/{idx}/timeline`). The old create/patch routes (`/sessions/.../stints`, `/pitstops`, `/laps`) and their tables remain only for clients that haven't upgraded; remove them once none are left.

## Commands

```powershell
dotnet build ingester/StintTracker.csproj
dotnet run --project ingester/StintTracker.csproj
dotnet test ingester.Tests
dotnet publish ingester/StintTracker.csproj -p:PublishProfile=ClickOnceProfile   # profiles in Properties/PublishProfiles

cd api
python -m venv .venv
.\.venv\Scripts\python -m pip install -r requirements-dev.txt
.\.venv\Scripts\python -m pytest -q
```

Backend tests default to a throwaway SQLite file (`tests/conftest.py` sets `DATABASE_URL` before `app` is imported, because `app/database/db.py` connects and runs `create_all` at import). Set `TEST_DATABASE_URL` to run against a scratch Postgres database — never production. `api/requirements.txt` is UTF-16 encoded; edit it with care.

**Contract check** (C# output validated by the real API code): from the repo root run `$env:STINT_CONTRACT_OUT="$PWD\api\tests\client_sample.json"; dotnet test ingester.Tests --filter ContractSampleTests`, then the pytest suite picks up `tests/test_client_contract.py` (skipped when the sample is absent).

Telemetry only flows while iRacing is running; otherwise the app sits in "Waiting for iRacing".

## Configuration

`appsettings.json` (copied to output dir; **listed in `.gitignore`** yet present in the working tree) holds `ApiBaseUrl`/`ApiKey` (production) and `TestApiBaseUrl`/`LocalApiKey` (local server at 127.0.0.1:8000). `Gui/MainWindow.xaml.cs` currently reads only `ApiBaseUrl`/`ApiKey`, so switch to the local API by changing which keys are read. Don't commit real keys.

Runtime state lives in `%LOCALAPPDATA%\StintTracker\`: `outbox\` (unsent events, one JSON file each; `outbox\bad\` holds unreadable ones) and `install-id`.

## Client architecture

Pipeline: **TelemetryManager → RaceStateTracker → EventOutbox → EventSender → API**.

- `Core/TelemetryManager.cs` — thin adapter over `IRacingSdk`. Each tick it reads telemetry into an immutable `TelemetryFrame`; it rebuilds the `SessionContext` (driver identity, car, team, session types/durations) only when iRacing publishes new session info. The driver is found by matching `CarIdx`/`DriverUserID`, not by list position. No race logic here.
- `Core/Tracking/RaceStateTracker.cs` — **pure state machine** (no SDK, no I/O, no clock) turning frames into events. Owns all edge detection (`wasOnPitRoad`, `wasInPitBox`, …). Key rules:
  - Only the client whose user is driving emits car events; every client emits `session_info` and `race_started`.
  - On `driver_entered`, edge state is **seeded from the current frame**, so a driver taking over mid-stop does not emit false pit entries (this was the driver-swap bug).
  - Laps are emitted `LapTickDelay` ticks after the crossing (so `LapLastLapTime` is valid) but stamped with the crossing time; a pending lap is flushed if the driver leaves first.
  - A tow counts as pit road for its whole duration.
- `Core/Events/` — wire records (`TrackerEvent` + payloads, serialized snake_case) and `EventFactory`.
- `Core/Outbox/EventOutbox.cs` — durable queue, one file per event, deleted only after the server acknowledges it. `EventSender.cs` posts batches of ≤100, treats accepted/duplicate/rejected as acknowledged, and retries everything else with backoff. Delivery is at-least-once; the server dedupes by `event_id`.
- `Core/StintTrackerService.cs` — composition root; re-exposes status as events (`OnIracingRunningChanged`, `OnTrackerStatusChanged`, `OnApiConnectionChanged`) for the UI. Defines `TrackerStatus`.
- `Gui/` — `App.xaml.cs` has a custom `Main` and a named mutex for single-instance; `MainWindow` loads config, starts the service, and marshals status events to the UI via `Dispatcher.Invoke`. Tray icon via `Hardcodet.NotifyIcon.Wpf`, toasts via `Microsoft.Toolkit.Uwp.Notifications`.

WPF projects omit `System.IO` from implicit usings (it clashes with `System.Windows.Shapes.Path`); add `using System.IO;` where needed. Logging is plain `Console.WriteLine` throughout.
