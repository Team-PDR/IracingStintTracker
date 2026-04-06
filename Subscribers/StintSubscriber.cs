using System;
using System.Collections.Generic;
using System.Text;
using IracingStintTracker.Dtos;
using IRSDKSharper;

namespace IracingStintTracker.Subscribers;
internal class StintSubscriber : RaceEventSubscriber
{

    private StintPostDto? currentStint = null;
    public StintSubscriber(TelemetryManager manager) : base(manager)
    {
        this.manager.RaceStarted += OnRaceStarted;
        this.manager.RaceFinished += OnRaceFinished;
    }

    protected override void SubscribeToCarEvents()
    {
        base.SubscribeToCarEvents();
        manager.UserStartedRace += OnUserStartedRace;
        manager.LapCompleted += OnLapCompleted;
        manager.PitBoxEntered += OnPitBoxEntered;
        manager.PitBoxExited += OnPitBoxExited;
        
    }

    protected override void UnsubscribeFromCarEvents()
    {
        base.UnsubscribeFromCarEvents();
        manager.UserStartedRace -= OnUserStartedRace;
        manager.LapCompleted -= OnLapCompleted;
        manager.PitBoxEntered -= OnPitBoxEntered;
        manager.PitBoxExited -= OnPitBoxExited;

    }

    protected override void OnRaceStarted(TelemetrySnapshot snapshot)
    {
        if (!subscribed)
        {
            SubscribeToCarEvents();
        }
        
    }

    protected override void OnRaceFinished(TelemetrySnapshot snapshot)
    {
        if (subscribed)
        {
            UnsubscribeFromCarEvents();
        }

        if (currentStint != null) 
        {
            EndStint(snapshot);
        }
    }

    private static string GetDriverName(IRacingSdkSessionInfo sessionInfo, int carId)
    {
        IRacingSdkSessionInfo.DriverInfoModel driverInfo = sessionInfo.DriverInfo;
        return driverInfo.Drivers[carId].UserName;
    }

    private void CreateStint(TelemetrySnapshot snapshot) 
    {
        currentStint = new StintPostDto ()
        {
            SessionId = snapshot.SessionInfo.WeekendInfo.SubSessionID,
            StintNumber = 1, // TODO: get stint number
            DriverName = GetDriverName(snapshot.SessionInfo, snapshot.PlayerCarIdx),
            StartTime = snapshot.SessionTime,
            StartPosition = snapshot.PlayerCarClassPosition,
            StartIncidents = snapshot.PlayerCarMyIncidentCount,
            StartFuel = snapshot.FuelLevel,
            TrackTemp = snapshot.TrackTempCrew,
            TrackWetness = snapshot.TrackWetness,
            SkyCover = snapshot.Skies,
            Precipitation = snapshot.Precipitation,
        };
        Console.WriteLine($"Creating stint: {currentStint}");
        // TODO: Post stint to API
    }
    private void UpdateStint(TelemetrySnapshot snapshot, bool completed) 
    {   
        if (currentStint == null) 
        {
            Console.WriteLine("No current stint to update");
            return;
        }
            
        var stintUpdate = new StintPatchDto()
        {
            EndTime = snapshot.SessionTime,
            EndPosition = snapshot.PlayerCarClassPosition,
            EndIncidents = snapshot.PlayerCarMyIncidentCount,
            EndFuel = snapshot.FuelLevel,
            IsComplete = completed,
        };
        Console.WriteLine($"Updating stint: {stintUpdate}");
        // TODO: Patch stint to API
    }
    private void EndStint(TelemetrySnapshot snapshot) 
    {
        UpdateStint(snapshot, true);

        Console.WriteLine("Ending stint");

        currentStint = null;
    }

    private void OnUserStartedRace(TelemetrySnapshot snapshot) 
    { 
        CreateStint(snapshot);
    }
    private void OnLapCompleted(TelemetrySnapshot snapshot) 
    {
        UpdateStint(snapshot, false);
    }
    private void OnPitBoxEntered(TelemetrySnapshot snapshot) 
    { 
        EndStint(snapshot);
    }
    private void OnPitBoxExited(TelemetrySnapshot snapshot) 
    {
        CreateStint(snapshot);
    }
}
