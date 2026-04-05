using System.Text.Json.Serialization;

public record SessionPostDto
{
    [JsonPropertyName("id")]
    public required int sessionId { get; set; }
    [JsonPropertyName("track")]
    public required string trackName { get; set; }
    [JsonPropertyName("car_class")]
    public required string carClass { get; set; }
    [JsonPropertyName("car")]
    public required string car { get; set; }
    [JsonPropertyName("race_duration")]
    public required int raceDuration { get; set; }
    [JsonPropertyName("session_date")]
    public required DateTime sessionDate { get; set; }
}
