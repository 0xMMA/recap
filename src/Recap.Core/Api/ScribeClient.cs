using System.Net.Http.Headers;
using System.Text.Json;

namespace Recap.Core.Api;

public class ScribeClient
{
    private readonly HttpClient _http = new();
    private const string Endpoint = "https://api.elevenlabs.io/v1/speech-to-text";

    public string? ApiKey { get; set; }

    public async Task<string> TranscribeAsync(
        string filePath,
        string? languageCode = null,
        bool tagAudioEvents = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(ApiKey))
            throw new InvalidOperationException("API key not set");

        using var content = new MultipartFormDataContent();
        var fileBytes = await File.ReadAllBytesAsync(filePath, ct);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(fileContent, "file", Path.GetFileName(filePath));
        content.Add(new StringContent("scribe_v2"), "model_id");

        if (!string.IsNullOrEmpty(languageCode) && languageCode != "auto")
            content.Add(new StringContent(languageCode), "language_code");

        content.Add(new StringContent(tagAudioEvents.ToString().ToLower()), "tag_audio_events");

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("xi-api-key", ApiKey);
        request.Content = content;

        var response = await _http.SendAsync(request, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            // Parse API error detail if available
            string detail = $"{(int)response.StatusCode} {response.StatusCode}";
            try
            {
                using var errDoc = JsonDocument.Parse(json);
                if (errDoc.RootElement.TryGetProperty("detail", out var d))
                    detail += $": {d}";
                else
                    detail += $": {json}";
            }
            catch
            {
                if (!string.IsNullOrWhiteSpace(json))
                    detail += $": {json}";
            }
            throw new HttpRequestException(detail);
        }

        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("text").GetString() ?? string.Empty;
    }
}
