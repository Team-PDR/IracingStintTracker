"""Derivation rules from docs/event-schema.md, including the driver-swap
timelines that the old POST/PATCH design got wrong."""

import random

from app.services.derivation import derive_car_timeline
from tests.builders import GRAYSON, GREG, KAM, Race, stored


def derive(race: Race, events: list[dict]):
    return derive_car_timeline(race.session_id, race.car_idx, stored(events))


def swap_at_86593512(r: Race) -> list[dict]:
    """The first swap in real session 86593512 (Kam -> Gregory), with the
    events each client should send under the new contract."""
    return [
        r.session_info(0, KAM),
        r.race_started(344, KAM),
        r.entered(344, KAM, fuel=100.0),
        r.lap(500, KAM, 1, 155.2),
        # Kam's client: the first half of the stop.
        r.road_in(8289, KAM, fuel=6.0),
        r.box_in(8412, KAM, fuel=2.1),
        r.exited(8420, KAM, box=True, fuel=2.1, incidents=3),
        # Gregory's client: joins mid-stop and finishes it.
        r.entered(8644, GREG, box=True, fuel=60.0),
        r.box_out(9266, GREG, fuel=100.0),
        r.road_out(9269, GREG),
        r.lap(9400, GREG, 60, 160.1),
    ]


def test_single_driver_race_with_one_stop():
    r = Race()
    t = derive(r, [
        r.session_info(0, KAM),
        r.race_started(100, KAM),
        r.entered(100, KAM, fuel=100.0),
        r.lap(250, KAM, 1, 150.0),
        r.road_in(3000, KAM),
        r.box_in(3020, KAM, fuel=4.0, required=12.5),
        r.box_out(3060, KAM, fuel=100.0),
        r.road_out(3070, KAM),
        r.lap(3200, KAM, 20, 151.0),
        r.finished(6000, KAM),
    ])

    assert t.session.track == "Spa"
    assert t.race_start_time == 100
    assert [(s.driver.name, s.start_time, s.end_time, s.end_reason) for s in t.stints] == [
        ("Kam Wilson", 100, 3020, "pit_stop"),
        ("Kam Wilson", 3060, 6000, "race_finished"),
    ]
    assert len(t.pit_stops) == 1
    stop = t.pit_stops[0]
    assert (stop.status, stop.road_enter_time, stop.road_exit_time) == ("complete", 3000, 3070)
    assert (stop.service_time, stop.pit_lane_time, stop.refuel_amount) == (40, 70, 96.0)
    assert stop.required_repair_s == 12.5
    assert stop.swap is None
    assert [s.lap_numbers for s in t.stints] == [[1], [20]]
    assert t.live.location == "finished"


def test_driver_swap_produces_one_complete_stop():
    r = Race()
    t = derive(r, swap_at_86593512(r))

    assert len(t.pit_stops) == 1
    stop = t.pit_stops[0]
    assert stop.status == "complete"
    assert stop.entry_observed
    # Entry half from Kam's client, exit half from Gregory's.
    assert (stop.road_enter_time, stop.service_start_time) == (8289, 8412)
    assert (stop.service_end_time, stop.road_exit_time) == (9266, 9269)
    assert (stop.fuel_start, stop.fuel_end) == (2.1, 100.0)
    assert stop.tires_changed.lf
    assert stop.swap.from_driver.name == "Kam Wilson"
    assert stop.swap.to_driver.name == "Gregory Toumaian"
    assert stop.swap.time == 8644

    # Every stint belongs to exactly one driver.
    assert [(s.driver.name, s.start_time, s.end_time) for s in t.stints] == [
        ("Kam Wilson", 344, 8412),
        ("Gregory Toumaian", 9266, None),
    ]
    assert t.stints[0].end_reason == "pit_stop"


def test_spurious_entry_edges_from_unseeded_client_are_ignored():
    """The old client fired pit_road_entered and pit_box_entered on the
    joining driver's first tick. That created the duplicate stops in the
    database; the derivation must not."""
    r = Race()
    events = swap_at_86593512(r)
    events += [r.road_in(8644, GREG, fuel=60.0), r.box_in(8644, GREG, fuel=60.0, tires=False)]

    t = derive(r, events)

    assert len(t.pit_stops) == 1
    stop = t.pit_stops[0]
    assert (stop.road_enter_time, stop.service_start_time, stop.fuel_start) == (8289, 8412, 2.1)
    assert stop.tires_changed.lf  # Kam's tire request, not the spurious one
    assert len(t.drive_throughs) == 0


def test_result_does_not_depend_on_arrival_order():
    r = Race()
    events = swap_at_86593512(r)
    expected = derive(r, events).model_dump()

    for seed in range(20):
        shuffled = events[:]
        random.Random(seed).shuffle(shuffled)
        assert derive(r, shuffled).model_dump() == expected


def test_drive_through_does_not_end_stint_or_count_as_stop():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.road_in(2000, KAM),
        r.road_out(2045, KAM),
        r.lap(2100, KAM, 13, 190.0),
    ])

    assert t.pit_stops == []
    assert len(t.drive_throughs) == 1
    assert (t.drive_throughs[0].road_enter_time, t.drive_throughs[0].road_exit_time) == (2000, 2045)
    assert len(t.stints) == 1 and t.stints[0].end_time is None
    assert t.stints[0].lap_numbers == [13]


