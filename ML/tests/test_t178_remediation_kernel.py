"""T-178: one deterministic baseline, and every rule flipped independently.

The baseline passes nine of nine checks and seven of seven conditions. Each test
below changes exactly one fact away from it, so the rule being aimed at is the only
thing that could have caused the verdict.
"""

import dataclasses
import unittest

from ppiq_ml.remediation import (
    ACTION_POSSIBILITY_CODES,
    CHECK_CODES,
    CONDITION_CODES,
    CausalEvidenceState,
    EligibilityState,
    ModelLifecycleState,
    PredictionState,
    RemediationContractError,
    RemediationFacts,
    SAFETY_CHECK_CODE,
    decide,
    evaluate_can_accept,
    evaluate_checks,
    evaluate_eligibility,
)


def baseline(**overrides) -> RemediationFacts:
    """Nine of nine and seven of seven. Every test bends one fact away from this."""
    facts = dict(
        parameter_is_controllable=True,
        remaining_stage_is_ahead_or_imminent=True,
        within_operating_limits=True,
        within_specification_limits=True,
        violates_forbidden_combination=False,
        safety_constraints_satisfied=True,
        historical_support_units=40,
        required_historical_support_units=12,
        survives_contextual_control=True,
        uncertainty_width=0.08,
        maximum_uncertainty_width=0.25,
        causal_evidence=CausalEvidenceState.SUPPORTED,
        conclusion_stable_under_sensitivity=True,
        actionable_deadline_elapsed=False,
        prediction_state=PredictionState.OPEN,
        safety_valid_on_recheck=True,
        producing_model_lifecycle=ModelLifecycleState.ACTIVE,
        tenant_entitled=True,
        caller_role_permits_decision=True,
    )
    facts.update(overrides)
    return RemediationFacts(**facts)


#: One override per check, each failing that check and no other.
SINGLE_CHECK_FAILURES = {
    "RM01": {"parameter_is_controllable": False},
    "RM02": {"remaining_stage_is_ahead_or_imminent": False},
    "RM03": {"within_operating_limits": False},
    "RM04": {"violates_forbidden_combination": True},
    "RM05": {"historical_support_units": 3},
    "RM06": {"survives_contextual_control": False},
    "RM07": {"uncertainty_width": 0.90},
    "RM08": {"causal_evidence": CausalEvidenceState.CONTRADICTED},
    "RM09": {"conclusion_stable_under_sensitivity": False},
}

#: One override per condition, each blocking that condition and no other.
SINGLE_CONDITION_BLOCKERS = {
    "CA1_ELIGIBILITY_ACTIONABLE": {"survives_contextual_control": False},
    "CA2_STAGE_STILL_AHEAD": {"remaining_stage_is_ahead_or_imminent": False},
    "CA3_DEADLINE_NOT_ELAPSED": {"actionable_deadline_elapsed": True},
    "CA4_PREDICTION_STILL_OPEN": {"prediction_state": PredictionState.SUPERSEDED},
    "CA5_SAFETY_VALID_ON_RECHECK": {"safety_valid_on_recheck": False},
    "CA6_MODEL_SERVING_LIFECYCLE": {
        "producing_model_lifecycle": ModelLifecycleState.UNDER_REVIEW
    },
    "CA7_ENTITLEMENT_AND_ROLE": {"caller_role_permits_decision": False},
}


class TheBaselinePassesEverything(unittest.TestCase):
    def test_all_nine_checks_pass_and_the_state_is_actionable(self):
        result = evaluate_eligibility(baseline())
        self.assertEqual(EligibilityState.ACTIONABLE, result.state)
        self.assertEqual((), result.failed_codes)
        self.assertEqual(9, len(result.checks))

    def test_all_seven_conditions_pass_and_the_decision_may_be_accepted(self):
        decision = decide(baseline())
        self.assertTrue(decision.authority.can_accept)
        self.assertEqual((), decision.authority.blocker_codes)
        self.assertEqual(7, len(decision.authority.conditions))

    def test_the_nine_codes_are_the_frozen_ones_in_the_frozen_order(self):
        self.assertEqual(
            ("RM01", "RM02", "RM03", "RM04", "RM05", "RM06", "RM07", "RM08", "RM09"),
            CHECK_CODES,
        )
        self.assertEqual(CHECK_CODES, tuple(c.code for c in evaluate_checks(baseline())))

    def test_every_check_carries_a_name_and_a_sentence_whether_it_passed_or_not(self):
        for outcome in evaluate_checks(baseline()):
            self.assertTrue(outcome.name.strip())
            self.assertTrue(outcome.reason.strip())
            self.assertTrue(outcome.detail)


