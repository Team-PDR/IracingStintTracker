using IracingStintTracker.Dtos;

namespace IracingStintTracker.Subscribers;

internal class PitstopSubscriber : RaceEventSubscriber
{
    private PitstopPostDto? currentPitstop = null;
    private float? roadEnterTime = null;
    private float? serviceEndTime = null;
    private float? fuelEndAmount = null;
    public PitstopSubscriber(TelemetryManager manager) : base(manager)
    {
        this.manager.RaceStarted += OnRaceStarted;
        this.manager.RaceFinished += OnRaceFinished;
    }

    protected override void SubscribeToCarEvents()
    {
        base.SubscribeToCarEvents();
        manager.PitRoadEntered += OnPitRoadEntered;
        manager.PitRoadExited += OnPitRoadExited;
        manager.PitBoxEntered += OnPitBoxEntered;
        manager.PitBoxExited += OnPitBoxExited;
        manager.CarExited += OnCarExited;
        manager.CarEntered += OnCarEntered;
    }

    protected override void UnsubscribeFromCarEvents()
    {
        base.UnsubscribeFromCarEvents();
        manager.PitRoadEntered -= OnPitRoadEntered;
        manager.PitRoadExited -= OnPitRoadExited;
        manager.PitBoxEntered -= OnPitBoxEntered;
        manager.PitBoxExited -= OnPitBoxExited;
    }

    private void ResetState()
    {
        currentPitstop = null;
        roadEnterTime = null;
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
    }

    private void OnCarEntered()
    {
        if (!subscribed)
        {
            SubscribeToCarEvents();
        }

        // TODO: get stint id
    }

    private void OnCarExited()
    {
        if (subscribed)
        {
            UnsubscribeFromCarEvents();
        }
        ResetState();
    }

    private void OnPitRoadEntered(TelemetrySnapshot snapshot)
    {
        roadEnterTime = snapshot.SessionTime;
    }

    private void OnPitRoadExited(TelemetrySnapshot snapshot)
    {


        var pitstop = new PitstopPatchDto()
        {
            RoadExitTime = snapshot.SessionTime,
            ServiceEndTime = this.serviceEndTime!.Value,
            FuelEndAmount = this.fuelEndAmount!.Value,
        };

        // TODO: send currentPitstopPastch to API
        ResetState();
        Console.WriteLine($"Pitstop update: {pitstop}");
    }

    private void OnPitBoxEntered(TelemetrySnapshot snapshot)
    {

        currentPitstop = new PitstopPostDto()
        {
            StintId = 1, // TODO: get stint id from somewhere
            RoadEnterTime = this.roadEnterTime ?? snapshot.SessionTime,
            ServiceStartTime = snapshot.SessionTime,
            FuelStartAmount = snapshot.FuelLevel,
            RequiredRepairTime = snapshot.PitRepairLeft,
            OptionalRepairTime = snapshot.PitOptRepairLeft,
            LeftFrontTireChange = snapshot.dpLFTireChange > 0,
            RightFrontTireChange = snapshot.dpRFTireChange > 0,
            LeftRearTireChange = snapshot.dpLRTireChange > 0,
            RightRearTireChange = snapshot.dpRRTireChange > 0,
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


        serviceEndTime = snapshot.SessionTime;
        fuelEndAmount = snapshot.FuelLevel;
    


        Console.WriteLine($"service end time: {serviceEndTime}, fuel end level: {fuelEndAmount}");
    }
}
