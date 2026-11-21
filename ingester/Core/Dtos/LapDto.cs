using System.Text.Json.Serialization;

namespace StintTracker.Core.Dtos;

public record LapPostDto : ITrackerDto
{
    [JsonPropertyName("number")]
    public required int LapNumber { get; init; }
    [JsonPropertyName("lap_time")]
    public required float LapTime { get; init; }
    [JsonPropertyName("end_time")]
    public required double EndTime { get; init; }
    [JsonPropertyName("end_fuel")]
    public required float EndFuel { get; init; }
}

public record LapResponseDto : ITrackerDto
{
    [JsonPropertyName("id")]
    public required int LapId { get; init; }

    [JsonPropertyName("stint_id")]
    public required int StintId { get; init; }

    [JsonPropertyName("number")]
    public required int LapNumber { get; init; }

    [JsonPropertyName("lap_time")]
    public required float LapTime { get; init; }
    [JsonPropertyName("end_time")]
    public required float EndTime { get; init; }
    [JsonPropertyName("end_fuel")]
    public required float EndFuel { get; init; }
}