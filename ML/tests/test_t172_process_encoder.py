"""T-172: the encoder contract, proven on a deterministic sealed fixture.

Every window in this file comes from a sealed T-170 sequence payload written in the
test that uses it. Nothing is read from a database, and there is no path by which it
could be.
"""

import os
import shutil
import tempfile
import unittest

from ppiq_ml.encoders import (
    EMBEDDING_TOLERANCE,
    ChannelSet,
    ChannelSetIncompatibleError,
    EncoderArtifactInvalidError,
    EncoderContractError,
    EncoderEligibilityError,
    EncoderRefusalCode,
    TemporalConvolutionEncoder,
    TrainingConfig,
    collect_windows,
    evaluate_training_eligibility,
    framework_environment,
    measure_encoder,
    training_input_identity,
)
from ppiq_ml.sequences import (
    SequenceDType,
    SequenceSchema,
    codec_for,
    read_manifest,
    write_sequence,
)

CHANNEL_NAMES = ("channel_alpha", "channel_beta", "channel_gamma", "channel_delta")
WINDOW_STEPS = 32
TRAINING_WINDOWS = 24
SMALL_CONFIG = TrainingConfig(epochs=2, embedding_dimension=6, hidden_channels=12)


def sample(channel, step, seed=20260815):
    """A value that depends only on which channel and which absolute step it is."""
    state = (seed + channel * 7919 + step * 104729) & 0x7FFFFFFF
    state = (state * 1103515245 + 12345) & 0x7FFFFFFF
    state = (state * 1103515245 + 12345) & 0x7FFFFFFF
    return (state / 0x7FFFFFFF) * 4.0 + channel * 2.0


def seal_sequence(directory, total_steps=TRAINING_WINDOWS * WINDOW_STEPS, chunk_steps=128):
    """Write a sealed T-170 payload. This is the only input the encoder ever sees."""
    schema = SequenceSchema(CHANNEL_NAMES, SequenceDType.FLOAT64, chunk_steps)
    path = os.path.join(directory, "process_sequence.ppiqseq")

    def chunks():
        produced = 0
        while produced < total_steps:
            steps = min(chunk_steps, total_steps - produced)
            yield [
                [sample(channel, produced + offset) for offset in range(steps)]
                for channel in range(len(CHANNEL_NAMES))
            ]
            produced += steps

    manifest = write_sequence(path, "process_fixture", schema, chunks(), codec_for("deflate"))
    return path, manifest


