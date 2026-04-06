using System.Text.Json.Serialization;

namespace IracingStintTracker.Dtos;

public record StintPostDto
{
    [JsonPropertyName("session_id")]
    public required int SessionId { get; init; }
    //[JsonPropertyName("stint_number")]
    //public required int stintNumber { get; init; }
    [JsonPropertyName("driver_name")]
    public required string DriverName { get; init; }

    [JsonPropertyName("start_time")]
    public required float StartTime { get; init; }
    [JsonPropertyName("start_position")]
    public required int StartPosition { get; init; }
    [JsonPropertyName("start_incidents")]
    public required int StartIncidents { get; init; }
    [JsonPropertyName("start_fuel")]
    public required float StartFuel { get; init; }
    [JsonPropertyName("track_temp")]
    public required float TrackTemp { get; init; }
    [JsonPropertyName("track_wetness")]
    public required int TrackWetness { get; init; }
    [JsonPropertyName("sky_cover")]
    public required int SkyCover { get; init; }
    [JsonPropertyName("precipitation")]
    public required float Precipitation { get; init; }
}

public record StintPatchDto
{
    [JsonPropertyName("end_time")]
    public float? EndTime { get; init; }
    [JsonPropertyName("end_position")]
    public int? EndPosition { get; init; }
    [JsonPropertyName("end_fuel")]
    public float? EndFuel { get; init; }
    [JsonPropertyName("end_incidents")]
    public int? EndIncidents { get; init; }
    [JsonPropertyName("is_complete")]
    public required bool IsComplete { get; init; }

}
