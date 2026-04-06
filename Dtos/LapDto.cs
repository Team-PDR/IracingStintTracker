using System.Text.Json.Serialization;

namespace IracingStintTracker.Dtos;

public record LapPostDto
{
    [JsonPropertyName("stint_id")]
    public required int stintId { get; init; }
    [JsonPropertyName("number")]
    public required int lapNumber { get; init; }
    [JsonPropertyName("time")]
    public required float lapTime { get; init; }
    [JsonPropertyName("position")]
    public required int position { get; init; }
    [JsonPropertyName("incidents")]
    public required int incidents { get; init; }
    [JsonPropertyName("start_fuel")]
    public required float startFuel { get; init; }
}
