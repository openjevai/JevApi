using System.Text.Json;
using System.Text.Json.Serialization;

namespace TypeSafe.Client;

/// <summary>Wire format of the evaluation request body.</summary>
internal sealed record EvaluationRequest
{
    [JsonPropertyName("state")]
    public required JsonElement State { get; init; }

    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("questions")]
    public required IReadOnlyDictionary<string, QuestionDto> Questions { get; init; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EvaluationRequest))]
[JsonSerializable(typeof(EvaluationResult))]
[JsonSerializable(typeof(Answer))]
[JsonSerializable(typeof(NoulAnswer))]
[JsonSerializable(typeof(ChoiceAnswer))]
[JsonSerializable(typeof(ScoreAnswer))]
[JsonSerializable(typeof(Usage))]
internal partial class TypeSafeJsonContext : JsonSerializerContext
{
    internal static readonly JsonSerializerOptions DefaultOptions = new(new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        TypeInfoResolver = TypeSafeJsonContext.Default,
    });
}

internal static class QuestionMapper
{
    public static QuestionDto ToDto(NoulQuestion question) => new NoulQuestionDto
    {
        Instructions = ToElement(question.Instructions),
        Criteria = question.Criteria is null
            ? null
            : new NoulCriteriaDto
            {
                True = question.Criteria.True is null ? null : ToElement(question.Criteria.True),
                False = question.Criteria.False is null ? null : ToElement(question.Criteria.False),
            },
    };

    public static QuestionDto ToDto(ChoiceQuestion question) => new ChoiceQuestionDto
    {
        Instructions = ToElement(question.Instructions),
        Criteria = question.Criteria.ToDictionary(
            pair => pair.Key,
            pair => pair.Value is null ? (JsonElement?)null : ToElement(pair.Value)),
    };

    public static QuestionDto ToDto(ScoreQuestion question) => new ScoreQuestionDto
    {
        Instructions = ToElement(question.Instructions),
        Criteria = question.Criteria.Select(ToElement).ToArray(),
    };

    private static JsonElement ToElement(object value) =>
        value is JsonElement element
            ? element.Clone()
            : JsonSerializer.SerializeToElement(value, TypeSafeJsonContext.DefaultOptions);
}
