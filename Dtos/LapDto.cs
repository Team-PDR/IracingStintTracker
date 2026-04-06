using System.Text.Json.Serialization;

namespace IracingStintTracker.Dtos;

public record LapPostDto
{
    [JsonPropertyName("stint_id")]
    public required int StintId { get; init; }
    [JsonPropertyName("number")]
    public required int LapNumber { get; init; }
    [JsonPropertyName("time")]
    public required float LapTime { get; init; }
}

public record LapResponseDto
{
    [JsonPropertyName("id")]
    public required int LapId { get; init; }

    [JsonPropertyName("stint_id")]
    public required int StintId { get; init; }

    [JsonPropertyName("number")]
    public required int LapNumber { get; init; }

    [JsonPropertyName("time")]
    public required float LapTime { get; init; }
}