"""T-170 part one: the payload contract, the chunk index and the codec seam."""

import os
import shutil
import tempfile
import unittest

from ppiq_ml.sequences import (
    FORMAT_VERSION,
    MANIFEST_KIND,
    SequenceContractError,
    SequenceDType,
    SequenceSchema,
    codec_for,
    codec_names,
    decode,
    default_codec,
    enabled_codecs,
    encode,
    item_bytes,
    iter_chunks,
    read_channel,
    read_manifest,
    verify_payload,
    write_sequence,
)

CHANNELS = ("channel_alpha", "channel_beta", "channel_gamma")


def sample(channel, step, seed=20260815):
    """A value that depends only on which channel and which absolute step it is.

    Deliberately not a running generator. A sequential state advances differently
    when the chunk size changes, so two chunk sizes would carry genuinely different
    data and a comparison between them would be measuring the fixture.
    """
    state = (seed + channel * 7919 + step * 104729) & 0x7FFFFFFF
    state = (state * 1103515245 + 12345) & 0x7FFFFFFF
    state = (state * 1103515245 + 12345) & 0x7FFFFFFF
    return (state / 0x7FFFFFFF) * 10.0 + channel * 100.0 + step * 0.001


def chunk_source(total_steps, chunk_steps, channel_count=3, seed=20260815):
    """Yield one chunk at a time. Never materialises the whole payload."""

    def generate():
        produced = 0
        while produced < total_steps:
            steps = min(chunk_steps, total_steps - produced)
            yield [
                [sample(channel, produced + offset, seed) for offset in range(steps)]
                for channel in range(channel_count)
            ]
            produced += steps

    return generate()


class TheElementLayoutIsExplicitAndReversible(unittest.TestCase):
    def test_every_type_declares_its_own_width(self):
        self.assertEqual(2, item_bytes(SequenceDType.INT16))
        self.assertEqual(4, item_bytes(SequenceDType.INT32))
        self.assertEqual(8, item_bytes(SequenceDType.INT64))
        self.assertEqual(4, item_bytes(SequenceDType.FLOAT32))
        self.assertEqual(8, item_bytes(SequenceDType.FLOAT64))

    def test_a_known_value_packs_to_a_known_width(self):
        self.assertEqual(12, len(encode(SequenceDType.INT32, [1, 2, 3])))
        self.assertEqual(24, len(encode(SequenceDType.FLOAT64, [1.0, 2.0, 3.0])))

    def test_double_precision_round_trips_exactly(self):
        values = [1.5, -2.25, 1e10, 0.0]
        self.assertEqual(
            tuple(values), decode(SequenceDType.FLOAT64, encode(SequenceDType.FLOAT64, values))
        )

    def test_integers_round_trip_exactly(self):
        values = [-32768, 0, 32767]
        self.assertEqual(
            tuple(values), decode(SequenceDType.INT16, encode(SequenceDType.INT16, values))
        )

    def test_a_value_outside_the_type_is_refused_rather_than_wrapped(self):
        with self.assertRaises(SequenceContractError):
            encode(SequenceDType.INT16, [70000])

    def test_a_byte_length_that_does_not_divide_is_refused(self):
        with self.assertRaises(SequenceContractError) as raised:
            decode(SequenceDType.FLOAT64, b"12345")
        self.assertIn("not what its header declares", str(raised.exception))


class TheCodecSeamIsNamedAndReplaceable(unittest.TestCase):
    def test_more_than_one_codec_is_enabled(self):
        self.assertGreaterEqual(len(enabled_codecs()), 2)
        self.assertIn("stored", codec_names())

    def test_a_codec_is_resolved_by_the_name_a_payload_records(self):
        self.assertEqual("deflate", codec_for("deflate").name)

    def test_an_unknown_codec_is_refused_rather_than_guessed(self):
        with self.assertRaises(SequenceContractError) as raised:
            codec_for("something_else")
        self.assertIn("guessing would produce numbers", str(raised.exception))

    def test_asking_for_a_default_codec_is_an_error(self):
        """No setting is selected here. B-04 measures and a later decision chooses."""
        with self.assertRaises(SequenceContractError) as raised:
            default_codec()
        self.assertIn("deliberately does not choose", str(raised.exception))

    def test_every_codec_round_trips_the_same_bytes(self):
        raw = b"".join(bytes([i % 251]) for i in range(5000))
        for codec in enabled_codecs():
            self.assertEqual(raw, codec.decompress(codec.compress(raw), len(raw)), codec.name)


