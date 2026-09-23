using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSafe.Client;

/// <summary>A yes/no question. Returns the probability the answer is yes.</summary>
public sealed record NoulQuestion
{
    /// <summary>The yes/no question to evaluate. May be a string, object, or array.</summary>
    public required object Instructions { get; init; }

    /// <summary>Optional descriptions of what a yes and a no mean.</summary>
    public NoulCriteria? Criteria { get; init; }
}

/// <summary>Optional descriptions of what a yes (<see cref="True"/>) and a no (<see cref="False"/>) mean.</summary>
public sealed record NoulCriteria
{
    /// <summary>What a yes (value near 1) means.</summary>
    public object? True { get; init; }

    /// <summary>What a no (value near 0) means.</summary>
    public object? False { get; init; }
}

/// <summary>Picks one option from a set you define. Returns the chosen option and the full probability distribution.</summary>
public sealed record ChoiceQuestion
{
    /// <summary>What the model should decide. May be a string, object, or array.</summary>
    public required object Instructions { get; init; }

    /// <summary>A map of option to rubric description; use null when an option needs no extra detail. Maximum of 255 options.</summary>
    public required IReadOnlyDictionary<string, object?> Criteria { get; init; }
}

/// <summary>Rates the state along a rubric you define. Returns a probability-weighted value across your levels.</summary>
public sealed record ScoreQuestion
{
    /// <summary>What the model should rate. May be a string, object, or array.</summary>
    public required object Instructions { get; init; }

    /// <summary>An ordered array of level descriptions (2 to 10 levels).</summary>
    public required IReadOnlyList<object> Criteria { get; init; }
}

// Wire format

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NoulQuestionDto), "noul")]
[JsonDerivedType(typeof(ChoiceQuestionDto), "choice")]
[JsonDerivedType(typeof(ScoreQuestionDto), "score")]
internal abstract record QuestionDto
{
    [JsonPropertyName("instructions")]
    public required JsonElement Instructions { get; init; }
}

internal sealed record NoulQuestionDto : QuestionDto
{
    [JsonPropertyName("criteria")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NoulCriteriaDto? Criteria { get; init; }
}

internal sealed record NoulCriteriaDto
{
    [JsonPropertyName("true")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? True { get; init; }

    [JsonPropertyName("false")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? False { get; init; }
}

internal sealed record ChoiceQuestionDto : QuestionDto
{
    [JsonPropertyName("criteria")]
    public required IReadOnlyDictionary<string, JsonElement?> Criteria { get; init; }
}

internal sealed record ScoreQuestionDto : QuestionDto
{
    [JsonPropertyName("criteria")]
    public required IReadOnlyList<JsonElement> Criteria { get; init; }
}
