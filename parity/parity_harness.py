#!/usr/bin/env python3
"""
Contract / parity harness for the OrderManager .NET -> Java migration.

This is the ACCEPTANCE GATE for every migrated route group. It:
  1. Runs a fixed, ordered suite of HTTP requests (reads first, then a
     deterministic sequence of writes) against a backend.
  2. Captures each response (status + JSON body) as a fixture.
  3. Compares the Java backend's responses against the .NET responses,
     normalizing away insignificant differences (volatile timestamps and
     .NET `ReferenceHandler.IgnoreCycles` serialization artifacts), and reports
     any remaining differences in the CONTRACT-relevant fields.

Both backends are expected to run against their OWN freshly-seeded database
(the two backends use different physical schemas -- PascalCase columns for
EF Core vs snake_case for Hibernate -- so they do NOT share one file). Because
both seed identical logical data in the same insert order, primary keys line
up deterministically; only timestamps differ, and those are normalized.

Usage:
  # capture a backend's responses to a fixtures dir
  python3 parity_harness.py capture --base-url http://localhost:5001 --out fixtures/dotnet

  # compare two live backends (captures both, then diffs)
  python3 parity_harness.py compare --dotnet-url http://localhost:5001 \
                                    --java-url   http://localhost:5000

  # compare a live Java backend against previously captured .NET golden fixtures
  python3 parity_harness.py compare --dotnet-dir fixtures/dotnet \
                                    --java-url http://localhost:5000

Exit code is 0 only when every CONTRACT request matches (status + normalized
JSON). Requests explicitly marked as expected-divergences (error-handling
differences the migration intentionally improves) are reported but never fail
the run.
"""
import argparse
import copy
import json
import os
import sys
import urllib.error
import urllib.request

# ---------------------------------------------------------------------------
# Request suite. Ordered: read-only requests first, then a deterministic
# sequence of writes. The SAME sequence runs against both backends starting
# from identical freshly-seeded databases, so ids and derived values align.
# ---------------------------------------------------------------------------
#   contract=True  -> must match exactly (status + normalized JSON)
#   contract=False -> expected divergence (error handling); reported only.
SUITE = [
    # ---- Customers (read) ----
    {"name": "customers_list",            "method": "GET",  "path": "/api/customers"},
    {"name": "customers_get_1",           "method": "GET",  "path": "/api/customers/1"},
    {"name": "customers_get_missing",     "method": "GET",  "path": "/api/customers/999"},

    # ---- Products (read) ----
    {"name": "products_list",             "method": "GET",  "path": "/api/products"},
    {"name": "products_get_1",            "method": "GET",  "path": "/api/products/1"},
    {"name": "products_by_category",      "method": "GET",  "path": "/api/products/category/Widgets"},
    {"name": "products_get_missing",      "method": "GET",  "path": "/api/products/999"},

    # ---- Inventory (read) ----
    {"name": "inventory_list",            "method": "GET",  "path": "/api/inventory"},
    {"name": "inventory_by_product_1",    "method": "GET",  "path": "/api/inventory/product/1"},
    {"name": "inventory_low_stock",       "method": "GET",  "path": "/api/inventory/low-stock"},

    # ---- Orders (read, empty state) ----
    {"name": "orders_list_empty",         "method": "GET",  "path": "/api/orders"},
    {"name": "orders_get_missing",        "method": "GET",  "path": "/api/orders/999"},

    # ---- Writes (deterministic order) ----
    {"name": "orders_create",             "method": "POST", "path": "/api/orders",
     "body": {"customerId": 1, "items": [{"productId": 1, "quantity": 2},
                                         {"productId": 3, "quantity": 1}]}},
    {"name": "orders_list_after_create",  "method": "GET",  "path": "/api/orders"},
    {"name": "orders_get_1",              "method": "GET",  "path": "/api/orders/1"},
    {"name": "orders_update_status",      "method": "PATCH", "path": "/api/orders/1/status",
     "body": {"status": "Shipped"}},
    {"name": "inventory_restock_1",       "method": "POST", "path": "/api/inventory/product/1/restock",
     "body": {"quantity": 25}},
    {"name": "inventory_after_restock_1", "method": "GET",  "path": "/api/inventory/product/1"},
    {"name": "customers_create",          "method": "POST", "path": "/api/customers",
     "body": {"name": "Umbrella Co", "email": "buyer@umbrella.com", "phone": "555-0400",
              "address": "1 Raccoon Rd", "city": "Raccoon City", "state": "IL", "zipCode": "60000"}},
    {"name": "products_create",           "method": "POST", "path": "/api/products",
     "body": {"name": "Gizmo Z", "description": "Deluxe gizmo", "category": "Gizmos",
              "price": 99.99, "sku": "GZM-001"}},

    # ---- Expected divergences: error handling the migration intentionally improves ----
    # .NET has no exception middleware -> unhandled exceptions surface as HTTP 500.
    # The Java backend maps them to 400/404/409 via @ControllerAdvice.
    {"name": "orders_insufficient_stock", "method": "POST", "path": "/api/orders",
     "body": {"customerId": 1, "items": [{"productId": 1, "quantity": 99999}]},
     "contract": False},
    {"name": "orders_unknown_customer",   "method": "POST", "path": "/api/orders",
     "body": {"customerId": 9999, "items": [{"productId": 1, "quantity": 1}]},
     "contract": False},
    {"name": "restock_unknown_product",   "method": "POST", "path": "/api/inventory/product/9999/restock",
     "body": {"quantity": 5}, "contract": False},
]

