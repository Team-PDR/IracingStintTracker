"""Derived per-car race timeline: the output of app/services/derivation.py."""

from typing import Literal, Optional

from pydantic import BaseModel, computed_field

from app.schemas.event_schemas import CarState, Conditions, Driver, TiresRequested

StintEndReason = Literal["pit_stop", "race_finished", "driver_exited", "superseded"]
PitStatus = Literal["open", "complete", "abandoned"]
CarLocation = Literal["unknown", "on_track", "in_pit_lane", "in_pit_box", "finished"]


class DerivedStint(BaseModel):
    number: int
    driver: Driver
    start_time: float
    end_time: Optional[float] = None
    start_state: CarState
    end_state: Optional[CarState] = None
    conditions: Optional[Conditions] = None
    end_reason: Optional[StintEndReason] = None
    lap_numbers: list[int] = []

    @computed_field
    @property
    def is_complete(self) -> bool:
        return self.end_time is not None

    @computed_field
    @property
    def ended_abnormally(self) -> bool:
        return self.end_reason in ("driver_exited", "superseded")

    @computed_field
    @property
    def duration(self) -> Optional[float]:
        return None if self.end_time is None else self.end_time - self.start_time

    @computed_field
    @property
    def incidents(self) -> Optional[int]:
        if self.end_state is None:
            return None
        return self.end_state.incidents - self.start_state.incidents


class DriverSwap(BaseModel):
    from_driver: Driver
    to_driver: Driver
    time: float


class DerivedPitStop(BaseModel):
    number: int
    status: PitStatus
    # False when the client never observed the pit road entry (for example
    # the app was started mid-stop), so road_enter_time is approximate.
    entry_observed: bool = True
    via_tow: bool = False
    road_enter_time: float
    road_exit_time: Optional[float] = None
    service_start_time: float
    service_end_time: Optional[float] = None
    fuel_start: float
    fuel_end: Optional[float] = None
    required_repair_s: float
    optional_repair_s: float
    fast_repair_available: int
    tires_changed: TiresRequested
    swap: Optional[DriverSwap] = None

    @computed_field
    @property
    def service_time(self) -> Optional[float]:
        if self.service_end_time is None:
            return None
        return self.service_end_time - self.service_start_time

    @computed_field
    @property
    def pit_lane_time(self) -> Optional[float]:
        if self.road_exit_time is None:
            return None
        return self.road_exit_time - self.road_enter_time

    @computed_field
    @property
    def refuel_amount(self) -> Optional[float]:
        return None if self.fuel_end is None else self.fuel_end - self.fuel_start


class DerivedDriveThrough(BaseModel):
    via_tow: bool = False
    road_enter_time: float
    road_exit_time: Optional[float] = None


class DerivedLap(BaseModel):
    number: int
    # None when iRacing reported an invalid time (<= 0).
    lap_time: Optional[float]
    end_time: float
    end_fuel: float
    driver: Driver
    stint_number: Optional[int] = None


class SessionMeta(BaseModel):
    track: str
    car_name: str
    car_class: str
    team_id: int
    team_name: str
    car_number: str
    race_duration_s: int
    session_date: str


class LiveState(BaseModel):
    location: CarLocation
    driver: Optional[Driver]
    last_update_time: Optional[float]
    last_state: Optional[CarState]


class CarTimeline(BaseModel):
    session_id: int
    car_idx: int
    session: Optional[SessionMeta]
    race_start_time: Optional[float]
    race_finish_time: Optional[float]
    stints: list[DerivedStint]
    pit_stops: list[DerivedPitStop]
    drive_throughs: list[DerivedDriveThrough]
    laps: list[DerivedLap]
    live: LiveState
