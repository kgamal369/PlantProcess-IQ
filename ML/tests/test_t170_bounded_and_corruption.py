"""T-170 part two: bounded reads on a large fixture, corruption, and B-04.

The fixture here is deliberately larger than the memory a bounded read is allowed to
use, because a bounded read that is only tested on data small enough to materialise
has not been tested at all.
"""

import gc
import os
import shutil
import tempfile
import tracemalloc
import unittest

from ppiq_ml.sequences import (
    B04Measurement,
    ChunkCorruptError,
    ChunkMissingError,
    PayloadTruncatedError,
    SequenceContractError,
    SequenceDType,
    SequenceSchema,
    codec_for,
    enabled_codecs,
    iter_chunks,
    measure_setting,
    measure_settings,
    read_manifest,
    verify_payload,
    write_sequence,
)
from tests.test_t170_sequence_payload import CHANNELS, chunk_source

#: Eight channels of twenty-four thousand steps at eight bytes each is about a
#: megabyte and a half of payload. Large enough that materialising it would show up
#: clearly against a bounded read, small enough that this file does not dominate the
#: suite that runs on every pack.
LARGE_CHANNELS = tuple(f"channel_{i:02d}" for i in range(8))
LARGE_STEPS = 24_000


def large_source(chunk_steps):
    return chunk_source(LARGE_STEPS, chunk_steps, channel_count=len(LARGE_CHANNELS))


