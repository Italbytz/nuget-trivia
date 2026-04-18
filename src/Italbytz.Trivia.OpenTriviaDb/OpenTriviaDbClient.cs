using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Italbytz.Trivia.Abstractions;

namespace Italbytz.Trivia.OpenTriviaDb;

public sealed record OpenTriviaDbRequest(
    int Amount = 10,
    int? CategoryId = null,
    Difficulty? Difficulty = null,
    Choices? ChoicesType = null,
    string? SessionToken = null)
{
    public int Amount { get; init; } = Amount < 1 ? 1 : Amount;
}

public enum OpenTriviaDbResponseCode
{
    Success = 0,
    NoResults = 1,
    InvalidParameter = 2,
    TokenNotFound = 3,
    TokenEmpty = 4,
    Unknown = 99
}

public sealed record OpenTriviaDbFetchResult(OpenTriviaDbResponseCode ResponseCode, IQuestion[] Questions, string RawResponse)
{
    public bool IsSuccess => ResponseCode == OpenTriviaDbResponseCode.Success;
}

public sealed class OpenTriviaDbClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public OpenTriviaDbClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            BaseAddress = new Uri("https://opentdb.com/")
        };
    }

    public async Task<OpenTriviaDbFetchResult> GetQuestionsAsync(OpenTriviaDbRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetStringAsync(BuildRequestUri(request), cancellationToken).ConfigureAwait(false);
        return ParseQuestions(response);
    }

    public static OpenTriviaDbFetchResult ParseQuestions(string rawResponse)
    {
        var payload = JsonSerializer.Deserialize<OpenTriviaDbApiResponse>(rawResponse, SerializerOptions);
        if (payload is null)
        {
            return new OpenTriviaDbFetchResult(OpenTriviaDbResponseCode.Unknown, Array.Empty<IQuestion>(), rawResponse);
        }

        var responseCode = Enum.IsDefined(typeof(OpenTriviaDbResponseCode), payload.ResponseCode)
            ? (OpenTriviaDbResponseCode)payload.ResponseCode
            : OpenTriviaDbResponseCode.Unknown;

        var questions = payload.Results?
            .Select(MapQuestion)
            .Where(question => question is not null)
            .Cast<IQuestion>()
            .ToArray() ?? Array.Empty<IQuestion>();

        return new OpenTriviaDbFetchResult(responseCode, questions, rawResponse);
    }

    public async Task<string?> GetSessionTokenAsync(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetStringAsync("api_token.php?command=request", cancellationToken).ConfigureAwait(false);
        var payload = JsonSerializer.Deserialize<OpenTriviaDbTokenResponse>(response, SerializerOptions);
        return payload?.ResponseCode == 0 ? payload.Token : null;
    }

    public async Task<string?> ResetSessionTokenAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return await GetSessionTokenAsync(cancellationToken).ConfigureAwait(false);
        }

        var response = await _httpClient.GetStringAsync($"api_token.php?command=reset&token={Uri.EscapeDataString(sessionToken)}", cancellationToken).ConfigureAwait(false);
        var payload = JsonSerializer.Deserialize<OpenTriviaDbTokenResponse>(response, SerializerOptions);
        return payload?.ResponseCode == 0 ? payload.Token : null;
    }

    private static string BuildRequestUri(OpenTriviaDbRequest request)
    {
        var parameters = new List<string>
        {
            $"amount={request.Amount}"
        };

        if (request.CategoryId is int categoryId)
        {
            parameters.Add($"category={categoryId}");
        }

        if (request.Difficulty is Difficulty difficulty)
        {
            parameters.Add($"difficulty={MapDifficulty(difficulty)}");
        }

        if (request.ChoicesType is Choices choicesType)
        {
            parameters.Add($"type={MapQuestionType(choicesType)}");
        }

        if (!string.IsNullOrWhiteSpace(request.SessionToken))
        {
            parameters.Add($"token={Uri.EscapeDataString(request.SessionToken)}");
        }

        return $"api.php?{string.Join("&", parameters)}";
    }

    private static IQuestion? MapQuestion(OpenTriviaDbQuestion? question)
    {
        if (question is null || string.IsNullOrWhiteSpace(question.Question))
        {
            return null;
        }

        var category = Decode(question.Category);
        var difficulty = MapDifficulty(question.Difficulty);
        var text = Decode(question.Question);

        if (string.Equals(question.Type, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            return new OpenTriviaDbYesNoQuestion
            {
                Category = category,
                Difficulty = difficulty,
                Text = text,
                Answer = string.Equals(question.CorrectAnswer, "True", StringComparison.OrdinalIgnoreCase)
            };
        }

        var options = question.IncorrectAnswers?
            .Select(Decode)
            .Where(answer => !string.IsNullOrWhiteSpace(answer))
            .ToList() ?? [];

        var correctAnswer = Decode(question.CorrectAnswer);
        var correctAnswerIndex = options.Count;
        options.Add(correctAnswer);
        Shuffle(options);
        correctAnswerIndex = options.FindIndex(answer => string.Equals(answer, correctAnswer, StringComparison.Ordinal));

        return new OpenTriviaDbMultipleChoiceQuestion
        {
            Category = category,
            Difficulty = difficulty,
            Text = text,
            PossibleAnswers = options,
            CorrectAnswerIndex = correctAnswerIndex
        };
    }

    private static string Decode(string? value) => WebUtility.HtmlDecode(value ?? string.Empty);

    private static void Shuffle(List<string> answers)
    {
        for (var index = answers.Count - 1; index > 0; index--)
        {
            var swapIndex = Random.Shared.Next(index + 1);
            (answers[index], answers[swapIndex]) = (answers[swapIndex], answers[index]);
        }
    }

    private static string MapDifficulty(Difficulty difficulty) => difficulty switch
    {
        Difficulty.Easy => "easy",
        Difficulty.Hard => "hard",
        _ => "medium"
    };

    private static Difficulty MapDifficulty(string? difficulty) => difficulty?.ToLowerInvariant() switch
    {
        "easy" => Difficulty.Easy,
        "hard" => Difficulty.Hard,
        _ => Difficulty.Medium
    };

    private static string MapQuestionType(Choices choicesType) => choicesType switch
    {
        Choices.Boolean => "boolean",
        _ => "multiple"
    };

    private sealed class OpenTriviaDbApiResponse
    {
        [JsonPropertyName("response_code")]
        public int ResponseCode { get; set; }

        [JsonPropertyName("results")]
        public List<OpenTriviaDbQuestion>? Results { get; set; }
    }

    private sealed class OpenTriviaDbQuestion
    {
        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("difficulty")]
        public string Difficulty { get; set; } = string.Empty;

        [JsonPropertyName("question")]
        public string Question { get; set; } = string.Empty;

        [JsonPropertyName("correct_answer")]
        public string CorrectAnswer { get; set; } = string.Empty;

        [JsonPropertyName("incorrect_answers")]
        public List<string>? IncorrectAnswers { get; set; }
    }

    private sealed class OpenTriviaDbTokenResponse
    {
        [JsonPropertyName("response_code")]
        public int ResponseCode { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;
    }

    private sealed class OpenTriviaDbYesNoQuestion : IYesNoQuestion
    {
        public bool Answer { get; set; }
        public string Category { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; } = QuestionType.Single;
        public Choices ChoicesType { get; set; } = Choices.Boolean;
        public Difficulty Difficulty { get; set; } = Difficulty.Medium;
        public string Text { get; set; } = string.Empty;
        public IQuestion AlternativeQuestion { get; set; } = null!;
    }

    private sealed class OpenTriviaDbMultipleChoiceQuestion : IMultipleChoiceQuestion
    {
        public string Category { get; set; } = string.Empty;
        public QuestionType QuestionType { get; set; } = QuestionType.Single;
        public Choices ChoicesType { get; set; } = Choices.Multiple;
        public Difficulty Difficulty { get; set; } = Difficulty.Medium;
        public string Text { get; set; } = string.Empty;
        public IQuestion AlternativeQuestion { get; set; } = null!;
        public List<string> PossibleAnswers { get; set; } = [];
        public int CorrectAnswerIndex { get; set; }
    }
}
