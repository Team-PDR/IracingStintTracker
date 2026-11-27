"""Derive a car's race timeline (stints, pit stops, drive-throughs, laps) from
its events.

This is a pure function of the event list: no database, no clock. Events may
come from several clients (one per driver) and in any arrival order; they are
sorted by session time first, so the result does not depend on arrival order.
The rules are documented in docs/event-schema.md ("Derivation rules").
"""

from dataclasses import dataclass, field
from typing import Any, Iterable, Optional

from app.schemas.event_schemas import CarState, Conditions, Driver, TiresRequested
from app.schemas.timeline_schemas import (
    CarLocation,
    CarTimeline,
    DerivedDriveThrough,
    DerivedLap,
    DerivedPitStop,
    DerivedStint,
    DriverSwap,
    LiveState,
    SessionMeta,
    StintEndReason,
)


@dataclass(frozen=True)
class StoredEvent:
    """The subset of a stored event the derivation needs."""

    type: str
    session_time: float
    session_tick: int
    driver: Driver
    payload: dict[str, Any]
    seq: int = 0  # insertion order; last-resort tie-breaker


# Order of event types that share the same session tick. For example, the
# race start comes before a driver entering at the same moment, and a lap
# crossing comes before the checkered flag.
_TYPE_ORDER = {
    "session_info": 0,
    "race_started": 1,
    "driver_entered": 2,
    "pit_road_entered": 3,
    "pit_box_entered": 4,
    "lap_completed": 5,
    "car_status": 6,
    "pit_box_exited": 7,
    "pit_road_exited": 8,
    "driver_exited": 9,
    "race_finished": 10,
}

_NO_TIRES = TiresRequested(lf=False, rf=False, lr=False, rr=False)


def _sort_key(ev: StoredEvent) -> tuple:
    return (ev.session_time, ev.session_tick, _TYPE_ORDER.get(ev.type, 99), ev.seq)


@dataclass
class _Visit:
    """One pass through pit lane, open until the pit road exit is seen."""

    road_enter_time: float
    entry_observed: bool
    via_tow: bool = False
    service_start_time: Optional[float] = None
    service_end_time: Optional[float] = None
    fuel_start: float = 0.0
    fuel_end: Optional[float] = None
    required_repair_s: float = 0.0
    optional_repair_s: float = 0.0
    fast_repair_available: int = 0
    tires: TiresRequested = field(default_factory=lambda: _NO_TIRES)
    road_exit_time: Optional[float] = None
    exited_driver: Optional[Driver] = None
    swap: Optional[DriverSwap] = None

    @property
    def has_service(self) -> bool:
        return self.service_start_time is not None

    @property
    def in_box(self) -> bool:
        return self.has_service and self.service_end_time is None


