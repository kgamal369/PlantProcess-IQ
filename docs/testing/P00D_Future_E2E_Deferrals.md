# P00D Future E2E Deferrals

The P00 test register listed two future E2E journeys:

- inspection-to-generated-page
- assistant-conversation

They are intentionally not implemented as executable happy-path tests yet because the required product phases are not landed:

- P06 must first provide the inspection-job to generated-analysis-page lifecycle.
- P09 must first provide the assistant conversation API and evidence-cited response lifecycle.

P00D records them as deferred rather than creating fake passing tests. They must become executable Playwright journeys when the corresponding features exist.
