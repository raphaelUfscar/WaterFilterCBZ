import contextlib
import io
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


if __name__ == "__main__":
    unittest.main()
