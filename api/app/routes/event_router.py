import logging
from datetime import datetime
from typing import Any

from fastapi import APIRouter, Depends, HTTPException, status
from pydantic import BaseModel, ValidationError

from app.dependencies import DbSessionDep, get_api_key
from app.repositories import event_repo
from app.schemas.event_schemas import (
    Driver,
    EventBatchIn,
    EventBatchResult,
    RejectedEvent,
    any_event_adapter,
)
from app.schemas.timeline_schemas import CarTimeline
from app.services.derivation import StoredEvent, derive_car_timeline

logger = logging.getLogger(__name__)

router = APIRouter(
    prefix="/v1",
    tags=["events"],
    dependencies=[Depends(get_api_key)],
)


def _summarize(error: ValidationError) -> str:
    first = error.errors()[0]
    loc = ".".join(str(part) for part in first["loc"])
    return f"{loc}: {first['msg']}" if loc else first["msg"]


@router.post("/events", response_model=EventBatchResult)
def ingest_events(batch: EventBatchIn, db: DbSessionDep):
    result = EventBatchResult()
    valid = []
    rows = []

    for raw in batch.events:
        try:
            ev = any_event_adapter.validate_python(raw)
        except ValidationError as e:
            raw_id = raw.get("event_id")
            result.rejected.append(
                RejectedEvent(id=str(raw_id) if raw_id is not None else None, reason=_summarize(e))
            )
            continue

        valid.append(ev)
        rows.append(
            {
                "event_id": ev.event_id,
                "schema_version": ev.schema_version,
                "type": ev.type,
                "session_id": ev.session_id,
                "car_idx": ev.car_idx,
                "session_num": ev.session_num,
                "session_time": ev.session_time,
                "session_tick": ev.session_tick,
                "driver_user_id": ev.driver.user_id,
                "driver_name": ev.driver.name,
                "client_install_id": ev.client.install_id,
                "app_version": ev.client.app_version,
                "sent_at_utc": ev.sent_at_utc,
                # Stored as received, so later derivation code can use fields
                # this version ignores.
                "payload": raw.get("payload") or {},
            }
        )

    try:
        inserted = event_repo.insert_events(db, rows)
    except Exception as e:
        logger.error("Failed to insert events: %s", e)
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
            detail="Couldn't store events",
        ) from e

    seen = set()
    for ev in valid:
        if ev.event_id in inserted and ev.event_id not in seen:
            result.accepted.append(ev.event_id)
        else:
            result.duplicates.append(ev.event_id)
        seen.add(ev.event_id)

    logger.info(
        "Ingested events: %s accepted, %s duplicates, %s rejected",
        len(result.accepted),
        len(result.duplicates),
        len(result.rejected),
    )
    return result


class StoredEventRead(BaseModel):
    event_id: str
    type: str
    session_time: float
    session_tick: int
    driver_user_id: int
    driver_name: str
    payload: dict[str, Any]
    received_at: datetime


@router.get(
    "/sessions/{session_id}/cars/{car_idx}/events",
    response_model=list[StoredEventRead],
)
def get_car_events(session_id: int, car_idx: int, db: DbSessionDep):
    """Raw events for one car, in session-time order. Mainly for debugging."""
    return [
        StoredEventRead(
            event_id=str(e.event_id),
            type=e.type,
            session_time=e.session_time,
            session_tick=e.session_tick,
            driver_user_id=e.driver_user_id,
            driver_name=e.driver_name,
            payload=e.payload,
            received_at=e.received_at,
        )
        for e in event_repo.get_car_events(db, session_id, car_idx)
    ]


@router.get(
    "/sessions/{session_id}/cars/{car_idx}/timeline",
    response_model=CarTimeline,
)
def get_car_timeline(session_id: int, car_idx: int, db: DbSessionDep):
    events = event_repo.get_car_events(db, session_id, car_idx)
    if not events:
        raise HTTPException(
            status_code=status.HTTP_404_NOT_FOUND,
            detail=f"No events for car {car_idx} in session {session_id}",
        )

    return derive_car_timeline(
        session_id,
        car_idx,
        (
            StoredEvent(
                type=e.type,
                session_time=e.session_time,
                session_tick=e.session_tick,
                driver=Driver(user_id=e.driver_user_id, name=e.driver_name),
                payload=e.payload,
                seq=e.id,
            )
            for e in events
        ),
    )
