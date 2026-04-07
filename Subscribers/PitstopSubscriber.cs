using IracingStintTracker.Dtos;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace IracingStintTracker.Subscribers;

internal class PitstopSubscriber : RaceEventSubscriber
{
    private PitstopPostDto? currentPitstop = null;
    private double? roadEnterTime = null;
    private double? serviceEndTime = null;
    private float? fuelEndAmount = null;
    public PitstopSubscriber(TelemetryManager manager, Channel<object> channel) : base(manager, channel)
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

        if (currentPitstop == null) { return; }

        var pitstop = new PitstopPatchDto()
        {
            RoadExitTime = snapshot.SessionTime,
            ServiceEndTime = this.serviceEndTime!.Value,
            FuelEndAmount = this.fuelEndAmount!.Value,
        };

        apiChannel.Writer.WriteAsync(pitstop);
        ResetState();
        Console.WriteLine($"Pitstop update: {pitstop}");
    }

    private void OnPitBoxEntered(TelemetrySnapshot snapshot)
    {

        currentPitstop = new PitstopPostDto()
        {
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

        apiChannel.Writer.WriteAsync(currentPitstop);
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
