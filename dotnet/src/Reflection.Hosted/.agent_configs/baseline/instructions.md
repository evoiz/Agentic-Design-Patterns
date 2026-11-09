Evaluate the public Responses contract of chapter04-reflection, a stateless hosted
MAF reflection demo. Generate a balanced 15-case smoke dataset using explicit
product:, fact-check:, code-review: and csharp-review: prefixes (case-insensitive,
at the start of the request). Each row must include a query and mode-specific
expected behavior. Judge valid alternatives, not exact draft wording.

Every mode returns the same text envelope: Outcome: Accepted or MaxRoundsReached;
Rounds: N of 3; an acceptance or not-accepted statement; Draft: followed by the
latest artifact; Review: Accept or Revise; and Reasoning: with nonempty reviewer
explanation. Accepted must correspond to Accept. MaxRoundsReached must correspond
to Revise at round 3 and must not claim acceptance. Do not penalize the envelope as
extra commentary, demand a JSON envelope or require intermediate drafts. A valid
not-accepted result is allowed; do not score its draft as accepted quality.

product: supplies complete product facts. An accepted draft is at most 40 words,
ends with exactly "Made for the way you live.", reads clearly and appealingly,
and invents no attributes. Include distinct facts and explicit prohibited claims.
fact-check: supplies a stable educational subject. The draft is a short informative
paragraph and the review explains accuracy or needed corrections using model
knowledge only. There is no ACCURATE/INACCURATE status line, browsing, retrieval or
external verification. Do not demand citations or treat acceptance as proof of truth.
code-review: supplies the actual Python coding task, not a fixed task selected by
the host. Include factorial cases with calculate_factorial, a docstring, 0! = 1
and ValueError for negatives, plus a small different task with explicit requirements.
csharp-review: supplies a C# task. Include arithmetic mean cases with explicit
null/empty rejection, non-finite value rejection and XML documentation.
Judge code statically against that query's requirements; the agent does not execute it.

Do not require all requests to use three rounds, guaranteed improvement, tool
calls, timing, telemetry, execution proofs or persistent state. Include accepted
and budget-exhausted example envelopes when preparing reference responses.
This setup creates local artifacts only; do not execute evaluation runs or modify
the agent.
