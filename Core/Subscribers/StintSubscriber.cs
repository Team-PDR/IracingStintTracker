using IRSDKSharper;
using StintTracker.Core.Dtos;
using System.Threading.Channels;

namespace StintTracker.Core.Subscribers;

internal class StintSubscriber : RaceEventSubscriber
{
    private bool raceFinished = false;
    private StintPostDto? currentStint = null;
    public StintSubscriber(TelemetryManager manager, Channel<ITrackerDto> channel) : base(manager, channel)
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

        raceFinished = true;

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
        currentStint = new StintPostDto()
        {
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

        apiChannel.Writer.WriteAsync(currentStint);
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
        apiChannel.Writer.WriteAsync(stintUpdate);
    }
    private void EndStint(TelemetrySnapshot snapshot)
    {
        UpdateStint(snapshot, true);

        Console.WriteLine("Ending stint");

        currentStint = null;
    }

    private void OnUserStartedRace(TelemetrySnapshot snapshot)
    {
        Console.WriteLine("User started the race. Creating a new stint");
        CreateStint(snapshot);
    }
    private void OnLapCompleted(TelemetrySnapshot snapshot)
    {
        if (!raceFinished)
        {
            UpdateStint(snapshot, false);
        }


        var lap = new LapPostDto()
        {
            LapNumber = snapshot.LapCompleted,
            LapTime = snapshot.LapLastLapTime,
            EndTime = snapshot.SessionTime,
            EndFuel = snapshot.FuelLevel,
        };

        Console.WriteLine($"Created Lap: {lap}");
        apiChannel.Writer.WriteAsync(lap);

        if (raceFinished)
        {
            manager.LapCompleted -= OnLapCompleted;
        }
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
