# Grounded Assistant - Demo Framing (V1-43)

## Status for the 8-Jul evaluation
The grounding contract and the GroundedAssistantPage are implemented and enforce the honesty
rules: every rendered number resolves to an evidence handle, and uncited figures are blocked
by GroundingService. Live model-provider wiring is being validated against the real
ppiq_assistant_provider_configs schema and is completed as a fast-follow.

## What to SAY in the room (verbatim, if the provider is not wired by demo time)
"The assistant is grounded: it can only cite numbers that the engines actually produced, and
it refuses to state any figure it cannot back with an evidence handle. Here is the grounding
contract and the result set it draws from. We are finalizing the model-provider connection
this sprint; the honesty gate you see is the hard part, and it is already in place."

## What to SHOW instead of a live answer
1. The correlation result set (13 findings) with the planted superheat driver on top,
   population + method + q-value visible - proof the numbers are real.
2. The grounding contract / evidence handles that any assistant answer must resolve against.
3. The no-egress toggle in the provider configuration (data-sovereignty story for the plant).

## Hard rule
Never demo an unverified live answer. A grounded refusal is a feature; a fabricated answer is
a credibility loss. If the provider is wired and the 25-item eval passes, demo it live;
otherwise use the framing above.
