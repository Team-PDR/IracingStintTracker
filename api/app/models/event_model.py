import uuid
from datetime import datetime
from typing import Any

from sqlalchemy import JSON, TIMESTAMP, Index, String, Uuid
from sqlalchemy.dialects.postgresql import JSONB
from sqlalchemy.orm import Mapped, mapped_column
from sqlalchemy.sql import func

from app.models import Base


class Events(Base):
    """Append-only log of facts reported by ingester clients.

    Rows are never updated. Stints, pit stops and laps are derived from these
    rows (see app/services/derivation.py), so they can always be rebuilt.
    See docs/event-schema.md for the contract.
    """

    __tablename__ = "events"

    id: Mapped[int] = mapped_column(primary_key=True)
    event_id: Mapped[uuid.UUID] = mapped_column(Uuid, unique=True)
    schema_version: Mapped[int]
    type: Mapped[str] = mapped_column(String(32))

    session_id: Mapped[int]
    car_idx: Mapped[int]
    session_num: Mapped[int]
    session_time: Mapped[float]
    session_tick: Mapped[int]

    driver_user_id: Mapped[int]
    driver_name: Mapped[str] = mapped_column(String)

    client_install_id: Mapped[uuid.UUID] = mapped_column(Uuid)
    app_version: Mapped[str] = mapped_column(String(32))
    sent_at_utc: Mapped[datetime] = mapped_column(TIMESTAMP(timezone=True))

    # Stored exactly as received (after validation), so new derivation logic
    # can use fields that older code ignored.
    payload: Mapped[dict[str, Any]] = mapped_column(
        JSON().with_variant(JSONB(), "postgresql")
    )

    # pylint: disable=not-callable
    received_at: Mapped[datetime] = mapped_column(
        TIMESTAMP(timezone=True), server_default=func.now()
    )

    __table_args__ = (
        Index("ix_events_car_timeline", "session_id", "car_idx", "session_time"),
    )
