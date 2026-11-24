from tests.builders import GREG, KAM, Race


def test_ingest_is_idempotent(client):
    r = Race()
    events = [r.race_started(100, KAM), r.entered(100, KAM)]

    first = client.post("/v1/events", json={"events": events})
    assert first.status_code == 200
    assert sorted(first.json()["accepted"]) == sorted(e["event_id"] for e in events)

    resent = client.post("/v1/events", json={"events": events})
    assert resent.status_code == 200
    assert resent.json()["accepted"] == []
    assert sorted(resent.json()["duplicates"]) == sorted(e["event_id"] for e in events)


def test_duplicate_within_one_batch(client):
    r = Race()
    ev = r.race_started(100, KAM)

    body = client.post("/v1/events", json={"events": [ev, ev]}).json()

    assert body["accepted"] == [ev["event_id"]]
    assert body["duplicates"] == [ev["event_id"]]


def test_invalid_event_is_rejected_without_failing_the_batch(client):
    r = Race()
    good = r.race_started(100, KAM)
    bad = r.box_in(200, KAM)
    del bad["payload"]["tires_requested"]
    unknown = r.event("teleported", 300, KAM, {})

    body = client.post("/v1/events", json={"events": [good, bad, unknown]}).json()

    assert body["accepted"] == [good["event_id"]]
    assert [rej["id"] for rej in body["rejected"]] == [bad["event_id"], unknown["event_id"]]
    assert "tires_requested" in body["rejected"][0]["reason"]


def test_batch_size_is_limited(client):
    r = Race()
    events = [r.status(100 + i, KAM) for i in range(101)]

    assert client.post("/v1/events", json={"events": events}).status_code == 422


def test_api_key_required(client):
    r = Race()
    res = client.post(
        "/v1/events",
        json={"events": [r.race_started(100, KAM)]},
        headers={"X-API-Key": ""},
    )
    assert res.status_code == 401


def test_timeline_merges_events_from_two_clients(client):
    r = Race()
    kam_client = [
        r.session_info(0, KAM),
        r.race_started(100, KAM),
        r.entered(100, KAM),
        r.road_in(3000, KAM),
        r.box_in(3020, KAM, fuel=3.0),
        r.exited(3025, KAM, box=True),
    ]
    greg_client = [
        r.session_info(3090, GREG),  # duplicate session info is harmless
        r.entered(3100, GREG, box=True),
        r.box_out(3160, GREG, fuel=100.0),
        r.road_out(3170, GREG),
    ]
    # Gregory's client delivers first; arrival order must not matter.
    assert client.post("/v1/events", json={"events": greg_client}).status_code == 200
    assert client.post("/v1/events", json={"events": kam_client}).status_code == 200

    res = client.get(f"/v1/sessions/{r.session_id}/cars/{r.car_idx}/timeline")
    assert res.status_code == 200
    t = res.json()

    assert t["session"]["track"] == "Spa"
    assert len(t["pit_stops"]) == 1
    stop = t["pit_stops"][0]
    assert stop["status"] == "complete"
    assert (stop["fuel_start"], stop["fuel_end"], stop["refuel_amount"]) == (3.0, 100.0, 97.0)
    assert stop["swap"]["from_driver"]["name"] == "Kam Wilson"
    assert stop["swap"]["to_driver"]["name"] == "Gregory Toumaian"
    assert [s["driver"]["name"] for s in t["stints"]] == ["Kam Wilson", "Gregory Toumaian"]


def test_events_endpoint_returns_session_time_order(client):
    r = Race()
    events = [r.entered(200, KAM), r.race_started(100, KAM)]
    client.post("/v1/events", json={"events": events})

    res = client.get(f"/v1/sessions/{r.session_id}/cars/{r.car_idx}/events")

    assert [e["type"] for e in res.json()] == ["race_started", "driver_entered"]


def test_timeline_404_for_unknown_car(client):
    assert client.get("/v1/sessions/1/cars/99/timeline").status_code == 404
