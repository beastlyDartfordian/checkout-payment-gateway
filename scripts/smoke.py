#!/usr/bin/env python3
"""Exercise a running gateway against the supplied bank simulator. Synthetic data only."""
import datetime
import json
import sys
import urllib.error
import urllib.request

base = (sys.argv[1] if len(sys.argv) > 1 else "http://localhost:5067").rstrip("/")


def call(path, payload=None):
    data = json.dumps(payload).encode() if payload is not None else None
    request = urllib.request.Request(base + path, data=data, headers={"Content-Type": "application/json"})
    try:
        response = urllib.request.urlopen(request, timeout=10)
    except urllib.error.HTTPError as error:
        response = error
    with response:
        body = response.read().decode()
        return response.status, json.loads(body) if body.startswith("{") else body


assert call("/health") == (200, "Healthy")
year = datetime.datetime.now(datetime.timezone.utc).year + 2
request = {"cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": year,
           "currency": "GBP", "amount": 1050, "cvv": "987"}
for suffix, expected in [("7", "Authorized"), ("8", "Declined")]:
    request["cardNumber"] = "222240534324887" + suffix
    status, payment = call("/api/Payments", request)
    assert status == 201, (status, payment)
    assert payment["status"] == expected, payment
    assert payment["cardNumberLastFour"] == "887" + suffix
    assert set(payment) == {"id", "status", "cardNumberLastFour", "expiryMonth", "expiryYear", "currency", "amount"}
    assert call("/api/Payments/" + payment["id"]) == (200, payment)
    print(expected + ": created and retrieved")
request["cardNumber"] = "2222405343248870"
status, problem = call("/api/Payments", request)
assert status == 503 and "id" not in problem, (status, problem)
print("Bank unavailable: 503, no invented payment outcome")
request["currency"] = "JPY"
status, problem = call("/api/Payments", request)
assert status == 400 and problem["errors"], (status, problem)
assert call("/api/Payments/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")[0] == 404
print("Validation rejection: 400; missing payment: 404; health: 200")
print("Smoke checks passed")
