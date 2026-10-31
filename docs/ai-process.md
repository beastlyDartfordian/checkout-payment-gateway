# AI-assisted development process

This assessment was developed with substantial assistance from OpenAI Codex. This document describes the collaboration as it happened, including where the candidate set direction and where AI carried out the work. It is a process account, not a claim that the candidate independently wrote every part of the solution.

## Direction and responsibility

Joshua chose the .NET version, provided the assessment and interview expectations, and asked for simple, maintainable code without over-engineering. He explicitly required a test-first workflow: write tests, run them and observe expected failures, then implement enough code to make them pass.

The initial intention was to work through the challenge together. We discussed the two main flows, validation, bank failures, and a proposed test order. Joshua agreed the retrieval contract: exact safe payment details, 404 for an unknown ID, no full card number/CVV in responses, and string suffixes preserving leading zeros.

After the retrieval red phase, Joshua asked Codex to complete the remaining work autonomously under those constraints and leave the decisions for his review once code-complete. Consequently, the later assumptions and implementation choices were made by Codex and documented for review; they were not individually approved by Joshua during development.

Codex authored the tests, application changes, container configuration, CI workflow, smoke script, and supporting documentation. It also executed local checks and published the repository at Joshua's request. Joshua remains responsible for reviewing the submission, deciding which choices to retain, and being able to explain the final implementation. At the time of this account (31 October 2026), the implementation had been handed over for that review.

## Work performed

1. **Read the challenge and establish a baseline.** Downloaded the official .NET starter and assessment instructions, inspected the supplied code and simulator, installed local prerequisites, restored dependencies, and ran the existing tests. The baseline had one passing test and one failing test: unknown-payment retrieval returned 204 instead of the expected 404.
2. **Agree the first contract and strengthen tests.** Replaced random retrieval fixtures with fixed data. Asserted actual JSON fields, authorized/declined statuses, safe response fields, missing IDs, and leading zeros. Ran the tests and observed failures before changing the API.
3. **Complete retrieval, then validation.** Fixed retrieval and suffix representation. Added invalid field/binding cases, malformed-body tests, a fixed clock, and assertions that rejected inputs never call the bank. Observed the missing POST endpoint failures before implementing validation.
4. **Implement payment processing through tests.** Added tests for bank payload mapping, authorized/declined results, Location headers, subsequent retrieval, valid boundaries, and upstream failures. Observed failures against the placeholder endpoint before adding the real bank integration. Added timeout/error mapping without automatic payment retries or invented declines.
5. **Test operational behaviour.** Added failing tests for liveness, rejection logging, and the OpenAPI response contract, then implemented those behaviours. Tested that logs and responses exclude sensitive submitted data. Updated vulnerable starter dependencies and removed an unused duplicate response model.
6. **Package and verify.** Added a non-root multi-stage Docker image, a separate Compose extension for the API, and CI. Ran tests, coverage, a warning-free Release build, formatting checks, a dependency audit, and smoke checks against the actual supplied bank simulator. Checked container identity and inspected gateway logs. Preserved the supplied simulator configuration, original Compose file, and `.editorconfig`.
7. **Publish for review.** Published the completed code to Joshua's public GitHub repository, verified anonymous access and the published commit, and waited for GitHub CI to pass. No recruiter submission or public service deployment was performed.

## How TDD was applied

Work proceeded in small behaviour batches, rather than writing the entire application before the tests. For each main implementation batch, Codex ran the new tests, checked that failures reflected missing or incorrect behaviour, implemented the change, and reran the suite. A test-code compilation error was corrected before counting that run as a behavioural red phase.

The [TDD record](tdd.md) contains the observed red/green counts. Not every later test began red: the final current-expiry-month regression case checked an existing rule and passed immediately. Tests were also adjusted when model types changed, while preserving their expected public JSON behaviour.

The implementation was committed after the suite was green, rather than committing each intermediate red and green state. Intermediate command logs remained outside the repository. The TDD record is therefore a summary of observed local execution, not a red/green sequence reconstructable solely from Git history.

## Verification and its limits

At initial handover, 74 tests passed, with 97.61% line coverage and 96.15% branch coverage. Local container/simulator checks and GitHub CI also passed. See [verification results](verification.md) for the commands and scope.

Automated tests use the real ASP.NET Core request pipeline with a controlled bank HTTP boundary. Real simulator checks provide a separate check of that integration. CI repeats build, formatting, test, and image-build checks on a clean Linux runner.

The same AI authored both tests and implementation. Passing tests and coverage are evidence of the behaviours asserted, not independent confirmation that every interpretation or design choice is correct. Candidate review and the interview discussion remain important, especially for the documented assumptions and production limitations.

## Decisions left visible for review

The [design notes](design.md) and README explain the choices made during autonomous implementation: a small controller/bank-client/repository structure; in-memory storage; GBP/USD/EUR; positive integer amounts; strictly future UTC expiry months; HTTP status mappings; a five-second bank timeout; no automatic retries; safe logging; and local hosting boundaries.

No raw chat transcript, recruitment emails, meeting links, or account credentials are included in this process document.
