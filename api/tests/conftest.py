import os
import tempfile
from pathlib import Path

import pytest

# app.database.db reads DATABASE_URL and creates tables at import time, so this
# must be set before anything under `app` is imported. Defaults to a throwaway
# SQLite file; set TEST_DATABASE_URL to run against a scratch Postgres database.
# Never point this at production.
_tmp_db = Path(tempfile.mkdtemp(prefix="stint-tests-")) / "test.db"
os.environ["DATABASE_URL"] = os.environ.get(
    "TEST_DATABASE_URL", f"sqlite:///{_tmp_db.as_posix()}"
)


@pytest.fixture(scope="session")
def app():
    from app.main import app as fastapi_app

    return fastapi_app


@pytest.fixture(scope="session")
def api_key(app):  # pylint: disable=redefined-outer-name,unused-argument
    from app.database.db import SessionLocal
    from app.dependencies import create_api_key

    db = SessionLocal()
    try:
        return create_api_key(db, "pytest")
    finally:
        db.close()


@pytest.fixture
def client(app, api_key):  # pylint: disable=redefined-outer-name
    from fastapi.testclient import TestClient

    with TestClient(app) as test_client:
        test_client.headers["X-API-Key"] = api_key
        yield test_client
