"""Comparable metrics for the four supported outcome shapes.

Standard library only, deliberately. The mathematics here decides whether a
candidate is worth anything, so it is implemented where a known-answer fixture can
certify it rather than delegated to a package whose version would become part of the
answer.

TWO RULES THIS MODULE OBEYS.

An undefined metric is omitted, never reported as a number. A holdout containing one
class has no area under the curve. Emitting a placeholder would put a value into a
manifest that a reader would compare against a real one, and a not-a-number value
would additionally produce a document the .NET side cannot parse.

Discrimination and probability quality are reported separately and never merged.
Ranking every unit correctly while assigning badly scaled probabilities is a real and
common state, and a product whose output is a risk band a human acts on cannot treat
the two as interchangeable.
"""

from __future__ import annotations

import math
from dataclasses import dataclass
from typing import Any, Mapping, Sequence

#: Probabilities are clamped before a logarithm is taken. A single confident and
#: wrong prediction would otherwise send the score to infinity and make two models
#: incomparable on the strength of one row.
PROBABILITY_FLOOR = 1e-15


@dataclass(frozen=True)
class MetricSet:
    """Metric values plus the reason any expected metric is absent."""

    values: Mapping[str, float]
    undefined: Mapping[str, str]

    def to_dict(self) -> dict[str, Any]:
        return {"values": dict(self.values), "undefined": dict(self.undefined)}


def _clamp(p: float) -> float:
    return min(1.0 - PROBABILITY_FLOOR, max(PROBABILITY_FLOOR, float(p)))


def roc_auc(indicators: Sequence[int], scores: Sequence[float]) -> float | None:
    """Rank based area under the curve, with the tie correction applied.

    Industrial measurements are heavily tied because sensors quantise and setpoints
    repeat, and a constant model produces a complete tie. A tie-blind rank sum would
    reward or punish those ties arbitrarily; the midrank treatment gives a constant
    model exactly 0.5, which is the honest answer.

    Returns None when one class is absent, because the quantity is undefined.
    """
    positives = sum(1 for y in indicators if y == 1)
    negatives = len(indicators) - positives
    if positives == 0 or negatives == 0:
        return None

    order = sorted(range(len(scores)), key=lambda i: float(scores[i]))
    ranks = [0.0] * len(scores)
    i = 0
    while i < len(order):
        j = i
        while j + 1 < len(order) and float(scores[order[j + 1]]) == float(scores[order[i]]):
            j += 1
        midrank = (i + j) / 2.0 + 1.0
        for k in range(i, j + 1):
            ranks[order[k]] = midrank
        i = j + 1

    rank_sum = sum(ranks[i] for i, y in enumerate(indicators) if y == 1)
    return (rank_sum - positives * (positives + 1) / 2.0) / (positives * negatives)


def evaluate_classification(
    classes: Sequence[Any],
    labels: Sequence[Any],
    probabilities: Sequence[Sequence[float]],
    class_order: Sequence[Any] | None = None,
) -> MetricSet:
    """Metrics for a binary, multiclass or ordinal holdout.

    probabilities rows are aligned to classes. class_order, when supplied, declares
    the rank of an ordinal outcome and adds the rank error.
    """
    n = len(labels)
    values: dict[str, float] = {"n": float(n)}
    undefined: dict[str, str] = {}
    if n == 0:
        undefined["all"] = "The holdout is empty."
        return MetricSet(values, undefined)

    index_of = {c: i for i, c in enumerate(classes)}
    unknown = sorted({str(y) for y in labels if y not in index_of})
    if unknown:
        undefined["all"] = (
            "The holdout carries label value(s) the model never saw: "
            + ", ".join(unknown)
            + ". No metric is reported rather than one computed against a class the "
            "model could not have predicted."
        )
        return MetricSet(values, undefined)

    truth = [index_of[y] for y in labels]

    correct = 0
    log_loss_total = 0.0
    brier_total = 0.0
    for row, actual in zip(probabilities, truth):
        predicted = max(range(len(classes)), key=lambda c: row[c])
        if predicted == actual:
            correct += 1
        log_loss_total -= math.log(_clamp(row[actual]))
        brier_total += sum(
            (float(row[c]) - (1.0 if c == actual else 0.0)) ** 2 for c in range(len(classes))
        )

    values["accuracy"] = correct / n
    values["log_loss"] = log_loss_total / n

    if len(classes) == 2:
        # The conventional binary score, so a value here is comparable with any
        # externally published binary result.
        positive = 1
        indicators = [1 if a == positive else 0 for a in truth]
        scores = [float(row[positive]) for row in probabilities]
        values["prevalence"] = sum(indicators) / n
        values["brier"] = sum(
            (s - y) ** 2 for s, y in zip(scores, indicators)
        ) / n
        auc = roc_auc(indicators, scores)
        if auc is None:
            undefined["auc"] = (
                "The holdout carries one class only, so no ranking between a positive "
                "and a negative unit exists to be measured."
            )
        else:
            values["auc"] = auc
    else:
        values["brier"] = brier_total / n

    if class_order:
        rank_of = {c: i for i, c in enumerate(class_order)}
        missing = sorted({str(c) for c in classes if c not in rank_of})
        if missing:
            undefined["mean_absolute_rank_error"] = (
                "Class(es) " + ", ".join(missing) + " carry no declared rank."
            )
        else:
            total = 0.0
            for row, actual in zip(probabilities, truth):
                predicted = max(range(len(classes)), key=lambda c: row[c])
                total += abs(rank_of[classes[predicted]] - rank_of[classes[actual]])
            values["mean_absolute_rank_error"] = total / n

    return MetricSet(values, undefined)


def evaluate_continuous(
    labels: Sequence[float], predictions: Sequence[float]
) -> MetricSet:
    """Metrics for a continuous holdout."""
    n = len(labels)
    values: dict[str, float] = {"n": float(n)}
    undefined: dict[str, str] = {}
    if n == 0:
        undefined["all"] = "The holdout is empty."
        return MetricSet(values, undefined)

    truth = [float(y) for y in labels]
    guess = [float(p) for p in predictions]
    errors = [g - t for g, t in zip(guess, truth)]

    values["mae"] = sum(abs(e) for e in errors) / n
    values["rmse"] = math.sqrt(sum(e * e for e in errors) / n)

    mean = sum(truth) / n
    total_sum_of_squares = sum((t - mean) ** 2 for t in truth)
    if total_sum_of_squares == 0.0:
        undefined["r2"] = (
            "Every holdout value is identical, so there is no variance for a model to "
            "explain and the proportion explained is undefined."
        )
    else:
        residual = sum(e * e for e in errors)
        values["r2"] = 1.0 - residual / total_sum_of_squares

    return MetricSet(values, undefined)