class EachCheckFailsByItsOwnName(unittest.TestCase):
    """One deterministic case fails each check by name, and only that check."""

    def test_each_of_the_nine_can_be_failed_independently(self):
        for code, override in SINGLE_CHECK_FAILURES.items():
            result = evaluate_eligibility(baseline(**override))
            self.assertEqual(
                (code,), result.failed_codes, f"{code} did not fail alone: {result.failed_codes}"
            )

    def test_every_one_of_the_nine_is_covered_by_a_case(self):
        self.assertEqual(set(CHECK_CODES), set(SINGLE_CHECK_FAILURES))

    def test_each_failure_carries_the_sentence_belonging_to_that_check(self):
        expected = {
            "RM01": "cannot be changed by an operator",
            "RM02": "already passed",
            "RM03": "operating limits",
            "RM04": "forbidden combination",
            "RM05": "not done this often enough",
            "RM06": "disappears once the declared context",
            "RM07": "cannot be told apart from no effect",
            "RM08": "did not do better than comparable units",
            "RM09": "reverses under the declared sensitivity",
        }
        for code, override in SINGLE_CHECK_FAILURES.items():
            result = evaluate_eligibility(baseline(**override))
            self.assertIn(expected[code], result.failed_checks[0].reason, code)

    def test_the_limits_check_names_which_limit_was_broken(self):
        operating = evaluate_eligibility(baseline(within_operating_limits=False))
        specification = evaluate_eligibility(baseline(within_specification_limits=False))
        self.assertIn("its operating limits", operating.failed_checks[0].reason)
        self.assertNotIn("its specification limits", operating.failed_checks[0].reason)
        self.assertIn("its specification limits", specification.failed_checks[0].reason)

    def test_absent_causal_evidence_does_not_fail_the_causal_check(self):
        """A method gap is never reported as a property of the customer's process."""
        result = evaluate_eligibility(
            baseline(causal_evidence=CausalEvidenceState.NOT_AVAILABLE)
        )
        self.assertEqual(EligibilityState.ACTIONABLE, result.state)
        causal = [c for c in result.checks if c.code == "RM08"][0]
        self.assertTrue(causal.passed)
        self.assertIn("does not refuse", causal.reason)

    def test_historical_support_reports_both_numbers(self):
        result = evaluate_eligibility(
            baseline(historical_support_units=3, required_historical_support_units=12)
        )
        self.assertIn("Only 3 comparable unit(s)", result.failed_checks[0].reason)
        self.assertIn("against 12 required", result.failed_checks[0].reason)


class SafetySuppressionWinsOverEverything(unittest.TestCase):
    def test_a_forbidden_combination_suppresses(self):
        result = evaluate_eligibility(baseline(violates_forbidden_combination=True))
        self.assertEqual(EligibilityState.SUPPRESSED, result.state)

    def test_a_broken_safety_constraint_suppresses(self):
        result = evaluate_eligibility(baseline(safety_constraints_satisfied=False))
        self.assertEqual(EligibilityState.SUPPRESSED, result.state)

    def test_safety_suppresses_even_when_every_other_check_passes(self):
        result = evaluate_eligibility(baseline(violates_forbidden_combination=True))
        self.assertEqual((SAFETY_CHECK_CODE,), result.failed_codes)
        self.assertEqual(EligibilityState.SUPPRESSED, result.state)

    def test_safety_suppresses_even_when_every_other_check_also_fails(self):
        result = evaluate_eligibility(
            baseline(
                violates_forbidden_combination=True,
                parameter_is_controllable=False,
                remaining_stage_is_ahead_or_imminent=False,
                within_operating_limits=False,
                historical_support_units=0,
                survives_contextual_control=False,
                uncertainty_width=9.0,
                causal_evidence=CausalEvidenceState.CONTRADICTED,
                conclusion_stable_under_sensitivity=False,
            )
        )
        self.assertEqual(EligibilityState.SUPPRESSED, result.state)
        self.assertEqual(9, len(result.failed_codes))

    def test_the_suppression_sentence_says_that_nothing_can_raise_it(self):
        result = evaluate_eligibility(baseline(safety_constraints_satisfied=False))
        self.assertIn("takes precedence over every softer classification", result.reason)

    def test_no_other_check_failing_alone_ever_suppresses(self):
        for code, override in SINGLE_CHECK_FAILURES.items():
            if code == SAFETY_CHECK_CODE:
                continue
            result = evaluate_eligibility(baseline(**override))
            self.assertNotEqual(EligibilityState.SUPPRESSED, result.state, code)


