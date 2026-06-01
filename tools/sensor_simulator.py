#!/usr/bin/env python3
"""Emit WaterFilterCBZ sensor frames to a serial port."""

from __future__ import annotations

import argparse
import math
import random
import struct
import sys
import time
from dataclasses import dataclass
from typing import Iterable


START_BYTE = 0xAA
END_BYTE = 0x55
MIN_SENSORS = 1
MAX_SENSORS = 4
DEFAULT_BAUD = 115200
DEFAULT_SENSOR_COUNT = 4
DEFAULT_RATE_HZ = 10.0
DEFAULT_UNIT_ID = 0x01
ERROR_INTERVAL_FRAMES = 25
PARTIAL_FRAME_PAUSE_SECONDS = 0.45

PROFILES = ("sine", "ramp", "step", "noise", "mixed")
ERROR_MODES = ("none", "checksum", "end-byte", "count", "partial", "noise")


@dataclass(frozen=True)
class SensorEntry:
    sensor_id: int
    timestamp_ms: int
    unit_id: int
    value: float


def build_sensor_entry(entry: SensorEntry) -> bytes:
    """Encode one 10-byte sensor entry."""
    validate_uint8(entry.sensor_id, "sensor_id")
    validate_uint8(entry.unit_id, "unit_id")
    if not 0 <= entry.timestamp_ms <= 0xFFFFFFFF:
        raise ValueError("timestamp_ms must fit in uint32")

    return struct.pack(
        "<BIBf",
        entry.sensor_id,
        entry.timestamp_ms,
        entry.unit_id,
        float(entry.value),
    )


def calculate_checksum(frame_without_checksum_or_end: bytes) -> int:
    return sum(frame_without_checksum_or_end) & 0xFF


def build_frame(entries: Iterable[SensorEntry]) -> bytes:
    encoded_entries = [build_sensor_entry(entry) for entry in entries]
    count = len(encoded_entries)
    if count < MIN_SENSORS or count > MAX_SENSORS:
        raise ValueError("frame must contain 1 to 4 sensor entries")

    body = bytes([START_BYTE, count]) + b"".join(encoded_entries)
    checksum = calculate_checksum(body)
    return body + bytes([checksum, END_BYTE])


