namespace TypeSafe.Client;

/// <summary>
/// Builds the map of typed questions sent to the TypeSafe evaluation endpoint.
/// Answers come back under the same keys used here.
/// </summary>
public sealed class EvaluationRequestBuilder
{
    private readonly Dictionary<string, object> _questions = new(StringComparer.Ordinal);
    private readonly object _state;

    internal EvaluationRequestBuilder(object state) => _state = state;

    /// <summary>Adds a yes/no question. The answer is a <see cref="NoulAnswer"/> with the probability of yes.</summary>
    /// <param name="id">A key you choose; the answer is returned under this id.</param>
    /// <param name="instructions">The yes/no question to evaluate (string, object, or array).</param>
    /// <param name="trueCriteria">Optional description of what a yes means.</param>
    /// <param name="falseCriteria">Optional description of what a no means.</param>
    public EvaluationRequestBuilder AddNoul(string id, object instructions, object? trueCriteria = null, object? falseCriteria = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(instructions);
        _questions[id] = new NoulQuestion
        {
            Instructions = instructions,
            Criteria = (trueCriteria, falseCriteria) is (null, null)
                ? null
                : new NoulCriteria { True = trueCriteria, False = falseCriteria },
        };
        return this;
    }

    /// <summary>Adds a question that picks one option from a set you define.</summary>
    /// <param name="id">A key you choose; the answer is returned under this id.</param>
    /// <param name="instructions">What the model should decide (string, object, or array).</param>
    /// <param name="options">A map of option to rubric description; use null when an option needs no extra detail. Maximum of 255 options.</param>
    public EvaluationRequestBuilder AddChoice(string id, object instructions, IReadOnlyDictionary<string, object?> options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Count is 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(options), "A choice question requires between 1 and 255 options.");
        _questions[id] = new ChoiceQuestion { Instructions = instructions, Criteria = options };
        return this;
    }

    /// <summary>Adds a question that picks one option from a set of simple options with no extra descriptions.</summary>
    public EvaluationRequestBuilder AddChoice(string id, object instructions, params string[] options)
        => AddChoice(id, instructions, options.ToDictionary<string, string, object?>(option => option, _ => null));

    /// <summary>Adds a question that rates the state along an ordered rubric (2 to 10 levels).</summary>
    /// <param name="id">A key you choose; the answer is returned under this id.</param>
    /// <param name="instructions">What the model should rate (string, object, or array).</param>
    /// <param name="levels">Ordered level descriptions, from lowest to highest.</param>
    public EvaluationRequestBuilder AddScore(string id, object instructions, IReadOnlyList<object> levels)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(levels);
        if (levels.Count is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(levels), "A score question requires between 2 and 10 levels.");
        _questions[id] = new ScoreQuestion { Instructions = instructions, Criteria = levels };
        return this;
    }

    /// <summary>Adds a question that rates the state along an ordered rubric (2 to 10 levels).</summary>
    public EvaluationRequestBuilder AddScore(string id, object instructions, params object[] levels)
        => AddScore(id, instructions, (IReadOnlyList<object>)levels);

    internal IReadOnlyDictionary<string, object> Questions => _questions;

    internal object State => _state;
}
