import contextlib
import io
import math
import struct
import unittest

import sensor_simulator as simulator


class SensorSimulatorFrameTests(unittest.TestCase):
    def test_builds_valid_one_sensor_frame(self):
        frame = simulator.build_frame(
            [
                simulator.SensorEntry(
                    sensor_id=0x01,
                    timestamp_ms=1000,
                    unit_id=0x01,
                    value=12.5,
                )
            ]
        )

        self.assertEqual(14, len(frame))
        self.assertEqual(simulator.START_BYTE, frame[0])
        self.assertEqual(1, frame[1])
        self.assertEqual(simulator.END_BYTE, frame[-1])
        self.assertEqual(simulator.calculate_checksum(frame[:-2]), frame[-2])

    def test_builds_valid_four_sensor_frame(self):
        frame = simulator.build_frame(
            [
                simulator.SensorEntry(i, i * 100, 0x01, float(i))
                for i in range(1, 5)
            ]
        )

        self.assertEqual(44, len(frame))
        self.assertEqual(4, frame[1])
        self.assertEqual(simulator.calculate_checksum(frame[:-2]), frame[-2])

    def test_entry_uses_little_endian_timestamp_and_float(self):
        frame = simulator.build_frame(
            [
                simulator.SensorEntry(
                    sensor_id=0x02,
                    timestamp_ms=0x01020304,
                    unit_id=0x03,
                    value=1.25,
                )
            ]
        )

        entry = frame[2:12]
        self.assertEqual(0x02, entry[0])
        self.assertEqual(bytes([0x04, 0x03, 0x02, 0x01]), entry[1:5])
        self.assertEqual(0x03, entry[5])
        self.assertEqual(struct.pack("<f", 1.25), entry[6:10])

    def test_checksum_is_eight_bit_sum(self):
        body = bytes([0xAA, 0x01, 0x01, 0xE8, 0x03, 0x00, 0x00, 0x01])

        self.assertEqual(sum(body) & 0xFF, simulator.calculate_checksum(body))

    def test_rejects_invalid_sensor_count(self):
        with self.assertRaises(ValueError):
            simulator.build_frame([])

        with self.assertRaises(ValueError):
            simulator.build_frame(
                [simulator.SensorEntry(i, 0, 0x01, 0.0) for i in range(1, 6)]
            )

    def test_error_injection_checksum_changes_checksum_byte(self):
        frame = simulator.build_frame([simulator.SensorEntry(1, 0, 1, 1.0)])
        corrupted = simulator.inject_error(frame, "checksum")

        self.assertNotEqual(frame[-2], corrupted[-2])
        self.assertEqual(frame[-1], corrupted[-1])

    def test_error_injection_end_byte_changes_end_byte(self):
        frame = simulator.build_frame([simulator.SensorEntry(1, 0, 1, 1.0)])
        corrupted = simulator.inject_error(frame, "end-byte")

        self.assertNotEqual(simulator.END_BYTE, corrupted[-1])

    def test_error_injection_count_sets_invalid_count(self):
        frame = simulator.build_frame([simulator.SensorEntry(1, 0, 1, 1.0)])
        corrupted = simulator.inject_error(frame, "count")

        self.assertEqual(5, corrupted[1])

    def test_error_injection_partial_returns_prefix(self):
        frame = simulator.build_frame([simulator.SensorEntry(1, 0, 1, 1.0)])
        corrupted = simulator.inject_error(frame, "partial")

        self.assertLess(len(corrupted), len(frame))
        self.assertEqual(frame[: len(corrupted)], corrupted)

    def test_error_injection_noise_prefixes_valid_frame(self):
        frame = simulator.build_frame([simulator.SensorEntry(1, 0, 1, 1.0)])
        noisy = simulator.inject_error(frame, "noise")

        self.assertGreater(len(noisy), len(frame))
        self.assertTrue(noisy.endswith(frame))
        self.assertNotEqual(simulator.START_BYTE, noisy[0])

    def test_parse_args_requires_port_unless_listing_ports(self):
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                simulator.parse_args([])

        args = simulator.parse_args(["--list-ports"])

        self.assertTrue(args.list_ports)
        self.assertIsNone(args.port)

    def test_natural_port_key_sorts_numbered_ports_numerically(self):
        ports = ["COM2", "COM10", "COM1"]

        self.assertEqual(["COM1", "COM2", "COM10"], sorted(ports, key=simulator.natural_port_key))


class SensorSimulatorProfileTests(unittest.TestCase):
    """The generic --profile signal source (scenario-free path)."""

    def test_each_profile_returns_a_finite_value(self):
        for profile in simulator.PROFILES:
            value = simulator.value_for_profile(profile, sensor_number=1, elapsed_seconds=1.0)
            self.assertIsInstance(value, float)
            self.assertFalse(math.isnan(value) or math.isinf(value))

    def test_unsupported_profile_raises(self):
        with self.assertRaises(ValueError):
            simulator.value_for_profile("does-not-exist", 1, 0.0)

    def test_build_sample_frame_with_profile_is_structurally_valid(self):
        frame = simulator.build_sample_frame(
            sensor_count=4,
            timestamp_ms=500,
            profile="sine",
            elapsed_seconds=1.0,
        )

        self.assertEqual(44, len(frame))
        self.assertEqual(simulator.START_BYTE, frame[0])
        self.assertEqual(simulator.END_BYTE, frame[-1])
        self.assertEqual(simulator.calculate_checksum(frame[:-2]), frame[-2])


