using System;
using System.Collections.Generic;
using System.Text;
using IracingStintTracker.Dtos;

namespace IracingStintTracker.Subscribers;

internal class PitstopSubscriber : RaceEventSubscriber
{

    private PitstopPostDto? currentPitstop = null;
    private PitstopPatchDto? currentPitstopPatch = null;
    private float? roadEnterTime = null; 
    public PitstopSubscriber(TelemetryManager manager) : base(manager)
    {
        this.manager.RaceStarted += OnRaceStarted;
        this.manager.RaceFinished += OnRaceFinished;
    }
    protected override void OnRaceStarted(TelemetrySnapshot snapshot)
    {
        manager.PitRoadEntered += OnPitRoadEntered;
        manager.PitRoadExited += OnPitRoadExited;
        manager.PitBoxEntered += OnPitBoxEntered;
        manager.PitBoxExited += OnPitBoxExited;
    }

    protected override void OnRaceFinished(TelemetrySnapshot snapshot)
    {
        manager.PitRoadEntered -= OnPitRoadEntered;
        manager.PitRoadExited -= OnPitRoadExited;
        manager.PitBoxEntered -= OnPitBoxEntered;
        manager.PitBoxExited -= OnPitBoxExited;
    }

    private void OnPitRoadEntered(TelemetrySnapshot snapshot)
    {
        roadEnterTime = snapshot.SessionTime;
    }

    private void OnPitRoadExited(TelemetrySnapshot snapshot)
    {
        if (currentPitstopPatch == null)
        {
            Console.WriteLine("No pitstop to update");
            return;
        }

        currentPitstopPatch.roadExitTime = snapshot.SessionTime;

        // TODO: send currentPitstopPastch to API
        Console.WriteLine($"Pitstop update: {currentPitstopPatch}");
    }

    private void OnPitBoxEntered(TelemetrySnapshot snapshot)
    {
        float enterTime;

        if (roadEnterTime == null)
        {
            enterTime = snapshot.SessionTime;
        }
        else
        {
            enterTime = roadEnterTime.Value;
        }

        currentPitstop = new PitstopPostDto()
        {
            stintId = 1, // TODO: get stint id from somewhere
            roadEnterTime = enterTime,
            serviceStartTime = snapshot.SessionTime,
            fuelStartAmount = snapshot.FuelLevel,
            requiredRepairTime = snapshot.PitRepairLeft,
            optionalRepairTime = snapshot.PitOptRepairLeft,
            leftFrontTireChange = snapshot.dpLFTireChange > 0,
            rightFrontTireChange = snapshot.dpRFTireChange > 0,
            leftRearTireChange = snapshot.dpLRTireChange > 0,
            rightRearTireChange = snapshot.dpRRTireChange > 0,
        };

        // TODO: send currentPitstop to API
        Console.WriteLine($"New pitstop: {currentPitstop}");
    }

    private void OnPitBoxExited(TelemetrySnapshot snapshot)
    {
        if (currentPitstop == null)
        {
            return;
        }

        currentPitstopPatch = new PitstopPatchDto()
        {
            serviceEndTime = snapshot.SessionTime,
            fuelEndAmount = snapshot.FuelLevel,
            roadExitTime = null,
        };

        Console.WriteLine($"updated pitstop: {currentPitstopPatch}");
    }
}
