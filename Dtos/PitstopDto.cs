using System.Text.Json.Serialization;

namespace IracingStintTracker.Dtos;

public record PitstopPostDto
{
    [JsonPropertyName("stint_id")]
    public required int stintId { get; init; }
    [JsonPropertyName("road_enter_time")]
    public required float roadEnterTime { get; init; }
    [JsonPropertyName("service_start_time")]
    public required float serviceStartTime { get; init; }
    [JsonPropertyName("fuel_start_amount")]
    public required float fuelStartAmount { get; init; }
    [JsonPropertyName("required_repair_time")]
    public required float requiredRepairTime { get; init; }
    [JsonPropertyName("optional_repair_time")]
    public required float optionalRepairTime { get; init; }
    [JsonPropertyName("left_front_tire_change")]
    public required bool leftFrontTireChange { get; init; }
    [JsonPropertyName("right_front_tire_change")]
    public required bool rightFrontTireChange { get; init; }
    [JsonPropertyName("left_rear_tire_change")]
    public required bool leftRearTireChange { get; init; }
    [JsonPropertyName("right_rear_tire_change")]
    public required bool rightRearTireChange { get; init; }
}

public record PitstopPatchDto
{
    [JsonPropertyName("road_exit_time")]
    public required float? roadExitTime { get; set; }
    [JsonPropertyName("service_end_time")]
    public required float serviceEndTime { get; init; }
    [JsonPropertyName("fuel_end_amount")]
    public required float fuelEndAmount { get; init; }
}   