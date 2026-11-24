import uuid
from typing import Any

from sqlalchemy import select
from sqlalchemy.dialects import postgresql, sqlite
from sqlalchemy.orm import Session

from app.models import Events

_INSERT_BY_DIALECT = {
    "postgresql": postgresql.insert,
    "sqlite": sqlite.insert,
}


def insert_events(db: Session, rows: list[dict[str, Any]]) -> set[uuid.UUID]:
    """Insert events, skipping any whose event_id already exists.

    Returns the event_ids that were actually inserted. Safe to call with
    duplicates, including duplicates within the same batch.
    """
    if not rows:
        return set()

    dialect = db.get_bind().dialect.name
    insert = _INSERT_BY_DIALECT.get(dialect)
    if insert is None:
        raise RuntimeError(f"Unsupported database dialect for event ingest: {dialect}")

    stmt = (
        insert(Events)
        .values(rows)
        .on_conflict_do_nothing(index_elements=["event_id"])
        .returning(Events.event_id)
    )

    try:
        inserted = set(db.scalars(stmt).all())
        db.commit()
    except Exception:
        db.rollback()
        raise

    return inserted


def get_car_events(db: Session, session_id: int, car_idx: int) -> list[Events]:
    stmt = (
        select(Events)
        .where(Events.session_id == session_id, Events.car_idx == car_idx)
        .order_by(Events.session_time, Events.session_tick, Events.id)
    )
    return list(db.scalars(stmt).all())
