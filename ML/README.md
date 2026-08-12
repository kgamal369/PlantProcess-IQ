# PPIQ Python ML Runtime

The compute plane. Governance and decisions live in the .NET control plane.

```
Backend/   C# .NET      platform, orchestration, governance, gates, Layer A
ML/        Python       numerical learning and computation      <- this project
Frontend/  React        interaction and visualisation
```

## What this project does

It executes one job at a time, described by a versioned job specification written by
the .NET runner, and reports what happened in a structured result manifest.

```
C# job runner
   writes JobSpec.json, pointing at sealed artifacts
        |
        v
Python runtime
   reads the artifacts, computes, writes artifacts and ResultManifest.json
        |
        v
C# job runner
   validates the manifest and hashes, runs the gates, decides
```

## Three rules this project obeys

**The manifest is the only authority.** stdout and stderr are diagnostics. A process
that prints SUCCESS and writes no valid manifest has failed.

**This runtime never connects to a database.** It reads sealed artifacts whose hashes
the job spec declares. It is therefore unaffected by a change to the physical database
schema. A guard test fails the build if a database or network library is imported.

**This runtime never decides.** It reports the artifact, metrics, calibration, latency,
hashes, warnings and terminal state. Whether a model becomes production champion is a
.NET governance decision.

## Job outcome is not analysis outcome

Two different axes, never collapsed.

| Axis | Values |
|---|---|
| `outcome`, how the execution ended | succeeded, refused, failed, cancelled, timed_out |
| `analysis_terminal_state`, what the analysis concluded | the Layer B terminal states |

A job can **succeed** while the analysis it ran honestly refuses to produce a finding.
That is a correct and common result, not an error.

## Running the tests

The protocol layer needs no install and no third-party package.

```
cd ML
PYTHONPATH=src python -m unittest discover -s tests
```

## Layout

```
ML/
  pyproject.toml          package metadata, optional extras per model family
  requirements.lock       intentionally empty for the protocol layer
  src/ppiq_ml/
    runtime/
      protocol.py         protocol identity, job outcomes, refusal codes
      job_spec.py         the specification the .NET runner writes
      result_manifest.py  the structured result the .NET runner reads
      checkpoint.py       stage checkpointing and resume
      runner.py           the entry point
  tests/
```

Model families arrive with their own tasks and live under `src/ppiq_ml/models/`.
No model source is ever placed in `Backend/` or `tools/`.
