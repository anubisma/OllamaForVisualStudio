using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OllamaChatExtension
{
    public class OllamaApiClient
    {
        private HttpClient _httpClient;
        private string _baseUrl = "http://localhost:11434";
        private int _timeoutSeconds = 300;
        private bool _hasStartedRequests = false;

        // Historial de conversación para mantener contexto
        private readonly List<ChatMessage> _conversationHistory = new List<ChatMessage>();

        public OllamaApiClient()
        {
            _httpClient = CreateHttpClient(_timeoutSeconds);
        }

        private HttpClient CreateHttpClient(int timeoutSeconds)
        {
            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        public void SetBaseUrl(string url)
        {
            _baseUrl = url.TrimEnd('/');
        }

        public void SetTimeout(int seconds)
        {
            // Si el timeout es el mismo, no hacer nada
            if (_timeoutSeconds == seconds) return;

            // Si ya se han hecho solicitudes, recrear el HttpClient
            if (_hasStartedRequests)
            {
                _timeoutSeconds = seconds;
                var oldClient = _httpClient;
                _httpClient = CreateHttpClient(seconds);
                
                // Disponer el cliente anterior de forma segura en background
                Task.Run(() => 
                {
                    try { oldClient?.Dispose(); } catch { }
                });
            }
            else
            {
                // Si no se han hecho solicitudes, podemos modificarlo directamente
                _timeoutSeconds = seconds;
                _httpClient.Timeout = TimeSpan.FromSeconds(seconds);
            }
        }

        /// <summary>
        /// Limpia el historial de conversación
        /// </summary>
        public void ClearHistory()
        {
            _conversationHistory.Clear();
        }

        /// <summary>
        /// Obtiene el número de mensajes en el historial
        /// </summary>
        public int HistoryCount => _conversationHistory.Count;

        public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
            _hasStartedRequests = true;
            var models = new List<string>();
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags", cancellationToken);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                var modelsResponse = JsonConvert.DeserializeObject<ModelsResponse>(json);

                if (modelsResponse?.Models != null)
                {
                    models.AddRange(modelsResponse.Models.Select(m => m.Name));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error obteniendo modelos: {ex.Message}");
            }
            return models;
        }

        public async Task StreamChatAsync(
            string model,
            string prompt,
            string systemPrompt,
            OllamaGenerationOptions options,
            Action<string> onChunkReceived,
            CancellationToken cancellationToken = default)
        {
            _hasStartedRequests = true;
            var messages = new List<ChatMessage>();

            // Agregar system prompt
            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            }

            // Agregar historial de conversación si está habilitado
            if (options.KeepConversationHistory && _conversationHistory.Count > 0)
            {
                var historyToInclude = _conversationHistory;
                if (options.MaxHistoryMessages > 0 && _conversationHistory.Count > options.MaxHistoryMessages * 2)
                {
                    historyToInclude = _conversationHistory
                        .Skip(_conversationHistory.Count - (options.MaxHistoryMessages * 2))
                        .ToList();
                }
                messages.AddRange(historyToInclude);
            }

            messages.Add(new ChatMessage { Role = "user", Content = prompt });

            var request = new ChatRequest
            {
                Model = model,
                Messages = messages,
                Stream = true,
                Options = CreateOptionsObject(options),
                KeepAlive = options.GetKeepAliveValue()
            };

            var jsonContent = JsonConvert.SerializeObject(request, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });

            var assistantResponse = new StringBuilder();

            using (var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/chat"))
            {
                httpRequest.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using (var response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken))
                {
                    response.EnsureSuccessStatusCode();

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                        {
                            var line = await reader.ReadLineAsync();
                            if (string.IsNullOrEmpty(line)) continue;

                            var chatResponse = JsonConvert.DeserializeObject<ChatResponse>(line);
                            if (chatResponse?.Message?.Content != null)
                            {
                                assistantResponse.Append(chatResponse.Message.Content);
                                onChunkReceived(chatResponse.Message.Content);
                            }
                        }
                    }
                }
            }

            if (options.KeepConversationHistory)
            {
                _conversationHistory.Add(new ChatMessage { Role = "user", Content = prompt });
                _conversationHistory.Add(new ChatMessage { Role = "assistant", Content = assistantResponse.ToString() });

                while (options.MaxHistoryMessages > 0 && _conversationHistory.Count > options.MaxHistoryMessages * 2)
                {
                    _conversationHistory.RemoveAt(0);
                    _conversationHistory.RemoveAt(0);
                }
            }
        }

        // Sobrecarga para compatibilidad hacia atrás
        public async Task StreamChatAsync(
            string model,
            string prompt,
            string systemPrompt,
            Action<string> onChunkReceived,
            CancellationToken cancellationToken = default)
        {
            await StreamChatAsync(model, prompt, systemPrompt, new OllamaGenerationOptions(), onChunkReceived, cancellationToken);
        }

        private Dictionary<string, object> CreateOptionsObject(OllamaGenerationOptions options)
        {
            var opts = new Dictionary<string, object>();

            if (options.MaxTokens != 2048) opts["num_predict"] = options.MaxTokens;
            if (options.ContextSize != 4096) opts["num_ctx"] = options.ContextSize;
            if (options.Seed != 0) opts["seed"] = options.Seed;
            if (options.NumThreads > 0) opts["num_thread"] = options.NumThreads;
            if (options.NumGpu != -1) opts["num_gpu"] = options.NumGpu;

            if (Math.Abs(options.Temperature - 0.7) > 0.001) opts["temperature"] = options.Temperature;
            if (Math.Abs(options.TopP - 0.9) > 0.001) opts["top_p"] = options.TopP;
            if (options.TopK != 40) opts["top_k"] = options.TopK;
            if (options.MinP > 0.001) opts["min_p"] = options.MinP;

            if (Math.Abs(options.RepeatPenalty - 1.1) > 0.001) opts["repeat_penalty"] = options.RepeatPenalty;
            if (options.RepeatLastN != 64) opts["repeat_last_n"] = options.RepeatLastN;
            if (options.PresencePenalty > 0.001) opts["presence_penalty"] = options.PresencePenalty;
            if (options.FrequencyPenalty > 0.001) opts["frequency_penalty"] = options.FrequencyPenalty;

            if (options.Mirostat != 0)
            {
                opts["mirostat"] = options.Mirostat;
                opts["mirostat_tau"] = options.MirostatTau;
                opts["mirostat_eta"] = options.MirostatEta;
            }

            if (Math.Abs(options.TfsZ - 1.0) > 0.001) opts["tfs_z"] = options.TfsZ;
            if (Math.Abs(options.TypicalP - 1.0) > 0.001) opts["typical_p"] = options.TypicalP;
            if (options.PenalizeNewline) opts["penalize_newline"] = true;

            if (options.StopTokens != null && options.StopTokens.Length > 0)
            {
                opts["stop"] = options.StopTokens;
            }

            return opts.Count > 0 ? opts : null;
        }

        public async Task<string> GenerateCodeCompletionAsync(
            string model,
            string codeContext,
            string language,
            CancellationToken cancellationToken = default)
        {
            _hasStartedRequests = true;
            var systemPrompt = $"Eres un asistente de programación experto. Genera código {language} limpio y bien documentado. Solo responde con código, sin explicaciones.";

            var result = new StringBuilder();
            await StreamChatAsync(model, codeContext, systemPrompt, chunk => result.Append(chunk), cancellationToken);
            return result.ToString();
        }
    }

    /// <summary>
    /// Opciones de generación para Ollama API
    /// </summary>
    // Elimina el método FromSettings que usa dynamic (líneas 270-293)
    // O reemplázalo con este que no usa dynamic:

    /// <summary>
    /// Opciones de generación para Ollama API
    /// </summary>
    public class OllamaGenerationOptions
    {
        // Generación
        public int MaxTokens { get; set; } = 2048;
        public int ContextSize { get; set; } = 4096;
        public int Seed { get; set; } = 0;
        public int NumThreads { get; set; } = 0;
        public int NumGpu { get; set; } = -1;

        // Creatividad
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 0.9;
        public int TopK { get; set; } = 40;
        public double MinP { get; set; } = 0.0;

        // Penalizaciones
        public double RepeatPenalty { get; set; } = 1.1;
        public int RepeatLastN { get; set; } = 64;
        public double PresencePenalty { get; set; } = 0.0;
        public double FrequencyPenalty { get; set; } = 0.0;

        // Mirostat
        public int Mirostat { get; set; } = 0;
        public double MirostatTau { get; set; } = 5.0;
        public double MirostatEta { get; set; } = 0.1;

        // Avanzado
        public double TfsZ { get; set; } = 1.0;
        public double TypicalP { get; set; } = 1.0;
        public bool PenalizeNewline { get; set; } = false;
        public string[] StopTokens { get; set; }

        // Memoria
        public bool KeepConversationHistory { get; set; } = true;
        public int MaxHistoryMessages { get; set; } = 20;
        public bool KeepAlive { get; set; } = true;
        public int KeepAliveSeconds { get; set; } = 300;

        public string GetKeepAliveValue()
        {
            if (!KeepAlive) return "0";
            if (KeepAliveSeconds < 0) return "-1";
            return KeepAliveSeconds + "s";
        }

        // ELIMINADO: El método FromSettings que usaba dynamic
        // La conversión se hace directamente en OllamaChatControl.GetGenerationOptions()
    }

    // ==================== Clases para serialización JSON ====================
    public class ModelsResponse
    {
        [JsonProperty("models")]
        public Model[] Models { get; set; }
    }

    public class Model
    {
        [JsonProperty("name")]
        public string Name { get; set; }
    }

    public class ChatMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

    public class ChatRequest
    {
        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("messages")]
        public List<ChatMessage> Messages { get; set; }

        [JsonProperty("stream")]
        public bool Stream { get; set; }

        [JsonProperty("options")]
        public Dictionary<string, object> Options { get; set; }

        [JsonProperty("keep_alive")]
        public string KeepAlive { get; set; }
    }

    public class ChatResponse
    {
        [JsonProperty("message")]
        public ChatMessageResponse Message { get; set; }
    }

    public class ChatMessageResponse
    {
        [JsonProperty("content")]
        public string Content { get; set; }
    }
}