class TheFrozenCombinationsProduceTheFrozenClasses(unittest.TestCase):
    def test_all_nine_pass_gives_actionable(self):
        self.assertEqual(EligibilityState.ACTIONABLE, evaluate_eligibility(baseline()).state)

    def test_five_to_nine_pass_with_one_of_one_to_four_failing_gives_evidence_only(self):
        for code in ("RM01", "RM02", "RM03"):
            result = evaluate_eligibility(baseline(**SINGLE_CHECK_FAILURES[code]))
            self.assertEqual(EligibilityState.EVIDENCE_ONLY, result.state, code)

    def test_one_to_six_pass_with_seven_or_eight_failing_gives_exploratory(self):
        for code in ("RM07", "RM08"):
            result = evaluate_eligibility(baseline(**SINGLE_CHECK_FAILURES[code]))
            self.assertEqual(EligibilityState.EXPLORATORY, result.state, code)

    def test_several_possibility_checks_failing_together_still_gives_evidence_only(self):
        result = evaluate_eligibility(
            baseline(parameter_is_controllable=False, within_operating_limits=False)
        )
        self.assertEqual(EligibilityState.EVIDENCE_ONLY, result.state)
        self.assertEqual(("RM01", "RM03"), result.failed_codes)

    def test_a_mixed_failure_is_exploratory_and_not_evidence_only(self):
        """A finding that is not sound is not offered as evidence.

        However impossible acting may be, an evidence check that also failed means
        there is no sound finding left to present.
        """
        result = evaluate_eligibility(
            baseline(parameter_is_controllable=False, uncertainty_width=9.0)
        )
        self.assertEqual(EligibilityState.EXPLORATORY, result.state)
        self.assertEqual(("RM01", "RM07"), result.failed_codes)
        self.assertIn("is not sound is not offered as evidence", result.reason)

    def test_any_evidence_check_failing_alone_is_exploratory(self):
        for code in ("RM05", "RM06", "RM09"):
            result = evaluate_eligibility(baseline(**SINGLE_CHECK_FAILURES[code]))
            self.assertEqual(EligibilityState.EXPLORATORY, result.state, code)

    def test_the_ruled_precedence_cases_exactly(self):
        """The canonical rows, asserted one for one as ruled."""
        expected = (
            (("RM01",), EligibilityState.EVIDENCE_ONLY),
            (("RM02",), EligibilityState.EVIDENCE_ONLY),
            (("RM03",), EligibilityState.EVIDENCE_ONLY),
            (("RM05",), EligibilityState.EXPLORATORY),
            (("RM09",), EligibilityState.EXPLORATORY),
            (("RM01", "RM05"), EligibilityState.EXPLORATORY),
            (("RM03", "RM09"), EligibilityState.EXPLORATORY),
            (("RM04", "RM07"), EligibilityState.SUPPRESSED),
            ((), EligibilityState.ACTIONABLE),
        )
        for codes, state in expected:
            override = {}
            for code in codes:
                override.update(SINGLE_CHECK_FAILURES[code])
            self.assertEqual(
                state, evaluate_eligibility(baseline(**override)).state, str(codes)
            )

    def test_every_combination_of_nine_checks_matches_the_canonical_precedence(self):
        """All 512 combinations, each checked against the ruled rule directly.

        The rule is restated here independently of the implementation, so the test
        would fail if the implementation drifted toward any broader or narrower one.
        """
        import itertools

        possibility = ("RM01", "RM02", "RM03")
        strength = ("RM05", "RM06", "RM07", "RM08", "RM09")
        seen = set()
        for flips in itertools.product((False, True), repeat=9):
            failing = [
                code
                for code, flip in zip(
                    ("RM01", "RM02", "RM03", "RM04") + strength, flips
                )
                if flip
            ]
            override = {}
            for code in failing:
                override.update(SINGLE_CHECK_FAILURES[code])
            state = evaluate_eligibility(baseline(**override)).state

            if "RM04" in failing:
                expected = EligibilityState.SUPPRESSED
            elif not failing:
                expected = EligibilityState.ACTIONABLE
            elif not [c for c in failing if c in strength] and [
                c for c in failing if c in possibility
            ]:
                expected = EligibilityState.EVIDENCE_ONLY
            else:
                expected = EligibilityState.EXPLORATORY

            self.assertEqual(expected, state, str(failing))
            seen.add(state)
        self.assertEqual(512, 2 ** 9)
        self.assertEqual(set(EligibilityState), seen)

    def test_every_combination_of_nine_checks_lands_in_one_of_the_four_states(self):
        """The classification is total. There is nowhere else for a case to go."""
        import itertools

        seen = set()
        for flips in itertools.product((False, True), repeat=4):
            overrides = {}
            for flip, code in zip(flips, ("RM01", "RM02", "RM03", "RM04")):
                if flip:
                    overrides.update(SINGLE_CHECK_FAILURES[code])
            for strength in itertools.product((False, True), repeat=5):
                case = dict(overrides)
                for flip, code in zip(strength, ("RM05", "RM06", "RM07", "RM08", "RM09")):
                    if flip:
                        case.update(SINGLE_CHECK_FAILURES[code])
                state = evaluate_eligibility(baseline(**case)).state
                self.assertIn(state, list(EligibilityState))
                seen.add(state)
        self.assertEqual(set(EligibilityState), seen)