# Timestamp-valued keys differ every run; normalize their VALUE to a sentinel
# (presence is still compared, only the value is ignored).
VOLATILE_KEYS = {"createdAt", "orderDate", "lastRestocked"}
_TS = "<timestamp>"


def request(base_url, method, path, body=None, timeout=30):
    url = base_url.rstrip("/") + path
    data = None
    headers = {"Accept": "application/json"}
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            raw = resp.read().decode("utf-8")
            status = resp.getcode()
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8", "replace")
        status = e.code
    try:
        parsed = json.loads(raw) if raw.strip() else None
    except json.JSONDecodeError:
        parsed = {"__raw__": raw}
    return {"status": status, "json": parsed}


def run_suite(base_url):
    out = {}
    for step in SUITE:
        out[step["name"]] = request(base_url, step["method"], step["path"], step.get("body"))
    return out


def normalize(value):
    """
    Canonical form that erases differences that are irrelevant to the API
    contract the Angular SPA consumes:
      * timestamp values -> sentinel (key presence preserved)
      * null values dropped
      * empty arrays/objects dropped
      * null array elements dropped (collapses .NET IgnoreCycles `[null]`)
    What remains is the meaningful field graph, which must match.
    """
    if isinstance(value, dict):
        result = {}
        for k, v in value.items():
            if k in VOLATILE_KEYS:
                result[k] = _TS if v is not None else None
                continue
            nv = normalize(v)
            if nv is None:
                continue
            if isinstance(nv, (dict, list)) and len(nv) == 0:
                continue
            result[k] = nv
        return result
    if isinstance(value, list):
        items = [normalize(v) for v in value]
        return [v for v in items if v is not None]
    return value


def diff(path, a, b, out):
    if isinstance(a, dict) and isinstance(b, dict):
        for k in sorted(set(a) | set(b)):
            if k not in a:
                out.append(f"{path}.{k}: only in JAVA ({b[k]!r})")
            elif k not in b:
                out.append(f"{path}.{k}: only in .NET ({a[k]!r})")
            else:
                diff(f"{path}.{k}", a[k], b[k], out)
    elif isinstance(a, list) and isinstance(b, list):
        if len(a) != len(b):
            out.append(f"{path}: list length .NET={len(a)} JAVA={len(b)}")
        for i in range(min(len(a), len(b))):
            diff(f"{path}[{i}]", a[i], b[i], out)
    elif a != b:
        out.append(f"{path}: .NET={a!r} JAVA={b!r}")


