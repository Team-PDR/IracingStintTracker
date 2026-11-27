using StintTracker.Core.Events;
using static StintTracker.Tests.Frames;

namespace StintTracker.Tests;

/// <summary>
/// Writes a realistic two-client swap as wire JSON so the API's own validation
/// and derivation can be run against what this client actually sends.
/// Set STINT_CONTRACT_OUT to a file path to enable; otherwise a no-op.
/// See api/tests/test_client_contract.py.
/// </summary>
public class ContractSampleTests
{
    [Fact]
    public void Write_swap_sample_for_api_contract_check()
    {
        string? path = Environment.GetEnvironmentVariable("STINT_CONTRACT_OUT");
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var kamCtx = Context(Kam);
        var gregCtx = Context(Greg);
        var kam = Tracker(statusIntervalS: 60);
        var greg = Tracker(statusIntervalS: 60);

        var events = new List<TrackerEvent>();
        events.AddRange(Run(kam, [
            At(kamCtx, 344, true) with { FuelLevel = 100f, LapCompleted = 0 },
            At(kamCtx, 500, true) with { LapCompleted = 1, LapLastLapTime = 155.2f },
            At(kamCtx, 510, true) with { LapCompleted = 1, LapLastLapTime = 155.2f },
            At(kamCtx, 8289, true) with { OnPitRoad = true, FuelLevel = 6f, LapCompleted = 1 },
            At(kamCtx, 8412, true) with { OnPitRoad = true, PitstopActive = true, FuelLevel = 2.1f, LapCompleted = 1, TireChangeLF = true, TireChangeRF = true, TireChangeLR = true, TireChangeRR = true },
            At(kamCtx, 8420, false) with { OnPitRoad = true, PitstopActive = true },
        ]));
        events.AddRange(Run(greg, [
            At(gregCtx, 344, false) with { LapCompleted = 0 },
            At(gregCtx, 8644, true) with { OnPitRoad = true, PitstopActive = true, FuelLevel = 60f, LapCompleted = 1 },
            At(gregCtx, 9266, true) with { OnPitRoad = true, FuelLevel = 100f, LapCompleted = 1 },
            At(gregCtx, 9269, true) with { FuelLevel = 100f, LapCompleted = 1 },
        ]));

        File.WriteAllText(path, "[" + string.Join(",", events.Select(e => e.ToJson())) + "]");
    }
}