class ALargePayloadIsReadWithoutMaterialisingIt(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t170-large-")
        self.schema = SequenceSchema(LARGE_CHANNELS, SequenceDType.FLOAT64, 2_000)
        self.path = os.path.join(self.root, "large.ppiqseq")
        self.manifest = write_sequence(
            self.path, "large_fixture", self.schema,
            large_source(2_000), codec_for("deflate"),
        )

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def peak_of(self, work):
        gc.collect()
        tracemalloc.start()
        try:
            result = work()
            _, peak = tracemalloc.get_traced_memory()
        finally:
            tracemalloc.stop()
        return result, peak

    def test_the_fixture_is_genuinely_larger_than_one_chunk(self):
        self.assertEqual(LARGE_STEPS, self.manifest.total_steps)
        self.assertEqual(12, self.manifest.chunk_count)
        self.assertGreater(self.manifest.uncompressed_bytes, 1_500_000)

    def test_a_full_read_never_allocates_the_whole_payload(self):
        def walk():
            steps = 0
            for chunk in iter_chunks(self.path, self.manifest):
                steps += chunk.steps
            return steps

        steps, peak = self.peak_of(walk)
        self.assertEqual(LARGE_STEPS, steps)
        self.assertLess(peak, self.manifest.uncompressed_bytes)

    def test_the_peak_does_not_grow_when_the_payload_grows(self):
        """The claim this library exists to support, stated so it can be falsified.

        Same chunk size, four times the payload. If a read materialised what it was
        given, the peak would follow the payload. It does not, because it follows
        the chunk.
        """
        bigger_path = os.path.join(self.root, "bigger.ppiqseq")
        bigger = write_sequence(
            bigger_path, "bigger_fixture",
            SequenceSchema(LARGE_CHANNELS, SequenceDType.FLOAT64, 2_000),
            chunk_source(LARGE_STEPS * 4, 2_000, channel_count=len(LARGE_CHANNELS)),
            codec_for("deflate"),
        )

        def walk(path, manifest):
            def work():
                for _ in iter_chunks(path, manifest):
                    pass
            return work

        _, small_peak = self.peak_of(walk(self.path, self.manifest))
        _, large_peak = self.peak_of(walk(bigger_path, bigger))

        self.assertEqual(4 * self.manifest.uncompressed_bytes, bigger.uncompressed_bytes)
        self.assertLess(large_peak, small_peak * 1.5)

    def test_the_peak_tracks_the_chunk_size_not_the_payload_size(self):
        """Halving the chunk roughly halves the memory a read needs."""
        small_path = os.path.join(self.root, "small_chunks.ppiqseq")
        small_manifest = write_sequence(
            small_path, "large_fixture",
            SequenceSchema(LARGE_CHANNELS, SequenceDType.FLOAT64, 500),
            large_source(500), codec_for("deflate"),
        )

        def walk(path, manifest):
            def work():
                for _ in iter_chunks(path, manifest):
                    pass
            return work

        _, wide_peak = self.peak_of(walk(self.path, self.manifest))
        _, narrow_peak = self.peak_of(walk(small_path, small_manifest))
        self.assertLess(narrow_peak, wide_peak)
        self.assertEqual(
            self.manifest.payload_content_hash, small_manifest.payload_content_hash
        )

    def test_reading_only_the_manifest_touches_almost_nothing(self):
        _, peak = self.peak_of(lambda: read_manifest(self.path))
        self.assertLess(peak, self.manifest.uncompressed_bytes / 10)

    def test_asking_for_the_byte_hash_is_an_explicit_and_separate_cost(self):
        plain = read_manifest(self.path)
        self.assertEqual("", plain.payload_byte_hash)
        hashed = read_manifest(self.path, with_byte_hash=True)
        self.assertEqual(self.manifest.payload_byte_hash, hashed.payload_byte_hash)

    def test_the_whole_large_payload_verifies(self):
        outcome = verify_payload(self.path, self.manifest)
        self.assertEqual(LARGE_STEPS, outcome["steps_read"])
        self.assertEqual(self.manifest.payload_content_hash, outcome["payload_content_hash"])


class CorruptionIsDetectedRatherThanReturned(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t170-corrupt-")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def seal(self, name="payload", codec_name="stored", chunk_steps=256, steps=2_000):
        path = os.path.join(self.root, f"{name}.ppiqseq")
        manifest = write_sequence(
            path, name,
            SequenceSchema(CHANNELS, SequenceDType.FLOAT64, chunk_steps),
            chunk_source(steps, chunk_steps), codec_for(codec_name),
        )
        return path, manifest

    def test_a_single_flipped_bit_in_a_chunk_is_caught_by_its_hash(self):
        path, manifest = self.seal()
        target = manifest.chunks[3]
        with open(path, "r+b") as handle:
            handle.seek(target.file_offset + 16)
            original = handle.read(1)
            handle.seek(target.file_offset + 16)
            handle.write(bytes([original[0] ^ 0x01]))

        with self.assertRaises(ChunkCorruptError) as raised:
            list(iter_chunks(path, manifest))
        self.assertIn("Chunk 3", str(raised.exception))
        self.assertIn("not the bytes that were sealed", str(raised.exception))

    def test_an_untouched_chunk_before_the_corrupted_one_still_reads(self):
        """Corruption surfaces at the chunk that carries it, not at the file."""
        path, manifest = self.seal()
        target = manifest.chunks[5]
        with open(path, "r+b") as handle:
            handle.seek(target.file_offset)
            handle.write(b"\x00\x00\x00\x00\x00\x00\x00\x00")

        reader = iter_chunks(path, manifest)
        for expected in range(5):
            self.assertEqual(expected, next(reader).ordinal)
        with self.assertRaises(ChunkCorruptError):
            next(reader)

    def test_a_missing_chunk_is_named_rather_than_skipped(self):
        path, manifest = self.seal()
        last = manifest.chunks[-1]
        with open(path, "r+b") as handle:
            handle.truncate(last.file_offset + 4)

        with self.assertRaises((ChunkMissingError, PayloadTruncatedError)) as raised:
            list(iter_chunks(path, manifest))
        self.assertTrue(str(raised.exception))

    def test_a_file_cut_short_is_refused_before_any_chunk_is_decoded(self):
        path, manifest = self.seal()
        with open(path, "r+b") as handle:
            handle.truncate(os.path.getsize(path) - 32)

        with self.assertRaises(PayloadTruncatedError) as raised:
            read_manifest(path)
        self.assertIn("cut short", str(raised.exception))

    def test_a_file_that_is_not_a_payload_at_all_is_refused(self):
        path = os.path.join(self.root, "stranger.bin")
        with open(path, "wb") as handle:
            handle.write(b"this is not a sequence payload" * 40)
        with self.assertRaises(SequenceContractError):
            read_manifest(path)

    def test_a_reordered_index_is_caught_by_the_payload_hash(self):
        """Swapping two entries changes the data the payload claims to be."""
        path, manifest = self.seal()
        swapped = list(manifest.chunks)
        swapped[1], swapped[2] = swapped[2], swapped[1]
        reordered = type(manifest)(
            manifest_kind=manifest.manifest_kind,
            format_version=manifest.format_version,
            sequence_id=manifest.sequence_id,
            schema=manifest.schema,
            codec_name=manifest.codec_name,
            total_steps=manifest.total_steps,
            chunks=tuple(swapped),
            payload_content_hash=manifest.payload_content_hash,
            payload_byte_hash=manifest.payload_byte_hash,
            stored_bytes=manifest.stored_bytes,
            uncompressed_bytes=manifest.uncompressed_bytes,
            chunk_stored_bytes=manifest.chunk_stored_bytes,
        )
        with self.assertRaises(ChunkCorruptError):
            verify_payload(path, reordered)

    def test_a_compressed_chunk_of_nonsense_is_refused_by_the_codec(self):
        path, manifest = self.seal(codec_name="deflate")
        target = manifest.chunks[2]
        with open(path, "r+b") as handle:
            handle.seek(target.file_offset)
            handle.write(b"\xff" * min(24, target.stored_bytes))

        with self.assertRaises(SequenceContractError) as raised:
            list(iter_chunks(path, manifest))
        self.assertTrue(str(raised.exception))


class TheB04HookMeasuresAndDecidesNothing(unittest.TestCase):
    """The sweep is measured once for the whole class.

    Every assertion below reads the same measurements. Repeating a full write and
    read per test method would multiply the cost of this file by six and would not
    produce a single additional fact.
    """

    @classmethod
    def setUpClass(cls):
        cls.root = tempfile.mkdtemp(prefix="ppiq-t170-b04-")
        cls.sweep = measure_settings(
            cls.root, "b04_fixture", LARGE_CHANNELS, SequenceDType.FLOAT64,
            (1_000, 4_000), large_source,
            codecs=(codec_for("stored"), codec_for("deflate")),
        )

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.root, ignore_errors=True)

    def measure(self, chunk_sizes=(1_000, 4_000), codecs=None):
        if chunk_sizes == (1_000, 4_000) and codecs is None:
            return self.sweep
        return measure_settings(
            self.root, "b04_fixture", LARGE_CHANNELS, SequenceDType.FLOAT64,
            chunk_sizes, large_source,
            codecs=codecs if codecs is not None else (codec_for("stored"), codec_for("deflate")),
        )

    def test_at_least_two_chunk_and_compression_settings_are_measured(self):
        results = self.measure()
        self.assertEqual(4, len(results))
        self.assertGreaterEqual(len({r.chunk_steps for r in results}), 2)
        self.assertGreaterEqual(len({r.codec_name for r in results}), 2)

    def test_every_enabled_codec_can_be_measured(self):
        """The slow codec is exercised once, on a small fixture, rather than on
        every sweep. Measuring it repeatedly would tell us nothing new and would
        make this file dominate the suite."""
        results = measure_settings(
            self.root, "b04_small", CHANNELS, SequenceDType.FLOAT64,
            (400,), lambda chunk_steps: chunk_source(1_200, chunk_steps),
        )
        self.assertEqual(len(enabled_codecs()), len(results))
        self.assertEqual({c.name for c in enabled_codecs()}, {r.codec_name for r in results})

    def test_every_setting_measures_the_same_data(self):
        """Without this the comparison would be between two different payloads."""
        results = self.measure()
        self.assertEqual(1, len({r.payload_content_hash for r in results}))
        self.assertEqual(1, len({r.uncompressed_bytes for r in results}))

    def test_each_measurement_carries_its_numbers_and_no_verdict(self):
        for result in self.sweep:
            self.assertIsInstance(result, B04Measurement)
            self.assertEqual("B-04", result.benchmark_id)
            self.assertGreater(result.write_seconds, 0.0)
            self.assertGreater(result.full_read_seconds, 0.0)
            self.assertGreater(result.stored_bytes, 0)
            self.assertGreater(result.peak_read_bytes, 0)
            self.assertGreater(result.read_steps_per_second, 0.0)
            record = result.to_dict()
            for absent in ("selected", "winner", "verdict", "passed", "recommended"):
                self.assertNotIn(absent, record)

    def test_the_measured_peak_read_follows_the_chunk_and_not_the_payload(self):
        """A ratio against the payload would be a number I chose. This is a property.

        Decoding turns eight packed bytes into a Python float object several times
        that size, so a large chunk over a small payload can peak above the payload
        itself. What must hold, and does, is that the smaller chunk setting peaks
        lower than the larger one on identical data.
        """
        by_setting = {(r.codec_name, r.chunk_steps): r.peak_read_bytes for r in self.sweep}
        for codec in {r.codec_name for r in self.sweep}:
            self.assertLess(
                by_setting[(codec, 1_000)], by_setting[(codec, 4_000)], codec
            )

    def test_a_compressing_setting_reports_a_ratio_above_one(self):
        results = {r.codec_name: r for r in self.sweep}
        self.assertAlmostEqual(1.0, results["stored"].compression_ratio, places=9)
        self.assertGreater(results["deflate"].compression_ratio, 1.0)

    def test_one_setting_can_be_measured_on_its_own(self):
        result = measure_setting(
            self.root, "single", CHANNELS, SequenceDType.FLOAT64,
            400, codec_for("stored"), lambda chunk_steps: chunk_source(1_200, chunk_steps),
        )
        self.assertEqual(1_200, result.total_steps)
        self.assertEqual(3, result.chunk_count)
        self.assertEqual(len(CHANNELS), result.channel_count)


if __name__ == "__main__":
    unittest.main()
