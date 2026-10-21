#!/usr/bin/env python3
"""Print the live Notch Pet payload for debugging."""
import json
import socket
import sys
from pathlib import Path

CURRENT = Path.home() / "Library/Application Support/DSHNotchPet/current.json"


def main() -> int:
    if not CURRENT.exists():
        print(f"no live socket: {CURRENT}", file=sys.stderr)
        return 1
    meta = json.loads(CURRENT.read_text())
    path = meta.get("socket")
    if not path:
        print("current.json has no socket", file=sys.stderr)
        return 1
    with socket.socket(socket.AF_UNIX, socket.SOCK_STREAM) as client:
        client.settimeout(3)
        client.connect(path)
        data = b""
        while not data.endswith(b"\n"):
            chunk = client.recv(65536)
            if not chunk:
                break
            data += chunk
    print(json.dumps(json.loads(data), indent=2, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
