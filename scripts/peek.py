#!/usr/bin/env python3
"""Print the live Notch Pet payload for debugging."""
import json
import os
import socket
import sys
from pathlib import Path


def current_file() -> Path:
    if sys.platform == "win32":
        base = Path(os.environ.get("LOCALAPPDATA", Path.home() / "AppData/Local"))
        return base / "DSHNotchPet" / "current.json"
    return Path.home() / "Library/Application Support/DSHNotchPet/current.json"


def print_payload(data: bytes) -> int:
    try:
        print(json.dumps(json.loads(data), indent=2, ensure_ascii=False))
        return 0
    except Exception as error:  # noqa: BLE001 - CLI debug helper
        print(f"invalid payload: {error}", file=sys.stderr)
        return 1


def main() -> int:
    current = current_file()
    if not current.exists():
        print(f"no live host: {current}", file=sys.stderr)
        return 1
    meta = json.loads(current.read_text())
    path = meta.get("socket")
    if not path:
        print("current.json has no socket", file=sys.stderr)
        return 1

    if sys.platform == "win32":
        try:
            with open(path, "rb", buffering=0) as client:
                data = client.readline()
            return print_payload(data)
        except OSError:
            state_file = meta.get("stateFile")
            if state_file and Path(state_file).exists():
                return print_payload(Path(state_file).read_bytes())
            print(f"no pipe client and no state mirror: {path}", file=sys.stderr)
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
    return print_payload(data)


if __name__ == "__main__":
    raise SystemExit(main())