class TheAuthorityIsNotReducibleToEligibility(unittest.TestCase):
    def test_each_of_the_seven_can_be_blocked_independently(self):
        for code, override in SINGLE_CONDITION_BLOCKERS.items():
            decision = decide(baseline(**override))
            self.assertFalse(decision.authority.can_accept, code)
            self.assertIn(code, decision.authority.blocker_codes, code)

    def test_every_one_of_the_seven_is_covered_by_a_case(self):
        self.assertEqual(set(CONDITION_CODES), set(SINGLE_CONDITION_BLOCKERS))

    def test_five_of_the_seven_block_while_eligibility_stays_actionable(self):
        """The proof that the authority carries fields eligibility does not.

        The deadline, the prediction state, the safety re-check, the model lifecycle
        and the caller's entitlement all refuse a decision that eligibility calls
        actionable. An authority derived from eligibility could not see any of them.
        """
        for code in (
            "CA3_DEADLINE_NOT_ELAPSED",
            "CA4_PREDICTION_STILL_OPEN",
            "CA5_SAFETY_VALID_ON_RECHECK",
            "CA6_MODEL_SERVING_LIFECYCLE",
            "CA7_ENTITLEMENT_AND_ROLE",
        ):
            decision = decide(baseline(**SINGLE_CONDITION_BLOCKERS[code]))
            self.assertEqual(EligibilityState.ACTIONABLE, decision.eligibility.state, code)
            self.assertFalse(decision.authority.can_accept, code)
            self.assertEqual((code,), decision.authority.blocker_codes, code)

    def test_acceptance_is_impossible_on_any_non_actionable_eligibility(self):
        for state_override in (
            SINGLE_CHECK_FAILURES["RM01"],
            SINGLE_CHECK_FAILURES["RM04"],
            SINGLE_CHECK_FAILURES["RM07"],
            SINGLE_CHECK_FAILURES["RM05"],
        ):
            decision = decide(baseline(**state_override))
            self.assertNotEqual(EligibilityState.ACTIONABLE, decision.eligibility.state)
            self.assertFalse(decision.authority.can_accept)
            self.assertIn("CA1_ELIGIBILITY_ACTIONABLE", decision.authority.blocker_codes)

    def test_a_suppressed_suggestion_can_never_be_accepted(self):
        decision = decide(baseline(violates_forbidden_combination=True))
        self.assertEqual(EligibilityState.SUPPRESSED, decision.eligibility.state)
        self.assertFalse(decision.authority.can_accept)

    def test_no_single_condition_determines_the_answer_on_its_own(self):
        """Each condition is necessary and none is sufficient."""
        for code, override in SINGLE_CONDITION_BLOCKERS.items():
            self.assertFalse(decide(baseline(**override)).authority.can_accept, code)

        only_one_true = baseline(
            actionable_deadline_elapsed=True,
            prediction_state=PredictionState.DECIDED,
            safety_valid_on_recheck=False,
            producing_model_lifecycle=ModelLifecycleState.RETIRED,
            tenant_entitled=False,
        )
        self.assertFalse(decide(only_one_true).authority.can_accept)

    def test_all_seven_are_evaluated_even_after_one_has_blocked(self):
        decision = decide(
            baseline(
                actionable_deadline_elapsed=True,
                prediction_state=PredictionState.SUPERSEDED,
                tenant_entitled=False,
            )
        )
        self.assertEqual(7, len(decision.authority.conditions))
        self.assertEqual(3, len(decision.authority.blocker_codes))

    def test_both_lifecycle_states_that_may_not_serve_are_refused(self):
        for lifecycle in (ModelLifecycleState.UNDER_REVIEW, ModelLifecycleState.RETIRED):
            decision = decide(baseline(producing_model_lifecycle=lifecycle))
            self.assertFalse(decision.authority.can_accept, lifecycle.value)

    def test_both_ways_a_prediction_stops_being_open_are_refused(self):
        for state in (PredictionState.SUPERSEDED, PredictionState.DECIDED):
            decision = decide(baseline(prediction_state=state))
            self.assertFalse(decision.authority.can_accept, state.value)

    def test_either_half_of_the_entitlement_condition_refuses(self):
        for override in ({"tenant_entitled": False}, {"caller_role_permits_decision": False}):
            decision = decide(baseline(**override))
            self.assertFalse(decision.authority.can_accept)
            self.assertIn("CA7_ENTITLEMENT_AND_ROLE", decision.authority.blocker_codes)


