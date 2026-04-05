using System.Text.Json.Serialization;

public class StintPostDto
{
    [JsonPropertyName("session_id")]
    required public int sessionId { get; set; }
    [JsonPropertyName("stint_number")]
    required public int stintNumber { get; set; }
    [JsonPropertyName("driver_name")]
    required public string driverName { get; set; }

    [JsonPropertyName("start_time")]
    required public float startTime { get; set; }
    [JsonPropertyName("start_position")]
    required public int startPosition { get; set; }
    [JsonPropertyName("start_incidents")]
    required public int startIncidents { get; set; }
    [JsonPropertyName("start_fuel")]
    required public float startFuel { get; set; }
    [JsonPropertyName("track_temp")]
    required public float trackTemp { get; set; }
    [JsonPropertyName("track_wetness")]
    required public int trackWetness { get; set; }
    [JsonPropertyName("sky_cover")]
    required public int skyCover { get; set; }
    [JsonPropertyName("precipitation")]
    required public float precipitation { get; set; }
}

public class StintPatchDto
{
    [JsonPropertyName("end_time")]
    public float? endTime { get; set; }
    [JsonPropertyName("end_position")]
    public int? endPosition { get; set; }
    [JsonPropertyName("end_fuel")]
    public float? endFuel { get; set; }
    [JsonPropertyName("end_incidents")]
    public int? endIncidents { get; set; }
    [JsonPropertyName("is_complete")]
    required public bool isComplete { get; set; }

}
