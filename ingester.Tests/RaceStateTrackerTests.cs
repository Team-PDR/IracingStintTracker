using IRSDKSharper;
using StintTracker.Core.Events;
using StintTracker.Core.Tracking;
using static StintTracker.Tests.Frames;

namespace StintTracker.Tests;

public class RaceStateTrackerTests
{
    private static readonly SessionContext KamCtx = Context(Kam);
    private static readonly SessionContext GregCtx = Context(Greg);

    [Fact]
    public void Race_start_while_driving_emits_session_info_race_started_and_driver_entered()
    {
        var tracker = Tracker();
        var gridding = At(KamCtx, 90, driving: true) with { SessionState = IRacingSdkEnum.SessionState.ParadeLaps };

        Assert.Empty(tracker.Process(gridding));
        var events = tracker.Process(At(KamCtx, 100, driving: true));

        Assert.Equal(["session_info", "race_started", "driver_entered"], Types(events));
        var entered = (DriverChangePayload)events[2].Payload;
        Assert.False(entered.OnPitRoad);
        Assert.False(entered.InPitBox);
        Assert.Equal(21600, ((SessionInfoPayload)events[0].Payload).RaceDurationS);
        Assert.True(tracker.RaceStarted);
    }

    [Fact]
    public void Non_race_sessions_emit_nothing()
    {
        var tracker = Tracker();
        var practice = At(KamCtx, 100, driving: true) with { SessionNum = 0 };

        Assert.Empty(tracker.Process(practice));
        Assert.False(tracker.RaceStarted);
    }

    [Fact]
    public void Single_driver_pit_stop_emits_the_four_edges_in_order()
    {
        var tracker = Tracker();
        var events = Run(tracker, [
            At(KamCtx, 100, true),
            At(KamCtx, 3000, true) with { OnPitRoad = true },
            At(KamCtx, 3020, true) with { OnPitRoad = true, PitstopActive = true, FuelLevel = 4f, PitRepairLeft = 12.5f, TireChangeLF = true },
            At(KamCtx, 3060, true) with { OnPitRoad = true, FuelLevel = 100f },
            At(KamCtx, 3070, true) with { FuelLevel = 100f },
        ]);

        Assert.Equal(
            ["session_info", "race_started", "driver_entered",
             "pit_road_entered", "pit_box_entered", "pit_box_exited", "pit_road_exited"],
            Types(events));

        var boxIn = (PitBoxEnteredPayload)events[4].Payload;
        Assert.Equal(4f, boxIn.CarState.Fuel);
        Assert.Equal(12.5f, boxIn.RequiredRepairS);
        Assert.True(boxIn.TiresRequested.Lf);
        Assert.False(boxIn.TiresRequested.Rf);
        Assert.Equal(100f, ((PitBoxExitedPayload)events[5].Payload).CarState.Fuel);
    }

    [Fact]
    public void Driver_swap_the_joining_driver_sends_no_pit_entry_edges()
    {
        // Kam's client: drives in, stops, and leaves the car in the box.
        var kam = Tracker();
        var kamEvents = Run(kam, [
            At(KamCtx, 344, true),
            At(KamCtx, 8289, true) with { OnPitRoad = true },
            At(KamCtx, 8412, true) with { OnPitRoad = true, PitstopActive = true, FuelLevel = 2.1f },
            At(KamCtx, 8420, false) with { OnPitRoad = true, PitstopActive = true },
        ]);

        Assert.Equal(
            ["session_info", "race_started", "driver_entered",
             "pit_road_entered", "pit_box_entered", "driver_exited"],
            Types(kamEvents));
        var exited = (DriverChangePayload)kamEvents[^1].Payload;
        Assert.True(exited.InPitBox);
        Assert.True(exited.OnPitRoad);
        Assert.Equal(2.1f, exited.CarState.Fuel);
        Assert.Equal(8412, kamEvents[^1].SessionTime); // last tick Kam was driving

        // Gregory's client: spectating (the player-car flags read false), then
        // takes over mid-stop. This is where the old client reported a new stop.
        var greg = Tracker();
        var gregEvents = Run(greg, [
            At(GregCtx, 344, false) with { LapCompleted = 0 },
            At(GregCtx, 8500, false) with { LapCompleted = 0 },
            At(GregCtx, 8644, true) with { OnPitRoad = true, PitstopActive = true, LapCompleted = 59, FuelLevel = 60f },
            At(GregCtx, 9000, true) with { OnPitRoad = true, PitstopActive = true, LapCompleted = 59 },
            At(GregCtx, 9266, true) with { OnPitRoad = true, LapCompleted = 59, FuelLevel = 100f },
            At(GregCtx, 9269, true) with { LapCompleted = 59, FuelLevel = 100f },
        ]);

        Assert.Equal(
            ["session_info", "race_started", "driver_entered", "pit_box_exited", "pit_road_exited"],
            Types(gregEvents));
        var entered = (DriverChangePayload)gregEvents[2].Payload;
        Assert.True(entered.InPitBox);
        Assert.Equal("Gregory Toumaian", gregEvents[2].Driver.Name);
        Assert.Equal(8644, gregEvents[2].SessionTime);
    }

