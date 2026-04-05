using System.Text.Json.Serialization;

namespace IracingStintTracker.Dtos;

public record StintPostDto
{
    [JsonPropertyName("session_id")]
    public required int sessionId { get; init; }
    //[JsonPropertyName("stint_number")]
    //public required int stintNumber { get; init; }
    [JsonPropertyName("driver_name")]
    public required string driverName { get; init; }

    [JsonPropertyName("start_time")]
    public required float startTime { get; init; }
    [JsonPropertyName("start_position")]
    public required int startPosition { get; init; }
    [JsonPropertyName("start_incidents")]
    public required int startIncidents { get; init; }
    [JsonPropertyName("start_fuel")]
    public required float startFuel { get; init; }
    [JsonPropertyName("track_temp")]
    public required float trackTemp { get; init; }
    [JsonPropertyName("track_wetness")]
    public required int trackWetness { get; init; }
    [JsonPropertyName("sky_cover")]
    public required int skyCover { get; init; }
    [JsonPropertyName("precipitation")]
    public required float precipitation { get; init; }
}

public record StintPatchDto
{
    [JsonPropertyName("end_time")]
    public float? endTime { get; init; }
    [JsonPropertyName("end_position")]
    public int? endPosition { get; init; }
    [JsonPropertyName("end_fuel")]
    public float? endFuel { get; init; }
    [JsonPropertyName("end_incidents")]
    public int? endIncidents { get; init; }
    [JsonPropertyName("is_complete")]
    public required bool isComplete { get; init; }

}
