"""Helpers for building events in the wire format of docs/event-schema.md."""

import random
import uuid
from typing import Any, Optional

from app.schemas.event_schemas import Driver
from app.services.derivation import StoredEvent

KAM = {"user_id": 1001, "name": "Kam Wilson"}
GREG = {"user_id": 1002, "name": "Gregory Toumaian"}
GRAYSON = {"user_id": 1003, "name": "Grayson Langlais"}

CONDITIONS = {"track_temp": 31.5, "track_wetness": 1, "skies": 1, "precipitation": 0.0}
CLIENT = {"install_id": str(uuid.UUID(int=1)), "app_version": "2.0.0-test"}


def car_state(fuel: float = 50.0, position: int = 5, incidents: int = 0, lap: int = 1) -> dict:
    return {
        "fuel": fuel,
        "class_position": position,
        "incidents": incidents,
        "lap": lap,
        "laps_completed": max(lap - 1, 0),
    }


class Race:
    """Builds events for one car. Each method returns one event dict."""

    def __init__(self, session_id: Optional[int] = None, car_idx: int = 7):
        self.session_id = session_id if session_id is not None else random.randint(10**7, 10**8)
        self.car_idx = car_idx

    def event(self, type_: str, t: float, driver: dict, payload: Optional[dict] = None) -> dict:
        ev: dict[str, Any] = {
            "schema_version": 1,
            "event_id": str(uuid.uuid4()),
            "type": type_,
            "session_id": self.session_id,
            "car_idx": self.car_idx,
            "session_num": 2,
            "session_time": t,
            "session_tick": int(t * 60),
            "driver": driver,
            "client": CLIENT,
            "sent_at_utc": "2026-11-23T18:00:00Z",
        }
        if payload is not None:
            ev["payload"] = payload
        return ev

    def session_info(self, t: float, d: dict) -> dict:
        return self.event("session_info", t, d, {
            "track": "Spa", "car_name": "Cadillac V-Series.R", "car_class": "GTP",
            "team_id": 555, "team_name": "PDR", "car_number": "7",
            "race_duration_s": 21600, "session_date": "2026-11-23",
        })

    def race_started(self, t: float, d: dict) -> dict:
        return self.event("race_started", t, d)

    def entered(self, t: float, d: dict, pit_road=False, box=False, fuel=50.0, incidents=0) -> dict:
        return self.event("driver_entered", t, d, {
            "car_state": car_state(fuel, incidents=incidents), "conditions": CONDITIONS,
            "on_pit_road": pit_road or box, "in_pit_box": box,
        })

    def exited(self, t: float, d: dict, pit_road=False, box=False, fuel=50.0, incidents=0) -> dict:
        return self.event("driver_exited", t, d, {
            "car_state": car_state(fuel, incidents=incidents), "conditions": CONDITIONS,
            "on_pit_road": pit_road or box, "in_pit_box": box,
        })

    def road_in(self, t: float, d: dict, fuel=5.0, tow=False) -> dict:
        return self.event("pit_road_entered", t, d, {"car_state": car_state(fuel), "via_tow": tow})

    def box_in(self, t: float, d: dict, fuel=5.0, tires=True, required=0.0, optional=0.0) -> dict:
        return self.event("pit_box_entered", t, d, {
            "car_state": car_state(fuel), "required_repair_s": required,
            "optional_repair_s": optional, "fast_repair_available": 1,
            "tires_requested": {"lf": tires, "rf": tires, "lr": tires, "rr": tires},
        })

    def box_out(self, t: float, d: dict, fuel=100.0) -> dict:
        return self.event("pit_box_exited", t, d, {"car_state": car_state(fuel), "conditions": CONDITIONS})

    def road_out(self, t: float, d: dict, fuel=100.0) -> dict:
        return self.event("pit_road_exited", t, d, {"car_state": car_state(fuel)})

    def lap(self, t: float, d: dict, number: int, lap_time: float, fuel=40.0) -> dict:
        return self.event("lap_completed", t, d, {
            "lap_number": number, "lap_time": lap_time, "car_state": car_state(fuel, lap=number + 1),
        })

    def status(self, t: float, d: dict, pit_road=False, box=False, fuel=40.0) -> dict:
        return self.event("car_status", t, d, {
            "car_state": car_state(fuel), "on_pit_road": pit_road or box, "in_pit_box": box,
        })

    def finished(self, t: float, d: dict, fuel=3.0, pit_road=False, position=2, incidents=4) -> dict:
        return self.event("race_finished", t, d, {
            "car_state": car_state(fuel, position=position, incidents=incidents),
            "on_pit_road": pit_road,
        })


def stored(events: list[dict]) -> list[StoredEvent]:
    """Convert wire-format events to what the derivation consumes."""
    return [
        StoredEvent(
            type=e["type"],
            session_time=e["session_time"],
            session_tick=e["session_tick"],
            driver=Driver(**e["driver"]),
            payload=e.get("payload", {}),
            seq=i,
        )
        for i, e in enumerate(events)
    ]
