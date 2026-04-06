using System.Text.Json.Serialization;

namespace IracingStintTracker.Dtos;

public record PitstopPostDto
{
    [JsonPropertyName("stint_id")]
    public required int StintId { get; init; }
    [JsonPropertyName("road_enter_time")]
    public required float RoadEnterTime { get; init; }
    [JsonPropertyName("service_start_time")]
    public required float ServiceStartTime { get; init; }
    [JsonPropertyName("fuel_start_amount")]
    public required float FuelStartAmount { get; init; }
    [JsonPropertyName("required_repair_time")]
    public required float RequiredRepairTime { get; init; }
    [JsonPropertyName("optional_repair_time")]
    public required float OptionalRepairTime { get; init; }
    [JsonPropertyName("left_front_tire_change")]
    public required bool LeftFrontTireChange { get; init; }
    [JsonPropertyName("right_front_tire_change")]
    public required bool RightFrontTireChange { get; init; }
    [JsonPropertyName("left_rear_tire_change")]
    public required bool LeftRearTireChange { get; init; }
    [JsonPropertyName("right_rear_tire_change")]
    public required bool RightRearTireChange { get; init; }
}

public record PitstopPatchDto
{
    [JsonPropertyName("road_exit_time")]
    public required float RoadExitTime { get; set; }
    [JsonPropertyName("service_end_time")]
    public required float ServiceEndTime { get; init; }
    [JsonPropertyName("fuel_end_amount")]
    public required float FuelEndAmount { get; init; }
}   