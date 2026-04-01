🦙 Ollama for Visual Studio
A Visual Studio 2022/2026 extension that integrates local AI models from Ollama directly into your IDE. Chat with models like Llama 3, CodeLlama, DeepSeek Coder, and more, all running locally on your machine.

Visual Studio Marketplace Version
License
Platform

Download it from visual studio marketplace

✨ Features
🤖 Local AI Chat - Use Ollama models without sending code to the cloud
📎 Attach Files - Include files from your project as context
🎨 Modern Interface - Design similar to GitHub Copilot Chat
🌙 Adaptive Theme - Adapts to Visual Studio's light/dark theme
⚡ Real-time Streaming - See responses as they are generated
🔧 Configurable - Customize the URL, model, and system prompt
📋 Requirements
Required Software
Requirement
Version
Link
Visual Studio	2022 or 2026	Download
Ollama	Latest version	Download
.NET Framework	4.8	Included in Windows 10/11

Recommended Ollama models for coding
bash

ollama pull codellama
ollama pull deepseek-coder-v2:16b
ollama pull qwen2.5-coder:7b
General models
bash

ollama pull llama3:8b
ollama pull llama3.1
🚀 Installation
Option 1: From GitHub Releases
Go to Releases
Download the OllamaForVisualStudio.vsix file
Double-click the file to install
Restart Visual Studio
Option 2: Build from source
Clone the repository
bash

git clone https://github.com/anubisma/OllamaForVisualStudio.git
cd OllamaForVisualStudio
Open in Visual Studio and build (F5 for debug, Ctrl+Shift+B for release).

🎯 Usage
1. Start Ollama
Before using the extension, make sure Ollama is running:

bash

ollama serve
2. Open Ollama Chat
In Visual Studio, go to:

View > Ollama Chat

Or use Quick Launch (Ctrl+Q) and type "Ollama Chat".

3. Select a model
Use the dropdown at the top to select the model you want to use.

4. Chat
Type your question and press Send or Ctrl+Enter.

5. Attach files (optional)
Click 📎 Attach or press Ctrl+Shift+A to include files from your project as context.

⚙️ Configuration
Go to Tools > Options > Ollama to configure:

Option
Description
Default Value
Ollama URL	Ollama server address	http://localhost:11434
Selected Model	Model to use by default	llama3
System Prompt	Custom instructions for the model	Programming Assistant
Max Tokens	Token limit in responses	2048

🛠️ Development
Development Requirements
Visual Studio 2022/2026 with the "Visual Studio extension development" workload
.NET Framework 4.8 SDK