def inject_error(frame: bytes, mode: str) -> bytes:
    if mode == "none":
        return frame
    if mode == "checksum":
        corrupted = bytearray(frame)
        corrupted[-2] ^= 0xFF
        return bytes(corrupted)
    if mode == "end-byte":
        corrupted = bytearray(frame)
        corrupted[-1] = 0x00
        return bytes(corrupted)
    if mode == "count":
        corrupted = bytearray(frame)
        corrupted[1] = MAX_SENSORS + 1
        return bytes(corrupted)
    if mode == "partial":
        return frame[: max(1, len(frame) // 2)]
    if mode == "noise":
        return bytes([0x00, 0x13, START_BYTE, 0x00, 0x7E]) + frame

    raise ValueError(f"unsupported error injection mode: {mode}")


def value_for_profile(profile: str, sensor_number: int, elapsed_seconds: float) -> float:
    selected = profile
    if profile == "mixed":
        selected = PROFILES[(sensor_number - 1) % 4]

    if selected == "sine":
        return 50.0 + 20.0 * math.sin(elapsed_seconds * 2.0 + sensor_number)
    if selected == "ramp":
        return ((elapsed_seconds * 12.5) + sensor_number * 7.0) % 100.0
    if selected == "step":
        return 25.0 if int(elapsed_seconds / 2.0 + sensor_number) % 2 == 0 else 75.0
    if selected == "noise":
        return 50.0 + random.uniform(-8.0, 8.0)

    raise ValueError(f"unsupported profile: {profile}")


def build_sample_frame(
    sensor_count: int,
    timestamp_ms: int,
    profile: str,
    elapsed_seconds: float,
    unit_id: int = DEFAULT_UNIT_ID,
) -> bytes:
    entries = [
        SensorEntry(
            sensor_id=sensor_number,
            timestamp_ms=timestamp_ms,
            unit_id=unit_id,
            value=value_for_profile(profile, sensor_number, elapsed_seconds),
        )
        for sensor_number in range(1, sensor_count + 1)
    ]
    return build_frame(entries)


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Emit WaterFilterCBZ binary sensor frames to a serial port."
    )
    parser.add_argument("--port", required=True, help="Serial port to write, for example COM10.")
    parser.add_argument("--baud", type=int, default=DEFAULT_BAUD, help="Serial baud rate.")
    parser.add_argument(
        "--sensors",
        type=int,
        default=DEFAULT_SENSOR_COUNT,
        choices=range(MIN_SENSORS, MAX_SENSORS + 1),
        metavar="1-4",
        help="Number of sensors per frame.",
    )
    parser.add_argument(
        "--rate-hz",
        type=float,
        default=DEFAULT_RATE_HZ,
        help="Frame emission rate from 1 to 100 Hz.",
    )
    parser.add_argument(
        "--duration-seconds",
        type=float,
        default=None,
        help="Optional run duration. Omit to run until Ctrl+C.",
    )
    parser.add_argument(
        "--profile",
        choices=PROFILES,
        default="mixed",
        help="Signal profile for generated values.",
    )
    parser.add_argument(
        "--inject-errors",
        choices=ERROR_MODES,
        default="none",
        help="Malformed frame mode injected periodically.",
    )

    args = parser.parse_args(argv)
    if args.rate_hz < 1.0 or args.rate_hz > 100.0:
        parser.error("--rate-hz must be between 1 and 100")
    if args.duration_seconds is not None and args.duration_seconds <= 0:
        parser.error("--duration-seconds must be greater than 0")
    return args


def run(args: argparse.Namespace) -> int:
    try:
        import serial
    except ImportError:
        print(
            "pyserial is required to open serial ports. "
            "Install it with: python -m pip install -r tools/requirements-simulator.txt",
            file=sys.stderr,
        )
        return 2

    interval_seconds = 1.0 / args.rate_hz
    timestamp_step_ms = max(1, round(interval_seconds * 1000))
    timestamp_ms = 0
    frame_number = 0
    start = time.monotonic()
    next_write = start

    print(
        f"Writing {args.sensors} sensor(s) to {args.port} at {args.baud} baud, "
        f"{args.rate_hz:g} Hz, profile={args.profile}, inject-errors={args.inject_errors}"
    )

    try:
        with serial.Serial(
            args.port,
            args.baud,
            bytesize=serial.EIGHTBITS,
            parity=serial.PARITY_NONE,
            stopbits=serial.STOPBITS_ONE,
            timeout=1,
            write_timeout=1,
        ) as port:
            while True:
                elapsed = time.monotonic() - start
                if args.duration_seconds is not None and elapsed >= args.duration_seconds:
                    break

                frame = build_sample_frame(
                    sensor_count=args.sensors,
                    timestamp_ms=timestamp_ms,
                    profile=args.profile,
                    elapsed_seconds=elapsed,
                )
                payload = frame
                injected = (
                    args.inject_errors != "none"
                    and frame_number > 0
                    and frame_number % ERROR_INTERVAL_FRAMES == 0
                )
                if injected:
                    payload = inject_error(frame, args.inject_errors)

                port.write(payload)
                port.flush()

                if injected and args.inject_errors == "partial":
                    time.sleep(PARTIAL_FRAME_PAUSE_SECONDS)

                frame_number += 1
                timestamp_ms = (timestamp_ms + timestamp_step_ms) & 0xFFFFFFFF
                next_write += interval_seconds
                sleep_for = next_write - time.monotonic()
                if sleep_for > 0:
                    time.sleep(sleep_for)
                else:
                    next_write = time.monotonic()
    except KeyboardInterrupt:
        print("\nStopped.")
        return 0
    except Exception as exc:
        print(f"Serial simulator failed: {exc}", file=sys.stderr)
        return 1

    print("Finished.")
    return 0


def validate_uint8(value: int, name: str) -> None:
    if not 0 <= value <= 0xFF:
        raise ValueError(f"{name} must fit in uint8")


def main(argv: list[str] | None = None) -> int:
    return run(parse_args(sys.argv[1:] if argv is None else argv))


if __name__ == "__main__":
    raise SystemExit(main())
