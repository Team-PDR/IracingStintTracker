using System.Text.Json.Serialization;

namespace IracingStintTracker.Dtos;

public record SessionPostDto
{
    [JsonPropertyName("id")]
    public required int SessionId { get; init; }
    [JsonPropertyName("track")]
    public required string TrackName { get; init; }
    [JsonPropertyName("car_class")]
    public required string CarClass { get; init; }
    [JsonPropertyName("car")]
    public required string Car { get; init; }
    [JsonPropertyName("race_duration")]
    public required int RaceDuration { get; init; }
    [JsonPropertyName("session_date")]
    public required DateOnly SessionDate { get; init; }
}

public record SessionResponseDto : SessionPostDto { }