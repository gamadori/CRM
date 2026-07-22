using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Audio;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CRM.Server.Services
{
    public interface IVoiceTranscriptionService
    {
        /// <summary>True se il servizio è configurato (chiave OpenAI presente).</summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Trascrive l'audio registrato dal microfono in testo, usando OpenAI Whisper.
        /// Il nome file serve solo a Whisper per dedurre il formato (es. "audio.webm").
        /// </summary>
        Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct = default);
    }

    /// <summary>
    /// Trascrizione vocale (speech-to-text) con OpenAI Whisper, usata dall'assistente AI
    /// quando l'amministratore ha scelto la modalità di input vocale "Server". Riusa la
    /// stessa chiave OpenAI già configurata per gli embedding.
    /// </summary>
    public class VoiceTranscriptionService : IVoiceTranscriptionService
    {
        private readonly ILogger<VoiceTranscriptionService> _logger;
        private readonly OpenAIClient? _client;
        private readonly string _model;

        public VoiceTranscriptionService(IConfiguration configuration, ILogger<VoiceTranscriptionService> logger)
        {
            _logger = logger;

            var apiKey = configuration["OpenAI:ApiKey"];
            if (!string.IsNullOrEmpty(apiKey) && apiKey != "YOUR_OPENAI_API_KEY_HERE")
                _client = new OpenAIClient(apiKey);
            else
                _logger.LogWarning("OpenAI API Key non configurata: trascrizione vocale (Whisper) non disponibile.");

            _model = configuration["OpenAI:TranscriptionModel"] ?? "whisper-1";
        }

        public bool IsConfigured => _client != null;

        public async Task<string> TranscribeAsync(Stream audio, string fileName, CancellationToken ct = default)
        {
            if (_client == null)
                throw new InvalidOperationException("Trascrizione vocale non configurata (manca la chiave OpenAI).");

            var audioClient = _client.GetAudioClient(_model);
            var result = await audioClient.TranscribeAudioAsync(
                audio,
                string.IsNullOrWhiteSpace(fileName) ? "audio.webm" : fileName,
                cancellationToken: ct);

            return result.Value?.Text?.Trim() ?? string.Empty;
        }
    }
}
