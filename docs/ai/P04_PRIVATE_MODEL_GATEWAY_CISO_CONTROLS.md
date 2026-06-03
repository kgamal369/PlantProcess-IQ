# PlantProcess IQ — P04 Private Model Gateway / CISO Controls

## What this pack proves

- Plant/customer data is not allowed to use public AI egress.
- Private endpoint profiles are supported:
  - Azure OpenAI Private Link
  - AWS Bedrock VPC endpoint
  - Customer BYOM endpoint
  - Local air-gapped mock model
- Zero-data-retention confirmation is required.
- Customer key/secret reference is required for non-local private/BYOM endpoint.
- Raw prompt/response are not stored by the certification endpoint; hashes and redaction summary are stored.
- Assistant governance records retrieved handles, citations, grounded/abstained status, model version, and retention policy.
- Golden eval harness validates grounding, abstention, public-endpoint rejection, and model-version pinning.

## Production certification boundary

This pack closes product-side CISO controls and runtime proof. A real customer deployment must still validate the actual private network path, cloud tenant policy, ZDR contract settings, and key vault integration.