def test_tow_is_recorded_on_the_stop():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.road_in(1500, KAM, tow=True),
        r.box_in(1500, KAM, required=45.0),
        r.box_out(1560, KAM),
        r.road_out(1570, KAM),
    ])

    assert t.pit_stops[0].via_tow
    assert t.pit_stops[0].required_repair_s == 45.0


def test_stop_still_in_progress_is_open_and_live_state_shows_swap():
    r = Race()
    events = swap_at_86593512(r)[:7]  # Kam has left the car; Gregory not in yet

    t = derive(r, events)

    assert [s.status for s in t.pit_stops] == ["open"]
    assert t.pit_stops[0].service_end_time is None
    assert t.live.location == "in_pit_box"
    assert t.live.driver is None


def test_car_in_pit_lane_before_box_is_not_yet_classified():
    r = Race()
    t = derive(r, [r.race_started(100, KAM), r.entered(100, KAM), r.road_in(900, KAM)])

    assert t.pit_stops == [] and t.drive_throughs == []
    assert t.live.location == "in_pit_lane"


def test_race_finish_closes_stint_and_ignores_later_events():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.lap(5000, KAM, 30, 150.0),
        r.lap(5150, KAM, 31, 150.0),
        r.finished(5150, KAM),
        # cool-down lap into the pits
        r.road_in(5300, KAM),
        r.box_in(5320, KAM),
        r.lap(5400, KAM, 32, 250.0),
    ])

    assert t.race_finish_time == 5150
    assert [(s.end_time, s.end_reason) for s in t.stints] == [(5150, "race_finished")]
    assert t.stints[0].incidents == 4
    assert t.pit_stops == []
    assert [lap.number for lap in t.laps] == [30, 31]


def test_race_finish_during_stop_abandons_it():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.road_in(5000, KAM),
        r.box_in(5020, KAM),
        r.box_out(5060, KAM),
        r.finished(5065, KAM, pit_road=True),
    ])

    assert [s.status for s in t.pit_stops] == ["abandoned"]
    assert t.stints[-1].end_reason == "race_finished"


def test_app_restart_mid_stint_does_not_split_it():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.lap(250, KAM, 1, 150.0),
        r.entered(400, KAM),  # app restarted while driving
        r.lap(550, KAM, 3, 150.0),
    ])

    assert len(t.stints) == 1
    assert t.stints[0].lap_numbers == [1, 3]


def test_driver_leaving_on_track_ends_stint_abnormally():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.exited(900, KAM, fuel=30.0),  # disconnect
        r.entered(950, KAM, fuel=30.0),
    ])

    assert [(s.start_time, s.end_time, s.end_reason) for s in t.stints] == [
        (100, 900, "driver_exited"),
        (950, None, None),
    ]
    assert t.stints[0].ended_abnormally


def test_different_driver_on_track_supersedes_unclosed_stint():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.status(700, KAM, fuel=35.0),
        r.entered(800, GRAYSON),  # Kam's client never reported leaving
    ])

    assert [(s.driver.name, s.end_time, s.end_reason) for s in t.stints] == [
        ("Kam Wilson", 700, "superseded"),
        ("Grayson Langlais", None, None),
    ]
    assert t.stints[0].end_state.fuel == 35.0


def test_missed_pit_road_exit_then_new_stop():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.road_in(3000, KAM),
        r.box_in(3020, KAM),
        r.box_out(3060, KAM),
        # pit_road_exited lost; the periodic status shows the car on track
        r.status(3100, KAM),
        r.road_in(6000, KAM),
        r.box_in(6020, KAM),
        r.box_out(6060, KAM),
        r.road_out(6070, KAM),
    ])

    assert [(s.status, s.road_enter_time) for s in t.pit_stops] == [
        ("abandoned", 3000),
        ("complete", 6000),
    ]
    assert len(t.stints) == 3


def test_driver_in_car_before_race_start_gets_stint_at_start():
    r = Race()
    t = derive(r, [
        r.entered(50, KAM, fuel=100.0),
        r.race_started(120, KAM),
    ])

    assert [(s.driver.name, s.start_time) for s in t.stints] == [("Kam Wilson", 120)]
    assert t.stints[0].start_state.fuel == 100.0


def test_pit_lane_start_begins_a_stint_and_is_a_drive_through():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM, pit_road=True),
        r.road_out(130, KAM),
        r.lap(300, KAM, 1, 200.0),
    ])

    assert [(s.driver.name, s.start_time, s.end_time) for s in t.stints] == [("Kam Wilson", 100, None)]
    assert t.pit_stops == []
    assert len(t.drive_throughs) == 1
    assert t.stints[0].lap_numbers == [1]


def test_laps_deduplicated_invalid_times_nulled_and_assigned_to_stints():
    r = Race()
    t = derive(r, [
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.lap(250, KAM, 1, 150.0),
        r.lap(250, KAM, 1, 150.0),  # resent by the outbox with a new id
        r.lap(400, KAM, 2, -1.0),  # invalidated by iRacing
        r.road_in(3000, KAM),
        r.box_in(3020, KAM),
        r.exited(3025, KAM, box=True),
        r.entered(3100, GREG, box=True),
        r.box_out(3160, GREG),
        r.road_out(3170, GREG),
        r.lap(3300, GREG, 21, 155.0),
    ])

    assert [(lap.number, lap.lap_time, lap.stint_number, lap.driver.name) for lap in t.laps] == [
        (1, 150.0, 1, "Kam Wilson"),
        (2, None, 1, "Kam Wilson"),
        (21, 155.0, 2, "Gregory Toumaian"),
    ]
