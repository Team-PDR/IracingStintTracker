using System.Text.Json.Serialization;

public class PitstopPostDto
{
    [JsonPropertyName("stint_id")]
    required public int stintId { get; set; }
    [JsonPropertyName("road_enter_time")]
    required public float roadEnterTime { get; set; }
    [JsonPropertyName("service_start_time")]
    required public float serviceStartTime { get; set; }
    [JsonPropertyName("fuel_start_amount")]
    required public float fuelStartAmount { get; set; }
    [JsonPropertyName("required_repair_time")]
    required public float requiredRepairTime { get; set; }
    [JsonPropertyName("optional_repair_time")]
    required public float optionalRepairTime { get; set; }
    [JsonPropertyName("left_front_tire_change")]
    required public bool leftFrontTireChange { get; set; }
    [JsonPropertyName("right_front_tire_change")]
    required public bool rightFrontTireChange { get; set; }
    [JsonPropertyName("left_rear_tire_change")]
    required public bool leftRearTireChange { get; set; }
    [JsonPropertyName("right_rear_tire_change")]
    required public bool rightRearTireChange { get; set; }
}

public class PitstopPatchDto
{
    [JsonPropertyName("road_exit_time")]
    required public float roadExitTime { get; set; }
    [JsonPropertyName("service_end_time")]
    required public float serviceEndTime { get; set; }
    [JsonPropertyName("fuel_end_amount")]
    required public float fuelEndAmount { get; set; }
}   