def compare(dotnet, java):
    results = []
    ok = True
    by_name = {s["name"]: s for s in SUITE}
    for name in [s["name"] for s in SUITE]:
        d, j = dotnet.get(name), java.get(name)
        is_contract = by_name[name].get("contract", True)
        entry = {"name": name, "contract": is_contract,
                 "dotnet_status": d["status"] if d else None,
                 "java_status": j["status"] if j else None,
                 "diffs": []}
        if d is None or j is None:
            entry["diffs"].append("missing capture on one side")
        else:
            if d["status"] != j["status"]:
                entry["diffs"].append(f"status: .NET={d['status']} JAVA={j['status']}")
            # Only success (2xx) responses carry a contract body the SPA reads.
            # For error responses the status is the contract; bodies differ by
            # framework (.NET ProblemDetails vs Spring error JSON) and are ignored.
            if 200 <= d["status"] < 300:
                diff("$", normalize(d["json"]), normalize(j["json"]), entry["diffs"])
        entry["match"] = len(entry["diffs"]) == 0
        if is_contract and not entry["match"]:
            ok = False
        results.append(entry)
    return ok, results


def print_report(results):
    print("=" * 78)
    print("PARITY REPORT  (.NET vs Java)")
    print("=" * 78)
    for r in results:
        tag = "CONTRACT" if r["contract"] else "diverge "
        status = "PASS" if r["match"] else ("FAIL" if r["contract"] else "DIFF")
        print(f"[{tag}] {status:4}  {r['name']:26} "
              f".NET={r['dotnet_status']} JAVA={r['java_status']}")
        for d in r["diffs"]:
            print(f"           - {d}")
    print("-" * 78)
    contract = [r for r in results if r["contract"]]
    passed = [r for r in contract if r["match"]]
    print(f"contract endpoints: {len(passed)}/{len(contract)} passed")
    diverge = [r for r in results if not r["contract"]]
    print(f"expected divergences (reported, non-fatal): {len(diverge)}")


def save_fixtures(captures, out_dir):
    os.makedirs(out_dir, exist_ok=True)
    with open(os.path.join(out_dir, "responses.json"), "w") as f:
        json.dump(captures, f, indent=2, sort_keys=True)


def load_fixtures(in_dir):
    with open(os.path.join(in_dir, "responses.json")) as f:
        return json.load(f)


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    cap = sub.add_parser("capture", help="capture one backend's responses to a fixtures dir")
    cap.add_argument("--base-url", required=True)
    cap.add_argument("--out", required=True)

    cmp = sub.add_parser("compare", help="diff Java against .NET (live urls or a .NET fixtures dir)")
    cmp.add_argument("--dotnet-url")
    cmp.add_argument("--dotnet-dir")
    cmp.add_argument("--java-url", required=True)
    cmp.add_argument("--out")
    cmp.add_argument("--report")

    args = ap.parse_args()

    if args.cmd == "capture":
        caps = run_suite(args.base_url)
        save_fixtures(caps, args.out)
        print(f"captured {len(caps)} responses -> {args.out}/responses.json")
        return 0

    # compare
    if bool(args.dotnet_url) == bool(args.dotnet_dir):
        ap.error("compare requires exactly one of --dotnet-url or --dotnet-dir")
    dotnet = run_suite(args.dotnet_url) if args.dotnet_url else load_fixtures(args.dotnet_dir)
    java = run_suite(args.java_url)
    if args.out:
        save_fixtures(dotnet, os.path.join(args.out, "dotnet"))
        save_fixtures(java, os.path.join(args.out, "java"))
    ok, results = compare(dotnet, java)
    print_report(results)
    if args.report:
        with open(args.report, "w") as f:
            json.dump(results, f, indent=2)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())
