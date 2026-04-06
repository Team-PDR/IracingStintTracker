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
    [JsonPropertyName("position")]
    public required int Position { get; init; }
    [JsonPropertyName("incidents")]
    public required int Incidents { get; init; }
    [JsonPropertyName("start_fuel")]
    public required float StartFuel { get; init; }
}
