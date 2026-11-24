using System.Text.Json;

namespace StintTracker.Core.Events;

// Wire format for POST /v1/events. Must match docs/event-schema.md and
// api/app/schemas/event_schemas.py. Property names are serialized in
// snake_case (EventId -> event_id).

public sealed record TrackerEvent
{
    public int SchemaVersion { get; init; } = 1;
    public required Guid EventId { get; init; }
    public required string Type { get; init; }
    public required int SessionId { get; init; }
    public required int CarIdx { get; init; }
    public required int SessionNum { get; init; }
    public required double SessionTime { get; init; }
    public required int SessionTick { get; init; }
    public required DriverInfo Driver { get; init; }
    public required ClientInfo Client { get; init; }
    public required DateTimeOffset SentAtUtc { get; init; }
    public required object Payload { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, EventJson.Options);
}

public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };
}

public static class EventTypes
{
    public const string SessionInfo = "session_info";
    public const string RaceStarted = "race_started";
    public const string DriverEntered = "driver_entered";
    public const string DriverExited = "driver_exited";
    public const string PitRoadEntered = "pit_road_entered";
    public const string PitBoxEntered = "pit_box_entered";
    public const string PitBoxExited = "pit_box_exited";
    public const string PitRoadExited = "pit_road_exited";
    public const string LapCompleted = "lap_completed";
    public const string RaceFinished = "race_finished";
    public const string CarStatus = "car_status";
}

public sealed record DriverInfo(int UserId, string Name);

public sealed record ClientInfo(Guid InstallId, string AppVersion);

// ---------- shared payload blocks ----------

public sealed record CarState(float Fuel, int ClassPosition, int Incidents, int Lap, int LapsCompleted);

public sealed record Conditions(float TrackTemp, int TrackWetness, int Skies, float Precipitation);

public sealed record TiresRequested(bool Lf, bool Rf, bool Lr, bool Rr);

// ---------- payloads ----------

public sealed record EmptyPayload;

public sealed record SessionInfoPayload(
    string Track,
    string CarName,
    string CarClass,
    int TeamId,
    string TeamName,
    string CarNumber,
    int RaceDurationS,
    string SessionDate);

public sealed record DriverChangePayload(
    CarState CarState, Conditions Conditions, bool OnPitRoad, bool InPitBox);

public sealed record PitRoadEnteredPayload(CarState CarState, bool ViaTow);

public sealed record PitBoxEnteredPayload(
    CarState CarState,
    float RequiredRepairS,
    float OptionalRepairS,
    int FastRepairAvailable,
    TiresRequested TiresRequested);

public sealed record PitBoxExitedPayload(CarState CarState, Conditions Conditions);

public sealed record CarStatePayload(CarState CarState);

public sealed record LapCompletedPayload(int LapNumber, float LapTime, CarState CarState);

public sealed record RaceFinishedPayload(CarState CarState, bool OnPitRoad);

public sealed record CarStatusPayload(CarState CarState, bool OnPitRoad, bool InPitBox);
