using CRM.Shared;
using CRM.Shared.DTOs;
using CRM.Shared.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRM.Client.Services
{
    /// <summary>
    /// Client HTTP dell'assistente AI unificato: streaming NDJSON della chat
    /// (una riga JSON per <see cref="AssistantStreamEvent"/>), feedback e log.
    /// </summary>
    public class ProxyAssistantService : IAssistantService
    {
        private const string BasePath = "api/CrmAssistant";

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient _http;

        public ProxyAssistantService(HttpClient http)
        {
            _http = http;
        }

        public async Task ChatStream(AssistantChatRequest request, Action<AssistantStreamEvent> onEvent)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/chat-stream")
                {
                    Content = JsonContent.Create(request)
                };
                // Abilita lo streaming della risposta nel browser (Blazor WASM)
                req.SetBrowserResponseStreamingEnabled(true);

                using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    onEvent(new AssistantStreamEvent
                    {
                        Type = AssistantStreamEvent.TypeError,
                        Text = string.IsNullOrWhiteSpace(err) ? $"Errore dal server ({(int)resp.StatusCode})" : err
                    });
                    return;
                }

                using var stream = await resp.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream, Encoding.UTF8);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    AssistantStreamEvent? streamEvent;
                    try
                    {
                        streamEvent = JsonSerializer.Deserialize<AssistantStreamEvent>(line, JsonOptions);
                    }
                    catch
                    {
                        // Riga malformata: ignora e prosegui col flusso
                        continue;
                    }

                    if (streamEvent != null)
                        onEvent(streamEvent);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore assistant chat stream: {ex.Message}");
                onEvent(new AssistantStreamEvent { Type = AssistantStreamEvent.TypeError, Text = ex.Message });
            }
        }

        public async Task<VoiceTranscriptionResult> Transcribe(byte[] audio, string contentType, string fileName)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                var audioContent = new ByteArrayContent(audio);

                // Il costruttore di MediaTypeHeaderValue NON accetta i parametri (";codecs=opus"
                // che Chrome mette nel MIME del MediaRecorder): si tiene solo il media type base.
                // Per Whisper conta comunque l'estensione del filename, non l'header.
                var mediaType = contentType;
                if (!string.IsNullOrWhiteSpace(mediaType))
                {
                    var semi = mediaType.IndexOf(';');
                    if (semi > 0)
                        mediaType = mediaType.Substring(0, semi);
                    try { audioContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType.Trim()); }
                    catch { /* MIME non valido: si lascia il default del multipart */ }
                }

                content.Add(audioContent, "audio", string.IsNullOrWhiteSpace(fileName) ? "audio.webm" : fileName);

                var resp = await _http.PostAsync($"{BasePath}/transcribe", content);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync();
                    return new VoiceTranscriptionResult
                    {
                        Success = false,
                        Error = string.IsNullOrWhiteSpace(err) ? $"Errore dal server ({(int)resp.StatusCode})" : err
                    };
                }

                var result = await resp.Content.ReadFromJsonAsync<TranscriptionResult>(JsonOptions);
                return new VoiceTranscriptionResult { Success = true, Text = result?.Text };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore trascrizione vocale: {ex.Message}");
                return new VoiceTranscriptionResult { Success = false, Error = ex.Message };
            }
        }

        private sealed class TranscriptionResult
        {
            public string? Text { get; set; }
        }

        public async Task<bool> SendFeedback(AssistantFeedbackRequest request)
        {
            try
            {
                var resp = await _http.PostAsJsonAsync($"{BasePath}/feedback", request);
                return resp.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore invio feedback assistente: {ex.Message}");
                return false;
            }
        }

        public async Task<List<AssistantChatLogDTO>> GetLogs(AssistantChatLogFilter filter)
        {
            var query = new List<string>();
            if (filter != null)
            {
                if (!string.IsNullOrWhiteSpace(filter.Search))
                    query.Add($"Search={Uri.EscapeDataString(filter.Search)}");
                if (filter.Vote.HasValue)
                    query.Add($"Vote={filter.Vote.Value}");
            }

            var url = query.Count > 0
                ? $"{BasePath}/logs?{string.Join("&", query)}"
                : $"{BasePath}/logs";

            try
            {
                return await _http.GetFromJsonAsync<List<AssistantChatLogDTO>>(url) ?? new();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore GetLogs assistente: {ex.Message}");
                return new();
            }
        }
    }
}
