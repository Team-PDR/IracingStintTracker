using System.Text.Json.Serialization;

namespace IracingStintTracker.Dtos;

public record SessionPostDto
{
    [JsonPropertyName("id")]
    public required int sessionId { get; init; }
    [JsonPropertyName("track")]
    public required string trackName { get; init; }
    [JsonPropertyName("car_class")]
    public required string carClass { get; init; }
    [JsonPropertyName("car")]
    public required string car { get; init; }
    [JsonPropertyName("race_duration")]
    public required int raceDuration { get; init; }
    [JsonPropertyName("session_date")]
    public required DateTime sessionDate { get; init; }
}
