using System.Text.Json.Serialization;

public record LapPostDto
{
    [JsonPropertyName("stint_id")]
    public required int stintId { get; set; }
    [JsonPropertyName("number")]
    public required int lapNumber { get; set; }
    [JsonPropertyName("time")]
    public required float lapTime { get; set; }
    [JsonPropertyName("position")]
    public required int position { get; set; }
    [JsonPropertyName("incidents")]
    public required int incidents { get; set; }
    [JsonPropertyName("fuel_used")]
    public required float fuelUsed { get; set; }
}
