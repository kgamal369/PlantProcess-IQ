# PlantProcess IQ — P5 OT-Safe Acquisition

## Closed by P5A

- OT-safe edge collector contract.
- One-way push batch gateway.
- Historian/time-series tag mapping into canonical parameter observations.
- Schema drift detection for added/removed/type/unit drift.
- Collector status UI proof: lag, buffer, network proof, credential rotation.
- SQL foundation for collectors, batches, buffer status, historian mappings and drift events.
- Hard tests for one-way proof, historian mapping and drift blocking.

## Security doctrine

PPIQ receives pushed batches.  
PPIQ does not open inbound connections to OT source networks.  
PPIQ does not provide control/write paths back to OT assets.