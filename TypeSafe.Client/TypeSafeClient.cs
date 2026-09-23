using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace TypeSafe.Client;

/// <summary>
/// Client for the TypeSafe AI evaluation endpoint. Evaluate a state against a map of typed
/// questions and get back structured answers, one per question.
/// </summary>
public sealed class TypeSafeClient
{
    private const string EvaluationPath = "v1/systemone";

    private readonly HttpClient _httpClient;
    private readonly TypeSafeClientOptions _options;

    /// <summary>Creates a client over an existing <see cref="HttpClient"/>.</summary>
    public TypeSafeClient(HttpClient httpClient, IOptions<TypeSafeClientOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options.Value;
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException($"An API key is required. Set {nameof(TypeSafeClientOptions)}.{nameof(TypeSafeClientOptions.ApiKey)}.");
    }

    /// <summary>Creates a client with the given API key and default settings.</summary>
    public TypeSafeClient(string apiKey)
        : this(new HttpClient(), Microsoft.Extensions.Options.Options.Create(new TypeSafeClientOptions { ApiKey = apiKey }))
    {
    }

    /// <summary>
    /// Evaluates a state against a map of typed questions. Each entry in <paramref name="questions"/>
    /// is a <see cref="NoulQuestion"/>, <see cref="ChoiceQuestion"/>, or <see cref="ScoreQuestion"/>;
    /// answers come back under the same keys.
    /// </summary>
    /// <param name="state">The content to evaluate: a plain string or structured data (object/array).</param>
    /// <param name="questions">A map of typed questions keyed by ids you choose.</param>
    /// <param name="model">Optional model override; defaults to <see cref="TypeSafeClientOptions.Model"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<EvaluationResult> EvaluateAsync(
        object state,
        IReadOnlyDictionary<string, object> questions,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(questions);
        if (questions.Count == 0)
            throw new ArgumentException("At least one question is required.", nameof(questions));

        var dtos = questions.ToDictionary(pair => pair.Key, pair => ToDto(pair.Value));
        var request = new EvaluationRequest
        {
            State = QuestionMapperToElement(state),
            Model = model ?? _options.Model,
            Questions = dtos,
        };

        return await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Evaluates a state against questions assembled fluently with an <see cref="EvaluationRequestBuilder"/>.
    /// </summary>
    /// <param name="state">The content to evaluate: a plain string or structured data (object/array).</param>
    /// <param name="configure">Configures the questions to evaluate.</param>
    /// <param name="model">Optional model override; defaults to <see cref="TypeSafeClientOptions.Model"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<EvaluationResult> EvaluateAsync(
        object state,
        Action<EvaluationRequestBuilder> configure,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var builder = new EvaluationRequestBuilder(state);
        configure(builder);
        return EvaluateAsync(builder.State, builder.Questions, model, cancellationToken);
    }

    /// <summary>Asks a single yes/no question and returns the probability of yes (0 to 1).</summary>
    public async Task<NoulAnswer> AskAsync(string question, object state, string? model = null, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(state, b => b.AddNoul("answer", question), model, cancellationToken).ConfigureAwait(false);
        return (NoulAnswer)result.Answers["answer"];
    }

    /// <summary>Picks one option from the given set and returns the chosen option with its probability distribution.</summary>
    public async Task<ChoiceAnswer> ChooseAsync(string question, object state, IReadOnlyDictionary<string, object?> options, string? model = null, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(state, b => b.AddChoice("answer", question, options), model, cancellationToken).ConfigureAwait(false);
        return (ChoiceAnswer)result.Answers["answer"];
    }

    /// <summary>Picks one option from the given set and returns the chosen option with its probability distribution.</summary>
    public Task<ChoiceAnswer> ChooseAsync(string question, object state, params string[] options)
        => ChooseAsync(question, state, options.ToDictionary<string, string, object?>(option => option, _ => null));

    /// <summary>Rates the state along an ordered rubric (2 to 10 levels) and returns a probability-weighted score.</summary>
    public async Task<ScoreAnswer> RateAsync(string question, object state, IReadOnlyList<object> levels, string? model = null, CancellationToken cancellationToken = default)
    {
        var result = await EvaluateAsync(state, b => b.AddScore("answer", question, levels), model, cancellationToken).ConfigureAwait(false);
        return (ScoreAnswer)result.Answers["answer"];
    }

    /// <summary>Rates the state along an ordered rubric (2 to 10 levels) and returns a probability-weighted score.</summary>
    public Task<ScoreAnswer> RateAsync(string question, object state, params object[] levels)
        => RateAsync(question, state, (IReadOnlyList<object>)levels);

    private async Task<EvaluationResult> SendAsync(EvaluationRequest request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request, TypeSafeJsonContext.Default.EvaluationRequest);
        var maxAttempts = Math.Max(1, _options.MaxRetries + 1);

        for (var attempt = 1; ; attempt++)
        {
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(_options.BaseAddress, EvaluationPath))
            {
                Content = content,
            };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await _httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content
                    .ReadFromJsonAsync(TypeSafeJsonContext.Default.EvaluationResult, cancellationToken)
                    .ConfigureAwait(false);
                return result ?? throw new TypeSafeApiException(response.StatusCode, "Empty response body.");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (IsTransient(response.StatusCode) && attempt < maxAttempts)
            {
                var delay = GetRetryDelay(response, attempt);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            throw new TypeSafeApiException(response.StatusCode, body);
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == (HttpStatusCode)429 || (int)statusCode == 529;

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return delta;
        if (retryAfter?.Date is { } date && date > DateTimeOffset.UtcNow)
            return date - DateTimeOffset.UtcNow;

        var backoff = _options.InitialRetryDelay * Math.Pow(2, attempt - 1);
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 250));
        return backoff + jitter;
    }

    private static QuestionDto ToDto(object question) => question switch
    {
        NoulQuestion noul => QuestionMapper.ToDto(noul),
        ChoiceQuestion choice => QuestionMapper.ToDto(choice),
        ScoreQuestion score => QuestionMapper.ToDto(score),
        _ => throw new ArgumentException($"Unsupported question type '{question.GetType().Name}'. Use {nameof(NoulQuestion)}, {nameof(ChoiceQuestion)}, or {nameof(ScoreQuestion)}.", nameof(question)),
    };

    private static JsonElement QuestionMapperToElement(object state) =>
        state is JsonElement element
            ? element.Clone()
            : JsonSerializer.SerializeToElement(state, TypeSafeJsonContext.DefaultOptions);
}
