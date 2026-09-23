# TypeSafe.Client

A user-friendly .NET client library for the [TypeSafe AI](https://docs.typesafe.ai/api) evaluation endpoint (`POST https://api.typesafe.ai/v1/systemone`).

Evaluate a `state` against a map of typed questions and get back structured answers — one per question.

## Installation

Reference the `TypeSafe.Client` project (or package) and register it:

```csharp
// Option 1: configuration (expects a "TypeSafe" section with an "ApiKey")
builder.Services.AddTypeSafeClient(builder.Configuration.GetSection("TypeSafe"));

// Option 2: inline options
builder.Services.AddTypeSafeClient(options =>
{
	options.ApiKey = "ts-...";
	options.Model = TypeSafeModels.JevLatest;   // default
	options.MaxRetries = 3;                     // retries for 429/529 (default)
});

// Option 3: no DI
var client = new TypeSafeClient("ts-...");
```

## Quick start

### Ask a yes/no question (Noul)

```csharp
NoulAnswer answer = await client.AskAsync(
	"Does this convey urgency?",
	"Help! My payouts have been failing for 3 days.");

if (answer.IsYes()) { /* ... */ }
```

### Pick one option (Choice)

```csharp
ChoiceAnswer department = await client.ChooseAsync(
	"Which team should handle this?",
	"Help! My payouts have been failing for 3 days.",
	new Dictionary<string, object?>
	{
		["billing"] = "Payments, invoicing, refunds",
		["technical"] = "Bugs, outages, integrations",
		["sales"] = "Pricing, upgrades, new accounts",
	});

Console.WriteLine($"{department.Choice} ({department.Confidence:P0})");
```

### Rate along a rubric (Score)

```csharp
ScoreAnswer frustration = await client.RateAsync(
	"How frustrated is the customer?",
	"Help! My payouts have been failing for 3 days.",
	"Calm", "Frustrated", "Very angry");

Console.WriteLine($"{frustration.Score:F2} ≈ {frustration.NearestLevelDescription}");
```

### Batch multiple typed questions

```csharp
EvaluationResult result = await client.EvaluateAsync(
	"Help! My payouts have been failing for 3 days.",
	questions => questions
		.AddNoul("is_urgent", "Does this convey urgency?",
			trueCriteria: "Explicitly time-sensitive",
			falseCriteria: "No urgency expressed")
		.AddChoice("department", "Which team should handle this?",
			new Dictionary<string, object?>
			{
				["billing"] = "Payments, invoicing, refunds",
				["technical"] = "Bugs, outages, integrations",
				["sales"] = "Pricing, upgrades, new accounts",
			})
		.AddScore("frustration", "How frustrated is the customer?",
			"Calm", "Frustrated", "Very angry"));

NoulAnswer urgent = result.GetAnswer<NoulAnswer>("is_urgent")!;
ChoiceAnswer team = result.GetAnswer<ChoiceAnswer>("department")!;
ScoreAnswer mood = result.GetAnswer<ScoreAnswer>("frustration")!;
Console.WriteLine($"Tokens: {result.Usage.InputTokens} in / {result.Usage.OutputTokens} out");
```

### Structured state and instructions

`state`, `instructions`, and criteria values accept strings, objects, or arrays — pass any serializable value:

```csharp
var state = new[]
{
	new { role = "user", text = "Help! My payouts have been failing for 3 days." },
};

var instructions = new
{
	potential_duplicate = new { name = "John Smith", last_employer = "Google" },
	question = "Is the resume for the same person as `potential_duplicate`?",
};
```

## Errors

Failures throw `TypeSafeApiException` with helpers for the documented status codes:

- `IsUnauthorized` — 401, missing/invalid API key
- `IsValidationError` — 422, request body failed validation
- `IsRateLimited` — 429, rate limit exceeded (retried automatically by default)
- `IsOverloaded` — 529, TypeSafe temporarily overloaded (retried automatically by default)

429 and 529 responses are retried with exponential backoff (honoring `Retry-After`) up to `TypeSafeClientOptions.MaxRetries` times.

## API summary

| Method | Question type | Answer |
| --- | --- | --- |
| `AskAsync` | noul | `NoulAnswer` (`Noul` 0–1, `IsYes()`) |
| `ChooseAsync` | choice | `ChoiceAnswer` (`Choice`, `Probabilities`, `Confidence`) |
| `RateAsync` | score | `ScoreAnswer` (`Score`, `Legend`, `Probabilities`, `Confidence`, `NearestLevel`) |
| `EvaluateAsync` | mixed | `EvaluationResult` (`Answers`, `Usage`, `Model`) |
