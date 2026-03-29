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
        private readonly HttpClient _httpClient;
        private string _baseUrl = "http://localhost:11434";

        public OllamaApiClient()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public void SetBaseUrl(string url)
        {
            _baseUrl = url.TrimEnd('/');
        }

        public async Task<List<string>> GetAvailableModelsAsync(CancellationToken cancellationToken = default)
        {
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
            Action<string> onChunkReceived,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<ChatMessage>();

            if (!string.IsNullOrEmpty(systemPrompt))
            {
                messages.Add(new ChatMessage { Role = "system", Content = systemPrompt });
            }
            messages.Add(new ChatMessage { Role = "user", Content = prompt });

            var request = new ChatRequest
            {
                Model = model,
                Messages = messages,
                Stream = true
            };

            var jsonContent = JsonConvert.SerializeObject(request);
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
                                onChunkReceived(chatResponse.Message.Content);
                            }
                        }
                    }
                }
            }
        }

        public async Task<string> GenerateCodeCompletionAsync(
            string model,
            string codeContext,
            string language,
            CancellationToken cancellationToken = default)
        {
            var systemPrompt = $"Eres un asistente de programación experto. Genera código {language} limpio y bien documentado. Solo responde con código, sin explicaciones.";

            var result = new StringBuilder();
            await StreamChatAsync(model, codeContext, systemPrompt, chunk => result.Append(chunk), cancellationToken);
            return result.ToString();
        }
    }

    // Clases para serialización JSON (compatibles con .NET Framework 4.8)
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