class TheDecisionIsDeterministic(unittest.TestCase):
    def test_the_same_facts_produce_the_same_decision_document(self):
        for override in list(SINGLE_CHECK_FAILURES.values()) + [{}]:
            first = decide(baseline(**override)).to_dict()
            second = decide(baseline(**override)).to_dict()
            self.assertEqual(first, second)

    def test_failed_checks_are_reported_in_the_frozen_code_order(self):
        result = evaluate_eligibility(
            baseline(
                conclusion_stable_under_sensitivity=False,
                parameter_is_controllable=False,
                uncertainty_width=9.0,
            )
        )
        self.assertEqual(("RM01", "RM07", "RM09"), result.failed_codes)

    def test_blockers_are_reported_in_the_condition_order(self):
        decision = decide(
            baseline(
                caller_role_permits_decision=False,
                actionable_deadline_elapsed=True,
                prediction_state=PredictionState.DECIDED,
            )
        )
        self.assertEqual(
            (
                "CA3_DEADLINE_NOT_ELAPSED",
                "CA4_PREDICTION_STILL_OPEN",
                "CA7_ENTITLEMENT_AND_ROLE",
            ),
            decision.authority.blocker_codes,
        )

    def test_the_authority_can_be_evaluated_against_a_supplied_state(self):
        facts = baseline()
        self.assertTrue(evaluate_can_accept(facts, EligibilityState.ACTIONABLE).can_accept)
        for state in (
            EligibilityState.EVIDENCE_ONLY,
            EligibilityState.EXPLORATORY,
            EligibilityState.SUPPRESSED,
        ):
            self.assertFalse(evaluate_can_accept(facts, state).can_accept, state.value)


class TheFactsContractRefusesImpossibleInput(unittest.TestCase):
    def test_negative_counts_and_widths_are_refused(self):
        for override in (
            {"historical_support_units": -1},
            {"required_historical_support_units": -1},
            {"uncertainty_width": -0.1},
            {"maximum_uncertainty_width": -0.1},
        ):
            with self.assertRaises(RemediationContractError):
                baseline(**override)

    def test_every_fact_the_kernel_reads_is_declared_by_the_caller(self):
        """No fact defaults to a passing value, so no check can pass by silence."""
        fields = {f.name for f in dataclasses.fields(RemediationFacts)}
        self.assertEqual(19, len(fields))
        for field in dataclasses.fields(RemediationFacts):
            self.assertIs(dataclasses.MISSING, field.default, field.name)
            self.assertIs(dataclasses.MISSING, field.default_factory, field.name)

    def test_the_possibility_and_strength_groups_partition_the_nine(self):
        self.assertEqual(
            set(CHECK_CODES), set(ACTION_POSSIBILITY_CODES) | set(__import__(
                "ppiq_ml.remediation", fromlist=["EVIDENCE_STRENGTH_CODES"]
            ).EVIDENCE_STRENGTH_CODES)
        )
        self.assertEqual(
            set(),
            set(ACTION_POSSIBILITY_CODES) & set(__import__(
                "ppiq_ml.remediation", fromlist=["EVIDENCE_STRENGTH_CODES"]
            ).EVIDENCE_STRENGTH_CODES),
        )


if __name__ == "__main__":
    unittest.main()
