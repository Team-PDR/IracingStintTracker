using System;
using System.Collections.Generic;
using System.Text;
using IracingStintTracker.Dtos;

namespace IracingStintTracker.Subscribers;
internal class LapSubscriber : RaceEventSubscriber
{
    public LapSubscriber(TelemetryManager manager) : base(manager)
    {
        this.manager.RaceStarted += OnRaceStarted;
    }

    protected override void OnRaceStarted(TelemetrySnapshot snapshot)
    {
        manager.LapCompleted += OnLapCompleted;
    }

    private void OnLapCompleted(TelemetrySnapshot snapshot)
    {
        var lap = new LapPostDto()
        {
            StintId = 1, // TODO: Get stintId from somewhere
            LapNumber = snapshot.LapCompleted,
            LapTime = snapshot.LapLastLapTime,
        };
        Console.WriteLine($"Created Lap: {lap}");
        // TODO: Post lap to API
    }
}

