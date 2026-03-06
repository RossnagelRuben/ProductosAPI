using System.Net.Http.Headers;
using System.Text.Json;

namespace BlazorApp_ProductosAPI.Services;

/// <summary>
/// Implementación de generación de texto vía OpenAI Chat Completions API.
/// Usa un HttpClient dedicado para no mezclar con el Bearer de la app (SOLID: una razón para cambiar).
/// Documentación: https://developers.openai.com/api/docs
/// </summary>
public sealed class OpenAITextService : IOpenAITextService
{
    private static readonly HttpClient OpenAiHttpClient = new HttpClient();

    private const string ChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const string DefaultModel = "gpt-4o-mini";

    public OpenAITextService(HttpClient _)
    {
        // HttpClient inyectado no se usa; evitamos enviar BaseAddress/headers de la app a OpenAI.
    }

    /// <inheritdoc />
    public async Task<OpenAITextResult> GenerateTextAsync(
        string prompt,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new OpenAITextResult { Success = false, ErrorMessage = "Falta la API key de OpenAI." };
        if (string.IsNullOrWhiteSpace(prompt))
            return new OpenAITextResult { Success = false, ErrorMessage = "El prompt no puede estar vacío." };

        try
        {
            var body = new
            {
                model = DefaultModel,
                messages = new[] { new { role = "user", content = prompt } },
                max_tokens = 2048
            };
            var request = new HttpRequestMessage(HttpMethod.Post, ChatCompletionsUrl)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(body),
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

            var response = await OpenAiHttpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new OpenAITextResult
                {
                    Success = false,
                    ErrorMessage = $"OpenAI {(int)response.StatusCode}: {json}"
                };

            var text = ParseChatCompletionContent(json);
            return new OpenAITextResult { Success = true, Text = text };
        }
        catch (Exception ex)
        {
            return new OpenAITextResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>
    /// Extrae el contenido del primer mensaje de la respuesta Chat Completions.
    /// Estructura esperada: { "choices": [ { "message": { "content": "..." } } ] }
    /// </summary>
    private static string? ParseChatCompletionContent(string responseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
                return null;
            var first = choices[0];
            if (!first.TryGetProperty("message", out var message))
                return null;
            if (!message.TryGetProperty("content", out var content))
                return null;
            return content.GetString();
        }
        catch
        {
            return null;
        }
    }
}