    [Fact]
    public void Lap_is_reported_at_crossing_time_with_the_delayed_lap_time()
    {
        var tracker = Tracker();
        var events = Run(tracker, [
            At(KamCtx, 100, true) with { LapCompleted = 9, LapLastLapTime = 150f },
            At(KamCtx, 250, true) with { LapCompleted = 10, LapLastLapTime = 150f }, // crossing; time not updated yet
            At(KamCtx, 255, true) with { LapCompleted = 10, LapLastLapTime = 149.8f },
            At(KamCtx, 259, true) with { LapCompleted = 10, LapLastLapTime = 149.8f }, // 540 ticks later
        ]);

        var lap = Assert.Single(events, e => e.Type == "lap_completed");
        var payload = (LapCompletedPayload)lap.Payload;
        Assert.Equal(10, payload.LapNumber);
        Assert.Equal(149.8f, payload.LapTime);
        Assert.Equal(250, lap.SessionTime);
    }

    [Fact]
    public void Pending_lap_is_flushed_when_the_driver_leaves_before_the_delay()
    {
        var tracker = Tracker();
        var events = Run(tracker, [
            At(KamCtx, 100, true) with { LapCompleted = 9 },
            At(KamCtx, 250, true) with { LapCompleted = 10, OnPitRoad = true, PitstopActive = true, LapLastLapTime = 170f },
            At(KamCtx, 252, false) with { LapCompleted = 10, OnPitRoad = true, PitstopActive = true },
        ]);

        Assert.Equal(["lap_completed", "driver_exited"], Types(events).TakeLast(2));
        Assert.Equal(170f, ((LapCompletedPayload)events[^2].Payload).LapTime);
    }

    [Fact]
    public void Tow_reports_one_pit_road_entry_and_no_exit_when_arriving_in_the_box()
    {
        var tracker = Tracker();
        var events = Run(tracker, [
            At(KamCtx, 100, true),
            At(KamCtx, 1500, false) with { TowTime = 30f },  // IsOnTrackCar drops during a tow
            At(KamCtx, 1510, false) with { TowTime = 20f },
            At(KamCtx, 1520, false) with { TowTime = 10f },
            At(KamCtx, 1530, true) with { OnPitRoad = true, PitstopActive = true },
            At(KamCtx, 1590, true) with { OnPitRoad = true },
            At(KamCtx, 1600, true),
        ]);

        Assert.Equal(
            ["session_info", "race_started", "driver_entered",
             "pit_road_entered", "pit_box_entered", "pit_box_exited", "pit_road_exited"],
            Types(events));
        Assert.True(((PitRoadEnteredPayload)events[3].Payload).ViaTow);
    }

    [Fact]
    public void Car_status_is_sent_at_the_configured_interval_while_driving()
    {
        var tracker = Tracker(statusIntervalS: 10);
        var frames = Enumerable.Range(0, 26).Select(i => At(KamCtx, 100 + i, true));

        var events = Run(tracker, frames);

        Assert.Equal([110.0, 120.0], events.Where(e => e.Type == "car_status").Select(e => e.SessionTime));
    }

    [Fact]
    public void Spectating_teammate_sends_only_session_events()
    {
        var tracker = Tracker(statusIntervalS: 10);
        var events = Run(tracker, [
            At(GregCtx, 100, false),
            At(GregCtx, 3000, false) with { OnPitRoad = true, PitstopActive = true, LapCompleted = 20 },
            At(GregCtx, 6000, false) with { SessionState = IRacingSdkEnum.SessionState.Checkered },
        ]);

        Assert.Equal(["session_info", "race_started"], Types(events));
        Assert.True(tracker.RaceFinished);
    }

    [Fact]
    public void Race_finish_emits_race_finished_flushes_the_final_lap_and_stops()
    {
        uint checkered = (uint)IRacingSdkEnum.Flags.Checkered;
        var tracker = Tracker();
        var events = Run(tracker, [
            At(KamCtx, 100, true) with { LapCompleted = 40 },
            At(KamCtx, 6000, true) with { LapCompleted = 40, SessionFlags = checkered },
            At(KamCtx, 6150, true) with { LapCompleted = 41, SessionFlags = checkered, LapLastLapTime = 150f },
            At(KamCtx, 6160, true) with { LapCompleted = 41, SessionFlags = checkered, LapLastLapTime = 148f, OnPitRoad = true },
            At(KamCtx, 6200, true) with { LapCompleted = 41, OnPitRoad = true, PitstopActive = true },
        ]);

        Assert.Equal(["session_info", "race_started", "driver_entered", "race_finished", "lap_completed"], Types(events));
        Assert.Equal(6150, events[3].SessionTime);
        Assert.Equal(6150, events[4].SessionTime);
        Assert.Equal(148f, ((LapCompletedPayload)events[4].Payload).LapTime);
        Assert.True(tracker.RaceFinished);
    }

    [Fact]
    public void Leaving_the_car_on_track_reports_driver_exited_off_pit_road()
    {
        var tracker = Tracker();
        var events = Run(tracker, [At(KamCtx, 100, true), At(KamCtx, 900, false)]);

        var exited = (DriverChangePayload)events[^1].Payload;
        Assert.Equal("driver_exited", events[^1].Type);
        Assert.False(exited.OnPitRoad);
        Assert.False(exited.InPitBox);
    }

    [Fact]
    public void New_subsession_resets_state()
    {
        var tracker = Tracker();
        Run(tracker, [At(KamCtx, 100, true)]);

        var next = Context(Kam, subSession: SubSession + 1);
        var events = tracker.Process(At(next, 50, true));

        Assert.Equal(["session_info", "race_started", "driver_entered"], Types(events));
        Assert.All(events, e => Assert.Equal(SubSession + 1, e.SessionId));
    }
}
