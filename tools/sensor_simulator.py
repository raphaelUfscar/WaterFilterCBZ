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

# Value-level alarm scenarios aligned to the app's SensorParameterRegistry. Unlike the
# generic --profile signals, these drive the application's two-tier validation and stale
# supervision deterministically so end-to-end tests can assert each alarm state:
#   normal       - every sensor stays inside its operating specification (no alarm)
#   out-of-spec  - plausible value above the operating band (OutOfSpec / tier 2)
#   invalid      - physically implausible value above the physical range (rejected / tier 1)
#   stale        - emit normal frames briefly, then go silent so the app's stale timer fires
SCENARIOS = ("normal", "out-of-spec", "invalid", "stale")

# Seconds of normal frames emitted before the "stale" scenario goes silent. Must be short
# relative to the app's 5 s stale threshold so a test sees fresh data then a stale transition.
STALE_WARMUP_SECONDS = 2.0


@dataclass(frozen=True)
class SensorParameter:
    """Process-parameter ranges mirroring the app's SensorParameterRegistry (OAI-003)."""

    name: str
    operating_min: float
    operating_max: float
    physical_min: float
    physical_max: float
    nominal: float


# Keyed by sensor_id 1..4, matching the app's 0x01=conductivity .. 0x04=pressure convention.
SENSOR_PARAMETERS: dict[int, SensorParameter] = {
    1: SensorParameter("Conductivity", 0.0, 1.3, 0.0, 200.0, 0.6),
    2: SensorParameter("Temperature", 15.0, 30.0, -10.0, 130.0, 22.0),
    3: SensorParameter("pH", 5.0, 7.0, 0.0, 14.0, 6.0),
    4: SensorParameter("Pressure", 1.0, 6.0, 0.0, 16.0, 3.0),
}


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


def normal_value(param: SensorParameter, sensor_number: int, elapsed_seconds: float) -> float:
    """A value that oscillates gently around the nominal, always inside the operating band."""
    amplitude = (param.operating_max - param.operating_min) * 0.2
    value = param.nominal + amplitude * math.sin(elapsed_seconds * 1.5 + sensor_number)
    return max(param.operating_min, min(param.operating_max, value))


def value_for_scenario(
    scenario: str,
    sensor_number: int,
    elapsed_seconds: float,
    target_sensor: int | None = None,
) -> float:
    """
    Parameter-aware value that drives a specific alarm state. When ``target_sensor`` is set,
    only that sensor receives the anomaly; the others stay normal. Unknown sensor ids fall back
    to a constant in-range value.
    """
    param = SENSOR_PARAMETERS.get(sensor_number)
    if param is None:
        return 1.0

    applies = target_sensor is None or target_sensor == sensor_number
    # "stale" keeps emitting normal values; the silence is handled by the run loop.
    if not applies or scenario in ("normal", "stale"):
        return normal_value(param, sensor_number, elapsed_seconds)

    if scenario == "out-of-spec":
        # Plausible (<= physical_max) but clearly above the operating band -> OutOfSpec.
        margin = max((param.physical_max - param.operating_max) * 0.3, 0.5)
        return min(param.operating_max + margin, param.physical_max)

    if scenario == "invalid":
        # Above the physical range -> rejected as implausible (tier 1).
        return param.physical_max + max((param.physical_max - param.physical_min) * 0.5, 1.0)

    raise ValueError(f"unsupported scenario: {scenario}")


def should_emit_frame(scenario: str | None, elapsed_seconds: float) -> bool:
    """
    Whether a frame should be written this tick. The "stale" scenario stops emitting once it
    passes its warm-up so the app's stale-data timer fires while the port stays connected;
    every other scenario always emits.
    """
    return not (scenario == "stale" and elapsed_seconds >= STALE_WARMUP_SECONDS)


def signal_description(scenario: str | None, profile: str, target_sensor: int | None) -> str:
    """Human-readable summary of the active signal source for the startup banner."""
    if scenario is None:
        return f"profile={profile}"
    if target_sensor is not None:
        return f"scenario={scenario} target-sensor={target_sensor}"
    return f"scenario={scenario}"


