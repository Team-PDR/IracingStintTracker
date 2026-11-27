"""Checks that events produced by the C# client pass the API's validation and
derive the expected timeline.

Opt-in: generate the sample from the repo root with

    $env:STINT_CONTRACT_OUT = "$PWD\\api\\tests\\client_sample.json"
    dotnet test ingester.Tests --filter ContractSampleTests

then run pytest. Skipped when the sample file doesn't exist.
"""

import json
from pathlib import Path

import pytest

from app.schemas.event_schemas import Driver, any_event_adapter
from app.services.derivation import StoredEvent, derive_car_timeline

SAMPLE = Path(__file__).parent / "client_sample.json"


@pytest.mark.skipif(not SAMPLE.exists(), reason="no C# client sample; see module docstring")
def test_client_events_validate_and_derive_one_swap_stop():
    raw = json.loads(SAMPLE.read_text(encoding="utf-8"))
    events = [any_event_adapter.validate_python(e) for e in raw]

    timeline = derive_car_timeline(
        events[0].session_id,
        events[0].car_idx,
        [
            StoredEvent(
                type=e.type,
                session_time=e.session_time,
                session_tick=e.session_tick,
                driver=Driver(**r["driver"]),
                payload=r.get("payload", {}),
                seq=i,
            )
            for i, (e, r) in enumerate(zip(events, raw))
        ],
    )

    assert timeline.session.track == "Spa"
    assert len(timeline.pit_stops) == 1
    stop = timeline.pit_stops[0]
    assert stop.status == "complete"
    assert (stop.road_enter_time, stop.service_start_time) == (8289, 8412)
    assert (stop.service_end_time, stop.road_exit_time) == (9266, 9269)
    assert stop.tires_changed.lf and stop.tires_changed.rr
    assert stop.swap.from_driver.name == "Kam Wilson"
    assert stop.swap.to_driver.name == "Gregory Toumaian"
    assert [s.driver.name for s in timeline.stints] == ["Kam Wilson", "Gregory Toumaian"]
    assert [lap.number for lap in timeline.laps] == [1]
