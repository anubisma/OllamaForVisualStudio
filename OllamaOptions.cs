using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Drawing.Design;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace OllamaForVisualStudio
{
    public class OllamaOptions : DialogPage
    {
        // ==================== CONEXIÓN ====================
        [Category("1. Conexión")]
        [DisplayName("URL de Ollama")]
        [Description("La URL donde está ejecutándose el servidor de Ollama (ej: http://localhost:11434)")]
        public string OllamaUrl { get; set; } = "http://localhost:11434";

        [Category("1. Conexión")]
        [DisplayName("Modelo seleccionado")]
        [Description("Nombre del modelo de Ollama a utilizar por defecto")]
        public string SelectedModel { get; set; } = "llama3";

        [Category("1. Conexión")]
        [DisplayName("Timeout (segundos)")]
        [Description("Tiempo máximo de espera para respuestas del servidor")]
        public int TimeoutSeconds { get; set; } = 300;

        // ==================== GENERACIÓN ====================
        [Category("2. Generación")]
        [DisplayName("Máximo de tokens (num_predict)")]
        [Description("Número máximo de tokens a generar en la respuesta. -1 = ilimitado, -2 = llenar contexto")]
        public int MaxTokens { get; set; } = 2048;

        [Category("2. Generación")]
        [DisplayName("Tamaño de contexto (num_ctx)")]
        [Description("Tamaño de la ventana de contexto para generar el siguiente token (default: 2048)")]
        public int ContextSize { get; set; } = 4096;

        [Category("2. Generación")]
        [DisplayName("Seed")]
        [Description("Semilla para generación reproducible. 0 = aleatorio")]
        public int Seed { get; set; } = 0;

        [Category("2. Generación")]
        [DisplayName("Número de hilos (num_thread)")]
        [Description("Número de hilos CPU a usar. 0 = automático")]
        public int NumThreads { get; set; } = 0;

        [Category("2. Generación")]
        [DisplayName("Número de capas GPU (num_gpu)")]
        [Description("Número de capas a cargar en GPU. 0 = solo CPU, -1 = todas")]
        public int NumGpu { get; set; } = -1;

        // ==================== CREATIVIDAD (SAMPLING) ====================
        [Category("3. Creatividad")]
        [DisplayName("Temperatura")]
        [Description("Controla la creatividad (0.0 = determinista, 2.0 = muy creativo). Recomendado: 0.7-0.8")]
        public double Temperature { get; set; } = 0.7;

        [Category("3. Creatividad")]
        [DisplayName("Top P (nucleus sampling)")]
        [Description("Probabilidad acumulada para nucleus sampling (0.0-1.0). Valores bajos = respuestas más enfocadas")]
        public double TopP { get; set; } = 0.9;

        [Category("3. Creatividad")]
        [DisplayName("Top K")]
        [Description("Limita los tokens candidatos a los K más probables. 0 = deshabilitado")]
        public int TopK { get; set; } = 40;

        [Category("3. Creatividad")]
        [DisplayName("Min P")]
        [Description("Probabilidad mínima relativa al token más probable (0.0-1.0)")]
        public double MinP { get; set; } = 0.0;

        // ==================== PENALIZACIONES ====================
        [Category("4. Penalizaciones")]
        [DisplayName("Penalización de repetición")]
        [Description("Penaliza la repetición de tokens (1.0 = sin penalización, >1.0 = menos repetición)")]
        public double RepeatPenalty { get; set; } = 1.1;

        [Category("4. Penalizaciones")]
        [DisplayName("Ventana de repetición (repeat_last_n)")]
        [Description("Tokens anteriores a considerar para penalización. 0 = deshabilitado, -1 = num_ctx")]
        public int RepeatLastN { get; set; } = 64;

        [Category("4. Penalizaciones")]
        [DisplayName("Penalización de presencia")]
        [Description("Penaliza tokens que ya aparecieron (0.0-1.0)")]
        public double PresencePenalty { get; set; } = 0.0;

        [Category("4. Penalizaciones")]
        [DisplayName("Penalización de frecuencia")]
        [Description("Penaliza tokens según su frecuencia (0.0-1.0)")]
        public double FrequencyPenalty { get; set; } = 0.0;

        // ==================== MIROSTAT ====================
        [Category("5. Mirostat")]
        [DisplayName("Modo Mirostat")]
        [Description("Algoritmo de muestreo adaptativo. 0 = deshabilitado, 1 = Mirostat, 2 = Mirostat 2.0")]
        public int Mirostat { get; set; } = 0;

        [Category("5. Mirostat")]
        [DisplayName("Mirostat Tau")]
        [Description("Objetivo de perplejidad para Mirostat (default: 5.0)")]
        public double MirostatTau { get; set; } = 5.0;

        [Category("5. Mirostat")]
        [DisplayName("Mirostat Eta")]
        [Description("Tasa de aprendizaje de Mirostat (default: 0.1)")]
        public double MirostatEta { get; set; } = 0.1;

        // ==================== CONTEXTO/MEMORIA ====================
        [Category("6. Memoria")]
        [DisplayName("Mantener historial de conversación")]
        [Description("Si está activado, el modelo recordará mensajes anteriores de la conversación")]
        public bool KeepConversationHistory { get; set; } = true;

        [Category("6. Memoria")]
        [DisplayName("Máximo de mensajes en historial")]
        [Description("Número máximo de mensajes (pares usuario/asistente) a mantener en memoria")]
        public int MaxHistoryMessages { get; set; } = 20;

        [Category("6. Memoria")]
        [DisplayName("Mantener contexto entre sesiones")]
        [Description("Mantiene el contexto del modelo cargado en memoria (más rápido)")]
        public bool KeepAlive { get; set; } = true;

        [Category("6. Memoria")]
        [DisplayName("Tiempo Keep Alive (segundos)")]
        [Description("Tiempo en segundos que el modelo permanece cargado. -1 = indefinido, 0 = descargar inmediatamente")]
        public int KeepAliveSeconds { get; set; } = 300;

        // ==================== TOKENS DE PARADA ====================
        [Category("7. Control")]
        [DisplayName("Tokens de parada")]
        [Description("Secuencias que detienen la generación (separadas por coma). Ej: </s>,<|end|>,###")]
        public string StopTokens { get; set; } = "";

        [Category("7. Control")]
        [DisplayName("Penalizar nueva línea")]
        [Description("Penaliza tokens de nueva línea en la generación")]
        public bool PenalizeNewline { get; set; } = false;

        // ==================== TFS/TÍPICO ====================
        [Category("8. Avanzado")]
        [DisplayName("TFS Z (Tail Free Sampling)")]
        [Description("Tail free sampling parameter (1.0 = deshabilitado)")]
        public double TfsZ { get; set; } = 1.0;

        [Category("8. Avanzado")]
        [DisplayName("Typical P")]
        [Description("Locally typical sampling (1.0 = deshabilitado)")]
        public double TypicalP { get; set; } = 1.0;

        // ==================== SYSTEM PROMPT ====================
        [Category("9. System Prompt")]
        [DisplayName("System Prompt")]
        [Description("Instrucciones del sistema para definir el comportamiento del modelo")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string SystemPrompt { get; set; } = DefaultSystemPrompt;

        private const string DefaultSystemPrompt = @"You are an expert programming assistant integrated into Visual Studio.

CAPABILITIES:
- Analyze and explain code in any language
- Detect bugs, performance issues, and bad practices
- Suggest refactorings and improvements
- Generate clean, well-documented code
- Write unit tests
- Explain programming concepts

RESPONSE FORMAT:
- Use code blocks with the language specified: ```csharp, ```python, etc.
- Use **bold** for important terms
- Use `inline code` for variable, method, or class names
- Use numbered or bulleted lists for steps or multiple options
- Be concise but complete

BEHAVIOR:
- ALWAYS, ALWAYS, ALWAYS answer in the language the user is interacting with you, regardless of the prompt language
- If you don't know something, admit it honestly
- Ask for clarification if the request is ambiguous
- Consider Visual Studio and .NET context when relevant";

        // Método para obtener stop tokens como array
        public string[] GetStopTokensArray()
        {
            if (string.IsNullOrWhiteSpace(StopTokens))
                return new string[0];

            return StopTokens.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // Método para obtener keep_alive en formato Ollama
        public string GetKeepAliveValue()
        {
            if (!KeepAlive) return "0";
            if (KeepAliveSeconds < 0) return "-1";
            return KeepAliveSeconds + "s";
        }
    }

    /// <summary>
    /// Editor personalizado para texto multilínea grande
    /// </summary>
    public class MultilineStringEditor : UITypeEditor
    {
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context)
        {
            return UITypeEditorEditStyle.Modal;
        }

        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            var editorService = provider?.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            if (editorService == null) return value;

            using (var form = new Form())
            {
                form.Text = "Editar System Prompt";
                form.Size = new System.Drawing.Size(700, 500);
                form.StartPosition = FormStartPosition.CenterParent;
                form.MinimizeBox = false;
                form.MaximizeBox = true;
                form.ShowInTaskbar = false;

                var textBox = new System.Windows.Forms.TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Both,
                    Dock = DockStyle.Fill,
                    Text = value as string ?? "",
                    Font = new System.Drawing.Font("Consolas", 10),
                    AcceptsReturn = true,
                    AcceptsTab = true,
                    WordWrap = true
                };

                var buttonPanel = new System.Windows.Forms.Panel
                {
                    Dock = DockStyle.Bottom,
                    Height = 45
                };

                var okButton = new System.Windows.Forms.Button
                {
                    Text = "Aceptar",
                    DialogResult = DialogResult.OK,
                    Size = new System.Drawing.Size(90, 30),
                    Location = new System.Drawing.Point(490, 8)
                };
                okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                var cancelButton = new System.Windows.Forms.Button
                {
                    Text = "Cancelar",
                    DialogResult = DialogResult.Cancel,
                    Size = new System.Drawing.Size(90, 30),
                    Location = new System.Drawing.Point(590, 8)
                };
                cancelButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                buttonPanel.Controls.Add(okButton);
                buttonPanel.Controls.Add(cancelButton);

                form.Controls.Add(textBox);
                form.Controls.Add(buttonPanel);
                form.AcceptButton = okButton;
                form.CancelButton = cancelButton;

                if (editorService.ShowDialog(form) == DialogResult.OK)
                {
                    return textBox.Text;
                }
            }

            return value;
        }
    }
}