def build_sample_frame(
    sensor_count: int,
    timestamp_ms: int,
    profile: str,
    elapsed_seconds: float,
    unit_id: int = DEFAULT_UNIT_ID,
    scenario: str | None = None,
    target_sensor: int | None = None,
) -> bytes:
    """Build one frame. When ``scenario`` is set it drives alarm states and overrides ``profile``."""
    entries = [
        SensorEntry(
            sensor_id=sensor_number,
            timestamp_ms=timestamp_ms,
            unit_id=unit_id,
            value=(
                value_for_scenario(scenario, sensor_number, elapsed_seconds, target_sensor)
                if scenario is not None
                else value_for_profile(profile, sensor_number, elapsed_seconds)
            ),
        )
        for sensor_number in range(1, sensor_count + 1)
    ]
    return build_frame(entries)


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Emit WaterFilterCBZ binary sensor frames to a serial port."
    )
    parser.add_argument("--port", help="Serial port to write, for example COM10.")
    parser.add_argument(
        "--list-ports",
        action="store_true",
        help="List serial ports visible to pyserial and exit.",
    )
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
        "--scenario",
        choices=SCENARIOS,
        default=None,
        help="Parameter-aware alarm scenario aligned to the app's sensor ranges. "
        "Overrides --profile when set.",
    )
    parser.add_argument(
        "--target-sensor",
        type=int,
        choices=range(MIN_SENSORS, MAX_SENSORS + 1),
        metavar="1-4",
        default=None,
        help="Restrict the scenario anomaly to a single sensor (default: all sensors).",
    )
    parser.add_argument(
        "--inject-errors",
        choices=ERROR_MODES,
        default="none",
        help="Malformed frame mode injected periodically.",
    )

    args = parser.parse_args(argv)
    if not args.list_ports and not args.port:
        parser.error("--port is required unless --list-ports is used")
    if args.rate_hz < 1.0 or args.rate_hz > 100.0:
        parser.error("--rate-hz must be between 1 and 100")
    if args.duration_seconds is not None and args.duration_seconds <= 0:
        parser.error("--duration-seconds must be greater than 0")
    if args.target_sensor is not None and args.target_sensor > args.sensors:
        parser.error("--target-sensor must not exceed --sensors")
    return args


def run(args: argparse.Namespace) -> int:
    try:
        import serial
        from serial.tools import list_ports
    except ImportError:
        print(
            "pyserial is required to open serial ports. "
            "Install it with: python -m pip install -r tools/requirements-simulator.txt",
            file=sys.stderr,
        )
        return 2

    if args.list_ports:
        ports = list_available_ports(list_ports)
        if ports:
            print("Serial ports visible to pyserial:")
            for port in ports:
                print(f"  {port.device}: {port.description}")
        else:
            print("No serial ports were found.")
        return 0

    interval_seconds = 1.0 / args.rate_hz
    timestamp_step_ms = max(1, round(interval_seconds * 1000))
    timestamp_ms = 0
    frame_number = 0
    start = time.monotonic()
    next_write = start

    signal_desc = signal_description(args.scenario, args.profile, args.target_sensor)
    print(
        f"Writing {args.sensors} sensor(s) to {args.port} at {args.baud} baud, "
        f"{args.rate_hz:g} Hz, {signal_desc}, inject-errors={args.inject_errors}"
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
                    scenario=args.scenario,
                    target_sensor=args.target_sensor,
                )
                payload = frame
                injected = (
                    args.inject_errors != "none"
                    and frame_number > 0
                    and frame_number % ERROR_INTERVAL_FRAMES == 0
                )
                if injected:
                    payload = inject_error(frame, args.inject_errors)

                # The "stale" scenario emits a short burst of fresh frames, then goes silent
                # (the port stays open) so the app's stale-data timer fires while connected.
                if should_emit_frame(args.scenario, elapsed):
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
        print_port_failure_hints(args.port, list_available_ports(list_ports), file=sys.stderr)
        return 1

    print("Finished.")
    return 0


def list_available_ports(list_ports_module) -> list:
    return sorted(list_ports_module.comports(), key=lambda port: natural_port_key(port.device))


def natural_port_key(port_name: str) -> tuple[str, int | str]:
    prefix = port_name.rstrip("0123456789")
    suffix = port_name[len(prefix) :]
    return (prefix.upper(), int(suffix) if suffix else port_name.upper())


def print_port_failure_hints(port_name: str, ports: list, file) -> None:
    visible_port_names = {port.device.upper() for port in ports}
    print("", file=file)
    if ports:
        print("Ports visible to pyserial:", file=file)
        for port in ports:
            print(f"  {port.device}: {port.description}", file=file)
    else:
        print("pyserial did not find any serial ports.", file=file)

    print("", file=file)
    if port_name.upper() not in visible_port_names:
        print(
            f"{port_name} is not currently visible. Create or confirm the virtual COM pair, "
            "then use one of the listed port names.",
            file=file,
        )
    else:
        print(
            f"{port_name} is visible but could not be opened. Close any other program using "
            "that side of the pair, including another simulator instance, serial monitor, or terminal.",
            file=file,
        )
    print(
        "For a virtual pair such as COM10 <-> COM11, run the simulator on one side "
        "and connect WaterFilterCBZ to the other side.",
        file=file,
    )
    print("You can also run: python tools/sensor_simulator.py --list-ports", file=file)


def validate_uint8(value: int, name: str) -> None:
    if not 0 <= value <= 0xFF:
        raise ValueError(f"{name} must fit in uint8")


def main(argv: list[str] | None = None) -> int:
    return run(parse_args(sys.argv[1:] if argv is None else argv))


if __name__ == "__main__":
    raise SystemExit(main())
