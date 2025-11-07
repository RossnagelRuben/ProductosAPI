using System.Net.Http.Headers;
using System.Text.Json;
using BlazorApp_ProductosAPI.Models;

namespace BlazorApp_ProductosAPI.Services;

public class OcrService : IOcrService
{
    private readonly HttpClient _httpClient;

    public OcrService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<OcrResult> ProcessOcrAsync(byte[] imageBytes, string apiKey, double naturalWidth = 800, double naturalHeight = 600)
    {
        try
        {
            var url = $"https://vision.googleapis.com/v1/images:annotate?key={apiKey}";
            var body = new VisionRequest
            {
                Requests = new[]
                {
                    new VisionRequestItem
                    {
                        Image = new VisionImage { Content = Convert.ToBase64String(imageBytes) },
                        Features = new[] { new VisionFeature { Type = "DOCUMENT_TEXT_DETECTION" } }
                    }
                }
            };

            var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };

            var res = await _httpClient.SendAsync(req);
            var json = await res.Content.ReadAsStringAsync();

            if (!res.IsSuccessStatusCode)
            {
                return new OcrResult
                {
                    Success = false,
                    ErrorMessage = $"Vision API error {(int)res.StatusCode} - {res.ReasonPhrase}. Respuesta: {json}"
                };
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var parsed = JsonSerializer.Deserialize<VisionResponse>(json, options);

            var ann = parsed?.Responses?.FirstOrDefault()?.FullTextAnnotation;
            if (ann is null)
            {
                return new OcrResult
                {
                    Success = false,
                    ErrorMessage = "No se detectó texto en la imagen."
                };
            }

            var fullText = ann.Text ?? "";
            var polygons = new List<PolygonModel>();

            foreach (var page in ann.Pages)
            {
                foreach (var block in page.Blocks)
                {
                    var text = new System.Text.StringBuilder();
                    foreach (var paragraph in block.Paragraphs)
                    {
                        foreach (var word in paragraph.Words)
                        {
                            foreach (var symbol in word.Symbols) text.Append(symbol.Text);
                            text.Append(' ');
                        }
                        text.AppendLine();
                    }

                    var rawPts = block.BoundingBox?.Vertices?
                        .Where(v => v != null)
                        .Select(v => new Point(v.X, v.Y))
                        .ToList() ?? new List<Point>();

                    bool bad = rawPts.Count < 3 || rawPts.Count(p => p.X == 0 && p.Y == 0) >= 2;

                    if (bad && block.BoundingBox?.NormalizedVertices?.Count >= 3)
                    {
                        rawPts = block.BoundingBox.NormalizedVertices
                            .Select(v => new Point(v.X, v.Y))
                            .ToList();
                    }

                    if (rawPts.Count < 3) continue;

                    var normalizedPts = PolygonHelper.NormalizeIfNeeded(rawPts, naturalWidth, naturalHeight);
                    polygons.Add(new PolygonModel(normalizedPts, text.ToString().Trim()));
                }
            }

            return new OcrResult
            {
                Success = true,
                FullText = fullText,
                Polygons = polygons
            };
        }
        catch (Exception ex)
        {
            return new OcrResult
            {
                Success = false,
                ErrorMessage = $"Excepción OCR: {ex.Message}"
            };
        }
    }
}

