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
        // ==================== CONNECTION ====================
        [Category("1. Connection")]
        [DisplayName("Ollama URL")]
        [Description("URL where the Ollama server is running (e.g. http://localhost:11434)")]
        public string OllamaUrl { get; set; } = "http://localhost:11434";

        [Category("1. Connection")]
        [DisplayName("Selected model")]
        [Description("Name of the Ollama model to use by default")]
        public string SelectedModel { get; set; } = "llama3";

        [Category("1. Connection")]
        [DisplayName("Timeout (seconds)")]
        [Description("Maximum waiting time for server responses")]
        public int TimeoutSeconds { get; set; } = 300;

        // ==================== GENERATION ====================
        [Category("2. Generation")]
        [DisplayName("Max tokens (num_predict)")]
        [Description("Maximum number of tokens to generate in the response. -1 = unlimited, -2 = fill context")]
        public int MaxTokens { get; set; } = 2048;

        [Category("2. Generation")]
        [DisplayName("Context size (num_ctx)")]
        [Description("Context window size for generating the next token (default: 2048)")]
        public int ContextSize { get; set; } = 4096;

        [Category("2. Generation")]
        [DisplayName("Seed")]
        [Description("Seed for reproducible generation. 0 = random")]
        public int Seed { get; set; } = 0;

        [Category("2. Generation")]
        [DisplayName("Number of threads (num_thread)")]
        [Description("CPU threads to use. 0 = automatic")]
        public int NumThreads { get; set; } = 0;

        [Category("2. Generation")]
        [DisplayName("GPU layers (num_gpu)")]
        [Description("Number of layers to load on GPU. 0 = CPU only, -1 = all")]
        public int NumGpu { get; set; } = -1;

        // ==================== CREATIVITY (SAMPLING) ====================
        [Category("3. Creativity")]
        [DisplayName("Temperature")]
        [Description("Controls randomness (0.0 = deterministic, 2.0 = very creative). Recommended: 0.7-0.8")]
        public double Temperature { get; set; } = 0.7;

        [Category("3. Creativity")]
        [DisplayName("Top P (nucleus sampling)")]
        [Description("Cumulative probability cutoff for nucleus sampling (0.0-1.0). Lower = more focused responses")]
        public double TopP { get; set; } = 0.9;

        [Category("3. Creativity")]
        [DisplayName("Top K")]
        [Description("Limits token candidates to the top K most probable. 0 = disabled")]
        public int TopK { get; set; } = 40;

        [Category("3. Creativity")]
        [DisplayName("Min P")]
        [Description("Minimum probability relative to the most likely token (0.0-1.0)")]
        public double MinP { get; set; } = 0.0;

        // ==================== PENALTIES ====================
        [Category("4. Penalties")]
        [DisplayName("Repeat penalty")]
        [Description("Penalizes repeated tokens (1.0 = no penalty, >1.0 = less repetition)")]
        public double RepeatPenalty { get; set; } = 1.1;

        [Category("4. Penalties")]
        [DisplayName("Repeat window (repeat_last_n)")]
        [Description("Number of previous tokens to consider for penalty. 0 = disabled, -1 = num_ctx")]
        public int RepeatLastN { get; set; } = 64;

        [Category("4. Penalties")]
        [DisplayName("Presence penalty")]
        [Description("Penalizes tokens that already appeared (0.0-1.0)")]
        public double PresencePenalty { get; set; } = 0.0;

        [Category("4. Penalties")]
        [DisplayName("Frequency penalty")]
        [Description("Penalizes tokens based on frequency (0.0-1.0)")]
        public double FrequencyPenalty { get; set; } = 0.0;

        // ==================== MIROSTAT ====================
        [Category("5. Mirostat")]
        [DisplayName("Mirostat mode")]
        [Description("Adaptive sampling algorithm. 0 = disabled, 1 = Mirostat, 2 = Mirostat 2.0")]
        public int Mirostat { get; set; } = 0;

        [Category("5. Mirostat")]
        [DisplayName("Mirostat Tau")]
        [Description("Target perplexity for Mirostat (default: 5.0)")]
        public double MirostatTau { get; set; } = 5.0;

        [Category("5. Mirostat")]
        [DisplayName("Mirostat Eta")]
        [Description("Mirostat learning rate (default: 0.1)")]
        public double MirostatEta { get; set; } = 0.1;

        // ==================== CONTEXT / MEMORY ====================
        [Category("6. Memory")]
        [DisplayName("Keep conversation history")]
        [Description("If enabled, the model remembers previous messages in the conversation")]
        public bool KeepConversationHistory { get; set; } = true;

        [Category("6. Memory")]
        [DisplayName("Max history messages")]
        [Description("Maximum number of message pairs (user/assistant) to keep in memory")]
        public int MaxHistoryMessages { get; set; } = 20;

        [Category("6. Memory")]
        [DisplayName("Keep context between sessions")]
        [Description("Keeps the model loaded in memory (faster responses)")]
        public bool KeepAlive { get; set; } = true;

        [Category("6. Memory")]
        [DisplayName("Keep alive time (seconds)")]
        [Description("Time in seconds the model stays loaded. -1 = indefinite, 0 = unload immediately")]
        public int KeepAliveSeconds { get; set; } = 300;

        // ==================== STOP TOKENS ====================
        [Category("7. Control")]
        [DisplayName("Stop tokens")]
        [Description("Sequences that stop generation (comma-separated). Example: </s>,<|end|>,###")]
        public string StopTokens { get; set; } = "";

        [Category("7. Control")]
        [DisplayName("Penalize newline")]
        [Description("Penalizes newline tokens during generation")]
        public bool PenalizeNewline { get; set; } = false;

        // ==================== ADVANCED ====================
        [Category("8. Advanced")]
        [DisplayName("TFS Z (Tail Free Sampling)")]
        [Description("Tail free sampling parameter (1.0 = disabled)")]
        public double TfsZ { get; set; } = 1.0;

        [Category("8. Advanced")]
        [DisplayName("Typical P")]
        [Description("Locally typical sampling (1.0 = disabled)")]
        public double TypicalP { get; set; } = 1.0;

        // ==================== SYSTEM PROMPT ====================
        [Category("9. System Prompt")]
        [DisplayName("System Prompt")]
        [Description("System instructions defining model behavior")]
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
- ALWAYS answer in the language the user is using
- If you don't know something, admit it honestly
- Ask for clarification if needed
- Consider Visual Studio and .NET context when relevant";

        // Get stop tokens as array
        public string[] GetStopTokensArray()
        {
            if (string.IsNullOrWhiteSpace(StopTokens))
                return new string[0];

            return StopTokens.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        // Get keep_alive value in Ollama format
        public string GetKeepAliveValue()
        {
            if (!KeepAlive) return "0";
            if (KeepAliveSeconds < 0) return "-1";
            return KeepAliveSeconds + "s";
        }
    }

    /// <summary>
    /// Custom editor for large multiline text
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
                form.Text = "Edit System Prompt";
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
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new System.Drawing.Size(90, 30),
                    Location = new System.Drawing.Point(490, 8)
                };
                okButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

                var cancelButton = new System.Windows.Forms.Button
                {
                    Text = "Cancel",
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