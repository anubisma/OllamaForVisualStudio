using Microsoft.VisualStudio.Shell;
using System.ComponentModel;

namespace OllamaForVisualStudio
{
    public class OllamaOptions : DialogPage
    {
        [Category("Ollama")]
        [DisplayName("URL de Ollama")]
        [Description("La URL donde está ejecutándose el servidor de Ollama")]
        public string OllamaUrl { get; set; } = "http://localhost:11434";

        [Category("Ollama")]
        [DisplayName("Modelo seleccionado")]
        [Description("Nombre del modelo de Ollama a utilizar")]
        public string SelectedModel { get; set; } = "llama3";

        [Category("Ollama")]
        [DisplayName("Máximo de tokens")]
        [Description("Número máximo de tokens en la respuesta")]
        public int MaxTokens { get; set; } = 2048;

        [Category("Ollama")]
        [DisplayName("System Prompt")]
        [Description("Instrucciones del sistema para el modelo")]
        public string SystemPrompt { get; set; } = "Eres un asistente de programación experto en Visual Studio y desarrollo de software.";
    }
}