class _Deriver:
    # pylint: disable=too-many-instance-attributes

    def __init__(self) -> None:
        self.session: Optional[SessionMeta] = None
        self.race_start_time: Optional[float] = None
        self.race_finish_time: Optional[float] = None

        self.driver_in_car: Optional[Driver] = None
        self.entered_state: Optional[tuple[CarState, Conditions]] = None

        self.stint: Optional[DerivedStint] = None
        self.stints: list[DerivedStint] = []

        self.visit: Optional[_Visit] = None
        self.pit_stops: list[DerivedPitStop] = []
        self.drive_throughs: list[DerivedDriveThrough] = []

        self.laps: dict[int, DerivedLap] = {}

        self.location: CarLocation = "unknown"
        self.last_time: Optional[float] = None
        self.last_state: Optional[CarState] = None

    # ---------- helpers ----------

    @property
    def racing(self) -> bool:
        return self.race_start_time is not None and self.race_finish_time is None

    def _start_stint(
        self,
        time: float,
        driver: Driver,
        state: CarState,
        conditions: Optional[Conditions],
    ) -> None:
        if self.stint is not None or not self.racing:
            return
        self.stint = DerivedStint(
            number=len(self.stints) + 1,
            driver=driver,
            start_time=time,
            start_state=state,
            conditions=conditions,
        )
        self.stints.append(self.stint)

    def _end_stint(
        self, time: float, state: Optional[CarState], reason: StintEndReason
    ) -> None:
        if self.stint is None:
            return
        self.stint.end_time = time
        self.stint.end_state = state
        self.stint.end_reason = reason
        self.stint = None

    def _open_visit(self, time: float, entry_observed: bool) -> _Visit:
        self.visit = _Visit(road_enter_time=time, entry_observed=entry_observed)
        return self.visit

    def _close_visit(self, status: str) -> None:
        """Record the open visit as a pit stop (if the car stopped in the box)
        or as a drive-through (if it did not)."""
        v = self.visit
        if v is None:
            return
        self.visit = None

        if not v.has_service:
            self.drive_throughs.append(
                DerivedDriveThrough(
                    via_tow=v.via_tow,
                    road_enter_time=v.road_enter_time,
                    road_exit_time=v.road_exit_time,
                )
            )
            return

        self.pit_stops.append(
            DerivedPitStop(
                number=len(self.pit_stops) + 1,
                status=status,
                entry_observed=v.entry_observed,
                via_tow=v.via_tow,
                road_enter_time=v.road_enter_time,
                road_exit_time=v.road_exit_time,
                service_start_time=v.service_start_time,
                service_end_time=v.service_end_time,
                fuel_start=v.fuel_start,
                fuel_end=v.fuel_end,
                required_repair_s=v.required_repair_s,
                optional_repair_s=v.optional_repair_s,
                fast_repair_available=v.fast_repair_available,
                tires_changed=v.tires,
                swap=v.swap,
            )
        )

    @staticmethod
    def _location(on_pit_road: bool, in_pit_box: bool) -> CarLocation:
        if in_pit_box:
            return "in_pit_box"
        if on_pit_road:
            return "in_pit_lane"
        return "on_track"

    # ---------- event handlers ----------

    def apply(self, ev: StoredEvent) -> None:
        handler = getattr(self, f"_on_{ev.type}", None)
        if handler is None:
            return

        # Nothing about the car changes after the checkered flag, except the
        # final lap crossing, which can share the finish tick.
        if self.race_finish_time is not None and not (
            ev.type == "lap_completed" and ev.session_time <= self.race_finish_time
        ):
            return

        handler(ev)

        state = ev.payload.get("car_state")
        if state is not None:
            self.last_state = CarState(**state)
            self.last_time = ev.session_time

    def _on_session_info(self, ev: StoredEvent) -> None:
        if self.session is None:
            self.session = SessionMeta(**ev.payload)

    def _on_race_started(self, ev: StoredEvent) -> None:
        if self.race_start_time is not None:
            return
        self.race_start_time = ev.session_time
        # A driver who was already in the car before the start.
        if (
            self.driver_in_car is not None
            and self.entered_state is not None
            and self.visit is None
        ):
            state, conditions = self.entered_state
            self._start_stint(ev.session_time, self.driver_in_car, state, conditions)

    def _on_driver_entered(self, ev: StoredEvent) -> None:
        p = ev.payload
        state = CarState(**p["car_state"])
        conditions = Conditions(**p["conditions"])
        self.driver_in_car = ev.driver
        self.entered_state = (state, conditions)
        self.location = self._location(p["on_pit_road"], p["in_pit_box"])

        if p["on_pit_road"] or p["in_pit_box"]:
            # Joining a pit stop in progress (normally the second half of a
            # driver swap). This must not start a new stop.
            v = self.visit or self._open_visit(ev.session_time, entry_observed=False)

            prev = v.exited_driver
            if prev is not None and prev.user_id != ev.driver.user_id and v.swap is None:
                v.swap = DriverSwap(
                    from_driver=prev, to_driver=ev.driver, time=ev.session_time
                )
            v.exited_driver = None

            if p["in_pit_box"] and not v.has_service:
                # The box entry was never reported, so the service details
                # (repairs, tires) are unknown.
                v.entry_observed = False
                v.service_start_time = ev.session_time
                v.fuel_start = state.fuel

            if self.stint is not None and self.stint.driver.user_id != ev.driver.user_id:
                self._end_stint(self.last_time or ev.session_time, self.last_state, "superseded")

            if not p["in_pit_box"] and not v.has_service:
                # On pit road but not stopped, with no stop in progress: a pit
                # lane start. The stint begins now, not at a box exit.
                self._start_stint(ev.session_time, ev.driver, state, conditions)
            return

        # Entering on track.
        if self.visit is not None:
            # The previous pit road exit was never reported.
            self._close_visit("abandoned")

        if self.stint is not None:
            if self.stint.driver.user_id == ev.driver.user_id:
                return  # same driver again, e.g. the app restarted mid-stint
            self._end_stint(self.last_time or ev.session_time, self.last_state, "superseded")

        self._start_stint(ev.session_time, ev.driver, state, conditions)

    def _on_driver_exited(self, ev: StoredEvent) -> None:
        p = ev.payload
        if self.driver_in_car is not None and self.driver_in_car.user_id == ev.driver.user_id:
            self.driver_in_car = None
        self.location = self._location(p["on_pit_road"], p["in_pit_box"])

        if p["on_pit_road"] or p["in_pit_box"]:
            # Remember who left, so the next driver_entered records a swap.
            v = self.visit or self._open_visit(ev.session_time, entry_observed=False)
            v.exited_driver = ev.driver

        # In the pits the stint was normally already ended by pit_box_entered.
        self._end_stint(ev.session_time, CarState(**p["car_state"]), "driver_exited")

    def _on_pit_road_entered(self, ev: StoredEvent) -> None:
        if self.visit is not None:
            if self.location in ("in_pit_lane", "in_pit_box"):
                # Already in pit lane: a duplicate or spurious edge (for
                # example from a client that joined mid-stop).
                return
            # The car got back on track without a reported pit road exit.
            self._close_visit("abandoned")

        v = self._open_visit(ev.session_time, entry_observed=True)
        v.via_tow = bool(ev.payload.get("via_tow", False))
        self.location = "in_pit_lane"

    def _on_pit_box_entered(self, ev: StoredEvent) -> None:
        p = ev.payload
        v = self.visit

        if v is not None and v.in_box:
            return  # already in the box: duplicate or spurious edge

        if v is not None and v.has_service:
            # A second box entry after this visit's service ended means the
            # pit road exit was missed and this is a new stop.
            self._close_visit("abandoned")
            v = None

        if v is None:
            v = self._open_visit(ev.session_time, entry_observed=False)

        state = CarState(**p["car_state"])
        v.service_start_time = ev.session_time
        v.fuel_start = state.fuel
        v.required_repair_s = p["required_repair_s"]
        v.optional_repair_s = p["optional_repair_s"]
        v.fast_repair_available = p["fast_repair_available"]
        v.tires = TiresRequested(**p["tires_requested"])
        self.location = "in_pit_box"

        self._end_stint(ev.session_time, state, "pit_stop")

    def _on_pit_box_exited(self, ev: StoredEvent) -> None:
        p = ev.payload
        state = CarState(**p["car_state"])
        v = self.visit
        if v is not None and v.has_service:
            v.service_end_time = ev.session_time
            v.fuel_end = state.fuel
        self.location = "in_pit_lane"

        self._start_stint(ev.session_time, ev.driver, state, Conditions(**p["conditions"]))

    def _on_pit_road_exited(self, ev: StoredEvent) -> None:
        self.location = "on_track"
        if self.visit is None:
            return
        self.visit.road_exit_time = ev.session_time
        self._close_visit("complete")

    def _on_lap_completed(self, ev: StoredEvent) -> None:
        p = ev.payload
        number = p["lap_number"]
        if number in self.laps:
            return  # keep the first report of each lap
        lap_time = p["lap_time"]
        self.laps[number] = DerivedLap(
            number=number,
            lap_time=lap_time if lap_time > 0 else None,
            end_time=ev.session_time,
            end_fuel=p["car_state"]["fuel"],
            driver=ev.driver,
        )

    def _on_car_status(self, ev: StoredEvent) -> None:
        p = ev.payload
        self.location = self._location(p["on_pit_road"], p["in_pit_box"])

    def _on_race_finished(self, ev: StoredEvent) -> None:
        state = CarState(**ev.payload["car_state"])
        self._end_stint(ev.session_time, state, "race_finished")
        if self.visit is not None:
            self._close_visit("abandoned")
        self.race_finish_time = ev.session_time
        self.location = "finished"

    # ---------- result ----------

    def finish(self, session_id: int, car_idx: int) -> CarTimeline:
        if self.visit is not None and self.visit.has_service:
            # Still in the pits: show the stop as open. A visit with no box
            # entry yet can't be classified, so it only shows as "in pit lane".
            self._close_visit("open")

        laps = [self.laps[n] for n in sorted(self.laps)]
        for lap in laps:
            for stint in self.stints:
                end = stint.end_time if stint.end_time is not None else float("inf")
                if stint.start_time <= lap.end_time <= end:
                    lap.stint_number = stint.number
                    stint.lap_numbers.append(lap.number)
                    break

        return CarTimeline(
            session_id=session_id,
            car_idx=car_idx,
            session=self.session,
            race_start_time=self.race_start_time,
            race_finish_time=self.race_finish_time,
            stints=self.stints,
            pit_stops=self.pit_stops,
            drive_throughs=self.drive_throughs,
            laps=laps,
            live=LiveState(
                location=self.location,
                driver=self.driver_in_car,
                last_update_time=self.last_time,
                last_state=self.last_state,
            ),
        )


def derive_car_timeline(
    session_id: int, car_idx: int, events: Iterable[StoredEvent]
) -> CarTimeline:
    deriver = _Deriver()
    for ev in sorted(events, key=_sort_key):
        deriver.apply(ev)
    return deriver.finish(session_id, car_idx)
