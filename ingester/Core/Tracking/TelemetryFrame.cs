using IRSDKSharper;

namespace StintTracker.Core.Tracking;

public sealed record DriverIdentity(int UserId, string Name);

/// <summary>
/// Per-session facts from iRacing's session info YAML. Rebuilt only when
/// iRacing publishes new session info, not every tick.
/// </summary>
public sealed record SessionContext
{
    public required int SubSessionId { get; init; }
    public required int CarIdx { get; init; }
    public required DriverIdentity Driver { get; init; }

    public required string Track { get; init; }
    public required string CarName { get; init; }
    public required string CarClass { get; init; }
    public required int TeamId { get; init; }
    public required string TeamName { get; init; }
    public required string CarNumber { get; init; }

    /// <summary>Session type ("Practice", "Race", ...) by SessionNum.</summary>
    public required IReadOnlyList<string> SessionTypes { get; init; }

    /// <summary>Scheduled length in seconds by SessionNum (0 if unlimited).</summary>
    public required IReadOnlyList<int> SessionDurationsS { get; init; }
}

/// <summary>
/// One telemetry tick as plain values, so race-state logic can be tested
/// without the iRacing SDK.
/// </summary>
public sealed record TelemetryFrame
{
    public required SessionContext Session { get; init; }

    public int SessionNum { get; init; }
    public double SessionTime { get; init; }
    public int SessionTick { get; init; }
    public IRacingSdkEnum.SessionState SessionState { get; init; }
    public uint SessionFlags { get; init; }

    /// <summary>This user's car is on track and this user is driving it.</summary>
    public bool IsOnTrackCar { get; init; }

    /// <summary>This user is in the car (iRacing "IsOnTrack").</summary>
    public bool IsOnTrack { get; init; }

    public float TowTime { get; init; }
    public bool OnPitRoad { get; init; }
    public bool PitstopActive { get; init; }

    public int Lap { get; init; }
    public int LapCompleted { get; init; }
    public float LapLastLapTime { get; init; }
    public int ClassPosition { get; init; }
    public int Incidents { get; init; }
    public float FuelLevel { get; init; }

    public float TrackTemp { get; init; }
    public int TrackWetness { get; init; }
    public int Skies { get; init; }
    public float Precipitation { get; init; }

    public float PitRepairLeft { get; init; }
    public float PitOptRepairLeft { get; init; }
    public int FastRepairAvailable { get; init; }
    public bool TireChangeLF { get; init; }
    public bool TireChangeRF { get; init; }
    public bool TireChangeLR { get; init; }
    public bool TireChangeRR { get; init; }

    public bool IsRaceSession =>
        SessionNum >= 0
        && SessionNum < Session.SessionTypes.Count
        && Session.SessionTypes[SessionNum] == "Race";
}