class EncoderCase(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.root = tempfile.mkdtemp(prefix="ppiq-t172-")
        cls.path, cls.payload = seal_sequence(cls.root)
        cls.channel_set = ChannelSet("channel-set-1", CHANNEL_NAMES)
        cls.windows = collect_windows(cls.path, cls.channel_set, WINDOW_STEPS)
        cls.input_identity = training_input_identity(
            cls.payload.payload_content_hash, cls.channel_set, WINDOW_STEPS, WINDOW_STEPS, None
        )

    @classmethod
    def tearDownClass(cls):
        shutil.rmtree(cls.root, ignore_errors=True)

    def fitted(self, channel_set=None, config=None, identity=None):
        encoder = TemporalConvolutionEncoder()
        encoder.train(
            self.windows,
            channel_set or self.channel_set,
            identity or self.input_identity,
            config or SMALL_CONFIG,
        )
        return encoder


class TheInputComesFromASealedSequenceArtifact(EncoderCase):
    def test_the_fixture_seals_and_windows_as_expected(self):
        self.assertEqual(TRAINING_WINDOWS * WINDOW_STEPS, self.payload.total_steps)
        self.assertEqual(TRAINING_WINDOWS, len(self.windows))
        self.assertEqual(len(CHANNEL_NAMES), len(self.windows[0]))
        self.assertEqual(WINDOW_STEPS, len(self.windows[0][0]))

    def test_windowing_does_not_depend_on_how_the_payload_was_chunked(self):
        """A window that changed with the chunk size would make the input identity a
        property of the storage rather than of the data."""
        other_directory = tempfile.mkdtemp(dir=self.root)
        other_path, other_payload = seal_sequence(other_directory, chunk_steps=97)
        other_windows = collect_windows(other_path, self.channel_set, WINDOW_STEPS)
        self.assertEqual(self.payload.payload_content_hash, other_payload.payload_content_hash)
        self.assertEqual(self.windows, other_windows)

    def test_a_channel_the_payload_does_not_carry_is_refused(self):
        absent = ChannelSet("channel-set-1", CHANNEL_NAMES[:3] + ("channel_absent",))
        with self.assertRaises(EncoderContractError) as raised:
            collect_windows(self.path, absent, WINDOW_STEPS)
        self.assertIn("does not carry every channel", str(raised.exception))

    def test_the_input_identity_covers_the_channels_and_the_windowing(self):
        base = self.input_identity
        other_channels = training_input_identity(
            self.payload.payload_content_hash,
            ChannelSet("channel-set-1", CHANNEL_NAMES[:3]),
            WINDOW_STEPS, WINDOW_STEPS, None,
        )
        other_window = training_input_identity(
            self.payload.payload_content_hash, self.channel_set, 16, 16, None
        )
        self.assertNotEqual(base, other_channels)
        self.assertNotEqual(base, other_window)


class TrainingIsReproducibleWithinTheDeclaredTolerance(EncoderCase):
    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        cls.first = TemporalConvolutionEncoder()
        cls.first_manifest = cls.first.train(
            cls.windows, cls.channel_set, cls.input_identity, SMALL_CONFIG
        )
        cls.second = TemporalConvolutionEncoder()
        cls.second_manifest = cls.second.train(
            cls.windows, cls.channel_set, cls.input_identity, SMALL_CONFIG
        )

    def test_two_runs_report_the_same_logical_training_input_identity(self):
        self.assertEqual(
            self.first_manifest.training_input_identity,
            self.second_manifest.training_input_identity,
        )
        self.assertEqual(self.input_identity, self.first_manifest.training_input_identity)

    def test_two_runs_produce_the_same_artifact_identity(self):
        self.assertEqual(
            self.first_manifest.artifact_identity, self.second_manifest.artifact_identity
        )

    def test_two_runs_produce_embeddings_within_the_declared_tolerance(self):
        left = self.first.encode(self.windows[:8], self.channel_set, self.input_identity)
        right = self.second.encode(self.windows[:8], self.channel_set, self.input_identity)
        worst = max(
            abs(a - b)
            for x, y in zip(left.embeddings, right.embeddings)
            for a, b in zip(x, y)
        )
        self.assertLess(worst, EMBEDDING_TOLERANCE)

    def test_encoding_twice_with_one_encoder_is_reproducible(self):
        left = self.first.encode(self.windows[:8], self.channel_set, self.input_identity)
        right = self.first.encode(self.windows[:8], self.channel_set, self.input_identity)
        self.assertEqual(left.embeddings, right.embeddings)

    def test_a_different_seed_produces_a_different_encoder(self):
        other = self.fitted(config=TrainingConfig(**{**SMALL_CONFIG.to_dict(), "seed": 99}))
        self.assertNotEqual(
            self.first_manifest.artifact_identity, other.manifest.artifact_identity
        )

    def test_the_declared_tolerance_is_recorded_on_the_manifest(self):
        self.assertEqual(EMBEDDING_TOLERANCE, self.first_manifest.numerical_tolerance)


class TheArtifactCarriesTheEncoderAndItsIdentity(EncoderCase):
    def test_a_saved_and_reloaded_encoder_produces_the_same_embeddings(self):
        encoder = self.fitted()
        before = encoder.encode(self.windows[:6], self.channel_set, self.input_identity)
        path = os.path.join(tempfile.mkdtemp(dir=self.root), "encoder.pt")
        saved = encoder.save(path)

        reloaded = TemporalConvolutionEncoder.load(path)
        after = reloaded.encode(self.windows[:6], self.channel_set, self.input_identity)

        self.assertEqual(saved.artifact_identity, reloaded.manifest.artifact_identity)
        worst = max(
            abs(a - b)
            for x, y in zip(before.embeddings, after.embeddings)
            for a, b in zip(x, y)
        )
        self.assertLess(worst, EMBEDDING_TOLERANCE)

    def test_the_artifact_identity_does_not_depend_on_the_serialised_bytes(self):
        """Stated because the framework does not promise byte-identical files.

        The identity is derived from the architecture, the channel set, the seed and
        the training input. Two serialisations of the same encoder are the same
        encoder whether or not their bytes agree.
        """
        encoder = self.fitted()
        first = os.path.join(tempfile.mkdtemp(dir=self.root), "a.pt")
        second = os.path.join(tempfile.mkdtemp(dir=self.root), "b.pt")
        one = encoder.save(first)
        two = encoder.save(second)
        self.assertEqual(one.artifact_identity, two.artifact_identity)
        self.assertGreater(one.artifact_bytes, 0)

    def test_the_manifest_records_size_and_a_byte_hash_as_observations(self):
        encoder = self.fitted()
        path = os.path.join(tempfile.mkdtemp(dir=self.root), "encoder.pt")
        saved = encoder.save(path)
        self.assertEqual(os.path.getsize(path), saved.artifact_bytes)
        self.assertEqual(64, len(saved.artifact_byte_hash))

    def test_an_encoder_that_has_not_been_trained_refuses_to_describe_itself(self):
        with self.assertRaises(EncoderContractError):
            TemporalConvolutionEncoder().manifest

    def test_training_a_second_time_on_one_encoder_is_refused(self):
        encoder = self.fitted()
        with self.assertRaises(EncoderContractError) as raised:
            encoder.train(self.windows, self.channel_set, self.input_identity, SMALL_CONFIG)
        self.assertIn("may already cite", str(raised.exception))


class AChangedChannelSetVersionMakesTheEncoderIncompatible(EncoderCase):
    """The central falsification for MF-01."""

    def test_a_new_channel_set_version_is_refused_by_the_prior_encoder(self):
        encoder = self.fitted()
        moved_on = ChannelSet("channel-set-2", CHANNEL_NAMES)
        with self.assertRaises(ChannelSetIncompatibleError) as raised:
            encoder.encode(self.windows[:4], moved_on, self.input_identity)
        self.assertIn("channel-set-1", str(raised.exception))
        self.assertIn("channel-set-2", str(raised.exception))
        self.assertIn("refused as incompatible", str(raised.exception))

    def test_the_same_version_over_changed_channels_is_also_refused(self):
        """The one case a version cannot catch on its own, caught by the identity."""
        encoder = self.fitted()
        reordered = ChannelSet("channel-set-1", tuple(reversed(CHANNEL_NAMES)))
        with self.assertRaises(ChannelSetIncompatibleError) as raised:
            encoder.encode(self.windows[:4], reordered, self.input_identity)
        self.assertIn("reused for a changed channel set", str(raised.exception))

    def test_a_window_of_the_wrong_length_is_refused(self):
        encoder = self.fitted()
        short = tuple(tuple(channel[:16] for channel in window) for window in self.windows[:4])
        with self.assertRaises(EncoderContractError) as raised:
            encoder.encode(short, self.channel_set, self.input_identity)
        self.assertIn("was handed windows of 16", str(raised.exception))


class AnUnusableArtifactOrPopulationIsRefused(EncoderCase):
    def test_a_missing_artifact_is_refused(self):
        with self.assertRaises(EncoderArtifactInvalidError):
            TemporalConvolutionEncoder.load(os.path.join(self.root, "absent.pt"))

    def test_a_corrupted_artifact_is_refused_rather_than_partially_loaded(self):
        encoder = self.fitted()
        path = os.path.join(tempfile.mkdtemp(dir=self.root), "encoder.pt")
        encoder.save(path)
        with open(path, "r+b") as handle:
            handle.seek(64)
            handle.write(b"\x00" * 256)
        with self.assertRaises(EncoderArtifactInvalidError):
            TemporalConvolutionEncoder.load(path)

    def test_a_file_that_is_not_an_encoder_at_all_is_refused(self):
        path = os.path.join(tempfile.mkdtemp(dir=self.root), "stranger.pt")
        with open(path, "wb") as handle:
            handle.write(b"this is not an encoder artifact" * 20)
        with self.assertRaises(EncoderArtifactInvalidError):
            TemporalConvolutionEncoder.load(path)

    def test_a_single_channel_set_is_refused_with_both_numbers(self):
        single = ChannelSet("channel-set-1", ("channel_alpha",))
        verdict = evaluate_training_eligibility(
            [[[1.0] * WINDOW_STEPS]] * TRAINING_WINDOWS, single
        )
        self.assertFalse(verdict.eligible)
        self.assertEqual(EncoderRefusalCode.INSUFFICIENT_CHANNELS, verdict.code)
        self.assertEqual(2.0, verdict.required)
        self.assertEqual(1.0, verdict.observed)

    def test_a_window_too_short_to_carry_shape_is_refused(self):
        stubby = [[[1.0] * 4 for _ in CHANNEL_NAMES] for _ in range(TRAINING_WINDOWS)]
        verdict = evaluate_training_eligibility(stubby, self.channel_set)
        self.assertFalse(verdict.eligible)
        self.assertEqual(EncoderRefusalCode.INVALID_SEQUENCE_SHAPE, verdict.code)

    def test_too_few_windows_is_refused_and_nothing_is_trained(self):
        few = self.windows[:4]
        encoder = TemporalConvolutionEncoder()
        with self.assertRaises(EncoderEligibilityError) as raised:
            encoder.train(few, self.channel_set, self.input_identity, SMALL_CONFIG)
        self.assertIn("would memorise them", str(raised.exception))
        with self.assertRaises(EncoderContractError):
            encoder.manifest

    def test_a_ragged_window_is_refused(self):
        ragged = [list(w) for w in self.windows[:20]]
        ragged[3] = [list(c) for c in ragged[3]]
        ragged[3][1] = ragged[3][1][:10]
        encoder = TemporalConvolutionEncoder()
        with self.assertRaises(EncoderContractError) as raised:
            encoder.train(ragged, self.channel_set, self.input_identity, SMALL_CONFIG)
        self.assertIn("no fixed shape to encode", str(raised.exception))


class TheEvidenceHooksAreEmittedAndDecideNothing(EncoderCase):
    @classmethod
    def setUpClass(cls):
        super().setUpClass()
        cls.encoder = TemporalConvolutionEncoder()
        cls.encoder.train(cls.windows, cls.channel_set, cls.input_identity, SMALL_CONFIG)
        cls.artifact_path = os.path.join(tempfile.mkdtemp(dir=cls.root), "encoder.pt")
        cls.saved = cls.encoder.save(cls.artifact_path)
        cls.embeddings = cls.encoder.encode(
            cls.windows, cls.channel_set, cls.input_identity
        )
        cls.measurement = measure_encoder(cls.saved, cls.embeddings)

    def test_every_required_identity_and_cost_is_present(self):
        record = self.measurement.to_dict()
        for field in (
            "encoder_kind", "artifact_identity", "channel_set_version",
            "training_input_identity", "embedding_dimension", "seed", "framework",
            "framework_version", "training_seconds", "p50_encode_ms", "p95_encode_ms",
            "artifact_bytes", "numerical_tolerance",
        ):
            self.assertIn(field, record)
            self.assertIsNotNone(record[field])

    def test_the_measured_costs_are_real_numbers(self):
        self.assertGreater(self.measurement.training_seconds, 0.0)
        self.assertGreater(self.measurement.p95_encode_ms, 0.0)
        self.assertGreaterEqual(
            self.measurement.p99_encode_ms, self.measurement.p50_encode_ms
        )
        self.assertGreater(self.measurement.artifact_bytes, 0)
        self.assertEqual(TRAINING_WINDOWS, self.measurement.encoded_windows)

    def test_the_metric_lift_input_is_the_embeddings_themselves(self):
        self.assertEqual(self.embeddings.embeddings, self.measurement.metric_lift_input)
        self.assertEqual(
            SMALL_CONFIG.embedding_dimension, len(self.measurement.metric_lift_input[0])
        )

    def test_the_record_carries_no_field_in_which_a_verdict_could_be_written(self):
        record = self.measurement.to_dict()
        for absent in (
            "deployable", "champion", "winner", "verdict", "promoted", "recommended",
            "beats_engineered_features",
        ):
            self.assertNotIn(absent, record)

    def test_the_embeddings_are_omitted_from_the_record_unless_asked_for(self):
        compact = self.measurement.to_dict()
        self.assertEqual([], compact["metric_lift_input"])
        self.assertEqual(TRAINING_WINDOWS, compact["metric_lift_input_windows"])
        full = self.measurement.to_dict(include_embeddings=True)
        self.assertEqual(TRAINING_WINDOWS, len(full["metric_lift_input"]))

    def test_the_environment_is_recorded_as_observed(self):
        environment = framework_environment()
        self.assertEqual("torch", environment["framework"])
        self.assertTrue(environment["framework_version"])
        self.assertEqual(1, environment["threads"])
        self.assertEqual(
            environment["framework_version"], self.saved.framework_version
        )

    def test_every_embedding_has_the_declared_fixed_dimension(self):
        for vector in self.embeddings.embeddings:
            self.assertEqual(SMALL_CONFIG.embedding_dimension, len(vector))


if __name__ == "__main__":
    unittest.main()
