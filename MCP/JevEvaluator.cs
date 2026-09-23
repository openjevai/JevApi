using System.Diagnostics;
using TypeSafe.Client;

namespace JevMcp;

/// <summary>Server-side limits, bound from the "Mcp" config section.</summary>
public sealed class McpServerOptions
{
    /// <summary>Hard cap on characters sent as a single state. States beyond this are truncated with a warning.</summary>
    public int MaxStateCharacters { get; set; } = 6000;

    /// <summary>Per-item content cap in batch evaluation.</summary>
    public int MaxItemCharacters { get; set; } = 4000;

    /// <summary>Maximum number of items accepted by evaluate_batch.</summary>
    public int MaxBatchItems { get; set; } = 50;

    /// <summary>Maximum number of questions per evaluate/evaluate_batch call.</summary>
    public int MaxQuestionsPerCall { get; set; } = 20;

    /// <summary>Max concurrent upstream calls inside one evaluate_batch.</summary>
    public int BatchParallelism { get; set; } = 5;
}

/// <summary>Runs Jev evaluations with truncation, error mapping, and latency capture. Stateless.</summary>
public sealed class JevEvaluator
{
    private readonly TypeSafeClient _client;
    private readonly McpServerOptions _options;
    private readonly TypeSafeClientOptions _clientOptions;

    public JevEvaluator(TypeSafeClient client, McpServerOptions options, TypeSafeClientOptions clientOptions)
    {
        _client = client;
        _options = options;
        _clientOptions = clientOptions;
    }

    public McpServerOptions Options => _options;

    /// <summary>True when a non-empty API key is configured (does not verify it against the API).</summary>
    public bool HasApiKey => !string.IsNullOrWhiteSpace(_clientOptions.ApiKey);

    /// <summary>The configured model name.</summary>
    public string Model => _clientOptions.Model;

    /// <summary>The configured API base address.</summary>
    public string BaseAddress => _clientOptions.BaseAddress.ToString();

    /// <summary>Truncates text to <paramref name="limit"/> characters; returns the (possibly shortened) text and a warning.</summary>
    public static (string Text, string? Warning) Truncate(string text, int limit)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= limit)
            return (text, null);
        return (text[..limit],
            $"content truncated from {text.Length} to {limit} characters; the judgment is based only on the retained prefix.");
    }

    /// <summary>Validates and maps one MCP question to a client question object.</summary>
    public object MapQuestion(McpQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        var type = question.Type?.Trim().ToLowerInvariant();
        return type switch
        {
            "noul" => new NoulQuestion
            {
                Instructions = Require(question.Statement, question.Id, "statement", "noul"),
            },
            "choice" => new ChoiceQuestion
            {
                Instructions = Require(question.Question, question.Id, "question", "choice"),
                Criteria = MapOptions(question),
            },
            "score" => new ScoreQuestion
            {
                Instructions = Require(question.Question, question.Id, "question", "score"),
                Criteria = MapLevels(question),
            },
            null or "" => throw Malformed(question.Id, "missing 'type'. Set type to \"choice\", \"score\", or \"noul\"."),
            _ => throw Malformed(question.Id, $"unknown type '{question.Type}'. Use \"choice\", \"score\", or \"noul\"."),
        };
    }

    /// <summary>Validates the shared structure of a question list (count, ids).</summary>
    public void ValidateQuestionList(IReadOnlyList<McpQuestion> questions)
    {
        if (questions.Count == 0)
            throw Malformed(null, "at least one question is required.");
        if (questions.Count > _options.MaxQuestionsPerCall)
            throw Malformed(null, $"too many questions ({questions.Count}); the limit is {_options.MaxQuestionsPerCall} per call. Split into multiple calls.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var q in questions)
        {
            if (string.IsNullOrWhiteSpace(q?.Id))
                throw Malformed(q?.Id, "every question needs a non-empty unique 'id' so results can be mapped back.");
            if (!seen.Add(q.Id))
                throw Malformed(q.Id, $"duplicate question id '{q.Id}'; ids must be unique within a call.");
        }
    }

    /// <summary>Evaluates one state against the question set; returns results in question order.</summary>
    public async Task<(List<QuestionResult> Results, EvaluationResult Raw, double LatencyMs)> EvaluateAsync(
        string state, IReadOnlyList<McpQuestion> questions, CancellationToken cancellationToken)
    {
        var mapped = questions.ToDictionary(q => q.Id, q => MapQuestion(q), StringComparer.Ordinal);

        var stopwatch = Stopwatch.StartNew();
        EvaluationResult raw = await _client.EvaluateAsync(state, mapped, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        var results = questions
            .Select(q => MapAnswer(q.Id, raw.Answers.TryGetValue(q.Id, out var a) ? a : null))
            .ToList();
        return (results, raw, stopwatch.Elapsed.TotalMilliseconds);
    }

    private static QuestionResult MapAnswer(string id, Answer? answer) => answer switch
    {
        NoulAnswer noul => new QuestionResult { Id = id, Type = "noul", Noul = noul.Noul },
        ChoiceAnswer choice => new QuestionResult
        {
            Id = id,
            Type = "choice",
            Choice = choice.Choice,
            Probabilities = new Dictionary<string, double>(choice.Probabilities, StringComparer.Ordinal),
            Confidence = choice.Confidence,
        },
        ScoreAnswer score => new QuestionResult
        {
            Id = id,
            Type = "score",
            Score = score.Score,
            Legend = new Dictionary<string, string>(score.Legend, StringComparer.Ordinal),
            Probabilities = new Dictionary<string, double>(score.Probabilities, StringComparer.Ordinal),
            Confidence = score.Confidence,
        },
        null => new QuestionResult { Id = id, Type = "unknown" },
        _ => new QuestionResult { Id = id, Type = answer.Type },
    };

    private Dictionary<string, object?> MapOptions(McpQuestion question)
    {
        var options = question.Options ?? [];
        if (options.Count is 0 or > 255)
            throw Malformed(question.Id, $"choice question needs between 1 and 255 'options'; got {options.Count}.");
        return options.ToDictionary<string, string, object?>(o => o, _ => null, StringComparer.Ordinal);
    }

    private static IReadOnlyList<object> MapLevels(McpQuestion question)
    {
        var levels = question.Levels ?? [];
        if (levels.Count is < 2 or > 10)
            throw Malformed(question.Id, $"score question needs between 2 and 10 'levels' (ordered low to high); got {levels.Count}.");
        return levels.Cast<object>().ToArray();
    }

    private static string Require(string? value, string id, string field, string type) =>
        string.IsNullOrWhiteSpace(value)
            ? throw Malformed(id, $"{type} question requires a non-empty '{field}'.")
            : value;

    /// <summary>Creates a structured malformed-question error the caller can fix.</summary>
    public static McpToolException Malformed(string? questionId, string detail) =>
        new(new McpError
        {
            Code = "malformed_question",
            Detail = (questionId is null ? string.Empty : $"Question '{questionId}': ") + detail,
            Retryable = false,
        });

    /// <summary>Creates a structured malformed-argument error (state/items problems).</summary>
    public static McpToolException BadArgument(string code, string detail) =>
        new(new McpError { Code = code, Detail = detail, Retryable = false });
}

/// <summary>Carries a structured <see cref="McpError"/> out of a tool call.</summary>
public sealed class McpToolException : Exception
{
    public McpError Error { get; }

    public McpToolException(McpError error)
        : base(error.Detail) => Error = error;
}