class SensorSimulatorScenarioTests(unittest.TestCase):
    """The alarm scenarios must produce values that map to the app's validation states."""

    SENSORS = (1, 2, 3, 4)
    # A spread of elapsed times so the oscillating "normal" signal is checked across its range.
    TIMES = (0.0, 0.4, 1.1, 2.7, 5.3, 9.9)

    def test_normal_value_stays_inside_operating_band(self):
        for sensor in self.SENSORS:
            param = simulator.SENSOR_PARAMETERS[sensor]
            for elapsed in self.TIMES:
                value = simulator.value_for_scenario("normal", sensor, elapsed)
                self.assertGreaterEqual(value, param.operating_min)
                self.assertLessEqual(value, param.operating_max)

    def test_out_of_spec_is_plausible_but_above_operating_band(self):
        for sensor in self.SENSORS:
            param = simulator.SENSOR_PARAMETERS[sensor]
            value = simulator.value_for_scenario("out-of-spec", sensor, 1.0)
            self.assertGreater(value, param.operating_max)        # outside operating spec
            self.assertLessEqual(value, param.physical_max)       # still physically plausible

    def test_invalid_is_outside_physical_range(self):
        for sensor in self.SENSORS:
            param = simulator.SENSOR_PARAMETERS[sensor]
            value = simulator.value_for_scenario("invalid", sensor, 1.0)
            self.assertGreater(value, param.physical_max)         # rejected as implausible

    def test_stale_scenario_emits_normal_values(self):
        # Staleness is produced by the run loop going silent, not by the value itself.
        for sensor in self.SENSORS:
            param = simulator.SENSOR_PARAMETERS[sensor]
            value = simulator.value_for_scenario("stale", sensor, 1.0)
            self.assertGreaterEqual(value, param.operating_min)
            self.assertLessEqual(value, param.operating_max)

    def test_target_sensor_limits_anomaly_to_one_sensor(self):
        target = 2
        for sensor in self.SENSORS:
            param = simulator.SENSOR_PARAMETERS[sensor]
            value = simulator.value_for_scenario("out-of-spec", sensor, 1.0, target_sensor=target)
            if sensor == target:
                self.assertGreater(value, param.operating_max)
            else:
                self.assertLessEqual(value, param.operating_max)  # untargeted -> normal

    def test_unknown_sensor_falls_back_to_in_range_constant(self):
        self.assertEqual(1.0, simulator.value_for_scenario("invalid", 99, 1.0))

    def test_build_sample_frame_with_scenario_is_structurally_valid(self):
        frame = simulator.build_sample_frame(
            sensor_count=4,
            timestamp_ms=1000,
            profile="mixed",
            elapsed_seconds=1.0,
            scenario="out-of-spec",
        )

        self.assertEqual(44, len(frame))
        self.assertEqual(simulator.START_BYTE, frame[0])
        self.assertEqual(4, frame[1])
        self.assertEqual(simulator.END_BYTE, frame[-1])
        self.assertEqual(simulator.calculate_checksum(frame[:-2]), frame[-2])

    def test_parse_args_accepts_scenario_and_target_sensor(self):
        args = simulator.parse_args(
            ["--port", "COM10", "--scenario", "invalid", "--target-sensor", "3"]
        )

        self.assertEqual("invalid", args.scenario)
        self.assertEqual(3, args.target_sensor)

    def test_parse_args_defaults_scenario_to_none(self):
        args = simulator.parse_args(["--port", "COM10"])

        self.assertIsNone(args.scenario)
        self.assertIsNone(args.target_sensor)

    def test_parse_args_rejects_target_sensor_beyond_sensor_count(self):
        with contextlib.redirect_stderr(io.StringIO()):
            with self.assertRaises(SystemExit):
                simulator.parse_args(
                    ["--port", "COM10", "--sensors", "2", "--target-sensor", "4"]
                )

    def test_should_emit_frame_goes_silent_only_after_stale_warmup(self):
        warmup = simulator.STALE_WARMUP_SECONDS

        # Non-stale scenarios always emit.
        self.assertTrue(simulator.should_emit_frame(None, warmup + 5))
        self.assertTrue(simulator.should_emit_frame("out-of-spec", warmup + 5))

        # Stale: emits during warm-up, silent afterwards.
        self.assertTrue(simulator.should_emit_frame("stale", warmup - 0.1))
        self.assertFalse(simulator.should_emit_frame("stale", warmup))
        self.assertFalse(simulator.should_emit_frame("stale", warmup + 1))

    def test_signal_description_summarizes_active_source(self):
        self.assertEqual("profile=mixed", simulator.signal_description(None, "mixed", None))
        self.assertEqual(
            "scenario=invalid", simulator.signal_description("invalid", "mixed", None)
        )
        self.assertEqual(
            "scenario=out-of-spec target-sensor=2",
            simulator.signal_description("out-of-spec", "mixed", 2),
        )


if __name__ == "__main__":
    unittest.main()