class TheSchemaRefusesWhatItCannotDescribe(unittest.TestCase):
    def test_a_sequence_needs_at_least_one_channel(self):
        with self.assertRaises(SequenceContractError):
            SequenceSchema((), SequenceDType.FLOAT32, 100)

    def test_channel_names_must_be_unique(self):
        with self.assertRaises(SequenceContractError):
            SequenceSchema(("a", "a"), SequenceDType.FLOAT32, 100)

    def test_a_chunk_must_carry_at_least_one_step(self):
        with self.assertRaises(SequenceContractError):
            SequenceSchema(("a",), SequenceDType.FLOAT32, 0)

    def test_the_schema_survives_a_round_trip_through_the_header(self):
        schema = SequenceSchema(CHANNELS, SequenceDType.FLOAT64, 512)
        self.assertEqual(schema, SequenceSchema.from_dict(schema.to_dict()))


class APayloadRoundTripsThroughEveryCodec(unittest.TestCase):
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="ppiq-t170-")

    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

    def seal(self, codec, total_steps=2500, chunk_steps=256, dtype=SequenceDType.FLOAT64):
        schema = SequenceSchema(CHANNELS, dtype, chunk_steps)
        path = os.path.join(self.root, f"payload_{codec.name}.ppiqseq")
        manifest = write_sequence(
            path, "fixture_sequence", schema,
            chunk_source(total_steps, chunk_steps), codec,
        )
        return path, manifest

    def test_the_manifest_describes_what_was_written(self):
        for codec in enabled_codecs():
            path, manifest = self.seal(codec)
            self.assertEqual(MANIFEST_KIND, manifest.manifest_kind)
            self.assertEqual(FORMAT_VERSION, manifest.format_version)
            self.assertEqual(2500, manifest.total_steps)
            self.assertEqual(10, manifest.chunk_count)
            self.assertEqual(codec.name, manifest.codec_name)
            self.assertEqual(os.path.getsize(path), manifest.stored_bytes)

    def test_the_index_covers_every_step_exactly_once_in_order(self):
        _, manifest = self.seal(enabled_codecs()[0])
        expected_first = 0
        for ordinal, entry in enumerate(manifest.chunks):
            self.assertEqual(ordinal, entry.ordinal)
            self.assertEqual(expected_first, entry.first_step)
            expected_first += entry.steps
        self.assertEqual(manifest.total_steps, expected_first)

    def test_the_values_read_back_are_the_values_written(self):
        expected = [list(c) for c in next(iter(chunk_source(256, 256)))]
        for codec in enabled_codecs():
            path, manifest = self.seal(codec)
            first = next(iter_chunks(path, manifest))
            for position in range(len(CHANNELS)):
                for step in range(256):
                    self.assertAlmostEqual(
                        expected[position][step], first.channel(position)[step], places=12
                    )

    def test_the_payload_content_hash_does_not_depend_on_the_codec(self):
        """A change of compression must not look like a change of data."""
        hashes = {self.seal(codec)[1].payload_content_hash for codec in enabled_codecs()}
        self.assertEqual(1, len(hashes))

    def test_the_payload_content_hash_does_not_depend_on_the_chunk_size(self):
        first = self.seal(enabled_codecs()[0], chunk_steps=256)[1]
        second = self.seal(enabled_codecs()[0], chunk_steps=500)[1]
        self.assertEqual(first.payload_content_hash, second.payload_content_hash)
        self.assertNotEqual(first.chunk_count, second.chunk_count)

    def test_the_byte_hash_does_depend_on_the_codec(self):
        hashes = {self.seal(codec)[1].payload_byte_hash for codec in enabled_codecs()}
        self.assertEqual(len(enabled_codecs()), len(hashes))

    def test_a_compressing_codec_stores_fewer_bytes_than_the_stored_codec(self):
        plain = self.seal(codec_for("stored"))[1]
        packed = self.seal(codec_for("deflate"))[1]
        self.assertEqual(plain.uncompressed_bytes, packed.uncompressed_bytes)
        self.assertLess(packed.stored_bytes, plain.stored_bytes)
        self.assertGreater(packed.compression_ratio, 1.0)
        self.assertAlmostEqual(1.0, plain.compression_ratio, delta=0.01)

    def test_the_whole_payload_verifies_chunk_by_chunk(self):
        for codec in enabled_codecs():
            path, manifest = self.seal(codec)
            outcome = verify_payload(path, manifest)
            self.assertEqual(manifest.chunk_count, outcome["chunks_read"])
            self.assertEqual(manifest.total_steps, outcome["steps_read"])
            self.assertEqual(manifest.payload_content_hash, outcome["payload_content_hash"])

    def test_a_final_short_chunk_is_carried_and_described(self):
        schema = SequenceSchema(CHANNELS, SequenceDType.FLOAT64, 256)
        path = os.path.join(self.root, "ragged.ppiqseq")
        manifest = write_sequence(
            path, "ragged", schema, chunk_source(600, 256), codec_for("stored")
        )
        self.assertEqual(3, manifest.chunk_count)
        self.assertEqual([256, 256, 88], [c.steps for c in manifest.chunks])
        self.assertEqual(600, manifest.total_steps)

    def test_single_precision_halves_the_payload(self):
        wide = self.seal(codec_for("stored"), dtype=SequenceDType.FLOAT64)[1]
        narrow = self.seal(codec_for("stored"), dtype=SequenceDType.FLOAT32)[1]
        self.assertEqual(wide.uncompressed_bytes, narrow.uncompressed_bytes * 2)

    def test_one_channel_can_be_streamed_without_naming_the_others(self):
        path, manifest = self.seal(codec_for("deflate"))
        collected = 0
        for first_step, values in read_channel(path, "channel_beta", manifest):
            self.assertGreaterEqual(first_step, 0)
            collected += len(values)
        self.assertEqual(manifest.total_steps, collected)

    def test_an_undeclared_channel_is_refused(self):
        path, manifest = self.seal(codec_for("stored"))
        with self.assertRaises(SequenceContractError) as raised:
            list(read_channel(path, "channel_absent", manifest))
        self.assertIn("not declared by this payload", str(raised.exception))

    def test_the_manifest_can_be_read_without_touching_a_chunk(self):
        path, written = self.seal(codec_for("deflate"))
        reopened = read_manifest(path)
        self.assertEqual(written.payload_content_hash, reopened.payload_content_hash)
        self.assertEqual(written.chunk_count, reopened.chunk_count)
        self.assertEqual(written.schema, reopened.schema)

    def test_an_empty_payload_is_refused_and_leaves_no_file(self):
        schema = SequenceSchema(CHANNELS, SequenceDType.FLOAT64, 128)
        path = os.path.join(self.root, "empty.ppiqseq")
        with self.assertRaises(SequenceContractError) as raised:
            write_sequence(path, "empty", schema, iter(()), codec_for("stored"))
        self.assertIn("describe nothing", str(raised.exception))
        self.assertFalse(os.path.exists(path))
        self.assertFalse(os.path.exists(path + ".partial"))

    def test_a_chunk_wider_than_the_declared_size_is_refused(self):
        schema = SequenceSchema(CHANNELS, SequenceDType.FLOAT64, 4)
        path = os.path.join(self.root, "toowide.ppiqseq")
        oversized = [[[1.0] * 9, [2.0] * 9, [3.0] * 9]]
        with self.assertRaises(SequenceContractError):
            write_sequence(path, "toowide", schema, iter(oversized), codec_for("stored"))

    def test_channels_of_unequal_length_in_one_chunk_are_refused(self):
        schema = SequenceSchema(CHANNELS, SequenceDType.FLOAT64, 8)
        path = os.path.join(self.root, "ragged_channels.ppiqseq")
        bad = [[[1.0] * 8, [2.0] * 7, [3.0] * 8]]
        with self.assertRaises(SequenceContractError) as raised:
            write_sequence(path, "bad", schema, iter(bad), codec_for("stored"))
        self.assertIn("covers the same steps", str(raised.exception))


if __name__ == "__main__":
    unittest.main()
