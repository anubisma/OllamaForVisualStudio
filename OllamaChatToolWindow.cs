using Microsoft.VisualStudio.Shell;
using System.Runtime.InteropServices;

namespace OllamaForVisualStudio
{
    [Guid("A1B2C3D4-E5F6-7890-ABCD-EF1234567890")]
    public class OllamaChatToolWindow : ToolWindowPane
    {
        public OllamaChatToolWindow() : base(null)
        {
            Caption = "Ollama Chat";
            Content = new OllamaChatControl();
        }
    }
}