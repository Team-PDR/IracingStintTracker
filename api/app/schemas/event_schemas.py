"""Ingest contract for ingester events. Mirrors docs/event-schema.md (v1)."""

import uuid
from datetime import datetime
from typing import Annotated, Any, Literal, Optional, Union

from pydantic import BaseModel, Field, TypeAdapter

MAX_BATCH_SIZE = 100


# ---------- shared blocks ----------


class Driver(BaseModel):
    user_id: int
    name: str


class Client(BaseModel):
    install_id: uuid.UUID
    app_version: str = Field(max_length=32)


class CarState(BaseModel):
    fuel: float
    class_position: int
    incidents: int
    lap: int
    laps_completed: int


class Conditions(BaseModel):
    track_temp: float
    track_wetness: int
    skies: int
    precipitation: float


class TiresRequested(BaseModel):
    lf: bool
    rf: bool
    lr: bool
    rr: bool


# ---------- payloads ----------


class EmptyPayload(BaseModel):
    pass


class SessionInfoPayload(BaseModel):
    track: str
    car_name: str
    car_class: str
    team_id: int
    team_name: str
    car_number: str
    race_duration_s: int
    session_date: str  # client's local date, ISO yyyy-mm-dd


class DriverEnteredPayload(BaseModel):
    car_state: CarState
    conditions: Conditions
    on_pit_road: bool
    in_pit_box: bool


class DriverExitedPayload(BaseModel):
    car_state: CarState
    conditions: Conditions
    on_pit_road: bool
    in_pit_box: bool


class PitRoadEnteredPayload(BaseModel):
    car_state: CarState
    via_tow: bool


class PitBoxEnteredPayload(BaseModel):
    car_state: CarState
    required_repair_s: float
    optional_repair_s: float
    fast_repair_available: int
    tires_requested: TiresRequested


class PitBoxExitedPayload(BaseModel):
    car_state: CarState
    conditions: Conditions


class PitRoadExitedPayload(BaseModel):
    car_state: CarState


class LapCompletedPayload(BaseModel):
    lap_number: int
    lap_time: float
    car_state: CarState


class RaceFinishedPayload(BaseModel):
    car_state: CarState
    on_pit_road: bool


class CarStatusPayload(BaseModel):
    car_state: CarState
    on_pit_road: bool
    in_pit_box: bool


# ---------- envelope ----------


class EventBase(BaseModel):
    schema_version: Literal[1]
    event_id: uuid.UUID
    session_id: int
    car_idx: int = Field(ge=0)
    session_num: int = Field(ge=0)
    session_time: float = Field(ge=0)
    session_tick: int = Field(ge=0)
    driver: Driver
    client: Client
    sent_at_utc: datetime


class SessionInfoEvent(EventBase):
    type: Literal["session_info"]
    payload: SessionInfoPayload


class RaceStartedEvent(EventBase):
    type: Literal["race_started"]
    payload: EmptyPayload = EmptyPayload()


class DriverEnteredEvent(EventBase):
    type: Literal["driver_entered"]
    payload: DriverEnteredPayload


class DriverExitedEvent(EventBase):
    type: Literal["driver_exited"]
    payload: DriverExitedPayload


class PitRoadEnteredEvent(EventBase):
    type: Literal["pit_road_entered"]
    payload: PitRoadEnteredPayload


class PitBoxEnteredEvent(EventBase):
    type: Literal["pit_box_entered"]
    payload: PitBoxEnteredPayload


class PitBoxExitedEvent(EventBase):
    type: Literal["pit_box_exited"]
    payload: PitBoxExitedPayload


class PitRoadExitedEvent(EventBase):
    type: Literal["pit_road_exited"]
    payload: PitRoadExitedPayload


class LapCompletedEvent(EventBase):
    type: Literal["lap_completed"]
    payload: LapCompletedPayload


class RaceFinishedEvent(EventBase):
    type: Literal["race_finished"]
    payload: RaceFinishedPayload


class CarStatusEvent(EventBase):
    type: Literal["car_status"]
    payload: CarStatusPayload


AnyEvent = Annotated[
    Union[
        SessionInfoEvent,
        RaceStartedEvent,
        DriverEnteredEvent,
        DriverExitedEvent,
        PitRoadEnteredEvent,
        PitBoxEnteredEvent,
        PitBoxExitedEvent,
        PitRoadExitedEvent,
        LapCompletedEvent,
        RaceFinishedEvent,
        CarStatusEvent,
    ],
    Field(discriminator="type"),
]

any_event_adapter: TypeAdapter[AnyEvent] = TypeAdapter(AnyEvent)


# ---------- ingest request / response ----------


class EventBatchIn(BaseModel):
    # Events are validated one by one in the route, so a single bad event is
    # rejected without failing the rest of the batch.
    events: list[dict[str, Any]] = Field(min_length=1, max_length=MAX_BATCH_SIZE)


class RejectedEvent(BaseModel):
    id: Optional[str]
    reason: str


class EventBatchResult(BaseModel):
    accepted: list[uuid.UUID] = []
    duplicates: list[uuid.UUID] = []
    rejected: list[RejectedEvent] = []
