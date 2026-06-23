#!/usr/bin/env python3
"""Build initial malware-hash SQLite database from MalwareBazaar public API."""
import sys
import json
import sqlite3
import urllib.request
import urllib.parse

EICAR_SHA256 = "275a021bbfb6489e54d471899f7db9d1663fc695ec2fe2a2c4538aabf651fd0f"
API_URL      = "https://mb-api.abuse.ch/api/v1/"
SELECTORS    = ["time", "exe", "dll", "doc", "ps1", "jar", "zip"]


def build(output: str) -> None:
    conn = sqlite3.connect(output)
    conn.execute("PRAGMA journal_mode=WAL")
    conn.execute("""
        CREATE TABLE IF NOT EXISTS hashes (
            sha256      TEXT PRIMARY KEY,
            threat_name TEXT NOT NULL
        )
    """)
    conn.execute("CREATE INDEX IF NOT EXISTS idx_sha256 ON hashes(sha256)")
    conn.execute("INSERT OR IGNORE INTO hashes VALUES (?, ?)", (EICAR_SHA256, "EICAR-Test-File"))
    conn.commit()

    inserted = 1
    for selector in SELECTORS:
        try:
            data = urllib.parse.urlencode({"query": "get_recent", "selector": selector}).encode()
            req  = urllib.request.Request(
                API_URL, data=data,
                headers={"Content-Type": "application/x-www-form-urlencoded",
                         "User-Agent":   "NinjaSecurity-HashDB-Builder/1.0"})
            with urllib.request.urlopen(req, timeout=30) as resp:
                result = json.loads(resp.read())

            if result.get("query_status") != "ok":
                print(f"  [{selector}] status={result.get('query_status')}", file=sys.stderr)
                continue

            for sample in result.get("data", []):
                sha256 = (sample.get("sha256_hash") or "").lower()
                if len(sha256) != 64:
                    continue
                sig  = sample.get("signature") or ""
                tags = sample.get("tags") or []
                name = sig or (f"Malware.{tags[0]}" if tags else "Malware.Unknown")
                conn.execute("INSERT OR IGNORE INTO hashes VALUES (?, ?)", (sha256, name))
                inserted += 1

            conn.commit()
            print(f"  [{selector}] ok — total so far: {inserted}")
        except Exception as exc:
            print(f"  [{selector}] warning: {exc}", file=sys.stderr)

    conn.close()
    print(f"Hash database built: {inserted} entries -> {output}")


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "publish/hashes.db"
    build(out)
