using System.Text.Json.Serialization;

public class  SessionPostDto
{
    [JsonPropertyName("id")]
    required public int sessionId { get; set; }
    [JsonPropertyName("track")]
    required public string trackName { get; set; }
    [JsonPropertyName("car_class")]
    required public string carClass { get; set; }
    [JsonPropertyName("car")]
    required public string car { get; set; }
    [JsonPropertyName("race_duration")]
    required public int raceDuration { get; set; }
    [JsonPropertyName("session_date")]
    required public DateTime sessionDate { get; set; }
}