# JevMcp — Jev evaluation MCP server

A stdio [MCP](https://modelcontextprotocol.io) server (built with [MCPSharp](https://github.com/afrise/MCPSharp)) that wraps the [TypeSafe.Client](../../TypeSafe.Client/README.md) library and exposes the Jev System One model to LLM agents.

Jev evaluates typed **questions** against a **state** and returns structured results with probability distributions and confidence. All tools are **idempotent and stateless** — every call is self-described and safe to retry, reorder, or parallelize.

## Setup

1. Set the API key in `appsettings.json` (copied to output) or via environment variables:

```json
{
  "TypeSafe": { "ApiKey": "ts-...", "Model": "jev-latest" },
  "Mcp": { "MaxStateCharacters": 6000, "MaxItemCharacters": 4000, "MaxBatchItems": 50, "MaxQuestionsPerCall": 20, "BatchParallelism": 5 }
}
```

Environment override: `TYPESAFE__ApiKey=ts-...`, `MCP__MaxBatchItems=100`, etc.

### OpenJEV (optional community gateway)

TypeSafe stays the default. To use [OpenJEV](https://openjev.sh) — a free community gateway to the same Jev model — instead, set `OPENJEV_API_KEY` (or `JEV_PROVIDER=openjev`); you can also add an `OpenJev` section to `appsettings.json`. The endpoint and model switch automatically; nothing else changes. See `OPENJEV.md`.

2. Point your MCP client at the built executable, e.g. in an MCP client config:

```json
{
  "mcpServers": {
	"jev-eval": { "command": "MCP/bin/Debug/net10.0/JevMcp.exe" }
  }
}
```

## Tools

| Tool | Use when | Key limits |
| --- | --- | --- |
| `evaluate(state, questions)` | Judge **one** item with 1–20 atomic questions (claim verification, routing, rubric rating) | state truncated at 6000 chars (warning returned) |
| `evaluate_batch(items, questions)` | Same judgment across **many** items — triage/pre-filtering of >~5 documents | ≤ 50 items/call, each truncated at 4000 chars |
| `validate_questions(questions)` | Dry-run phrasing checks (atomicity, required fields, duplicate ids) before spending an evaluation | free; no upstream call |
| `examples()` | Read canonical call patterns when unsure how to structure a call | free; no upstream call |

### Question shapes

```jsonc
// choice — pick one option, get full probabilities + confidence
{ "id": "category", "type": "choice", "question": "Which category fits best?", "options": ["a", "b", "c"] }
// score — rate along a rubric (2–10 levels, low→high), get score + confidence
{ "id": "relevance", "type": "score", "question": "How relevant is this to topic X?", "levels": ["irrelevant", "somewhat", "highly relevant"] }
// noul — is a statement true? get a raw 0–1 probability (never reduced to a boolean)
{ "id": "claim_ok", "type": "noul", "statement": "The source explicitly states that X." }
```

Questions may be mixed in one call (choice + score + noul in parallel at no extra latency). Keep each question **atomic** — one gut-check judgment; decompose multi-factor evaluations into several questions and combine the results yourself.

### Errors

Failures return `{ "error": { "code", "detail", "retryable" } }` with codes: `malformed_question`, `malformed_argument`, `state_too_large`, `unauthorized`, `rate_limited`, `overloaded`, `upstream_error`.

### Observability

Every successful response includes `request_id`, `usage` (input/output tokens), and `latency_ms` so callers can learn which question patterns are cheap vs. expensive.
