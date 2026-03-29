# 🦙 Ollama for Visual Studio

Una extensión para Visual Studio 2022/2026 que integra modelos de IA locales de [Ollama](https://ollama.ai) directamente en tu IDE. Chatea con modelos como Llama 3, CodeLlama, DeepSeek Coder y más, todo ejecutándose localmente en tu máquina.

![Visual Studio Marketplace Version](https://img.shields.io/badge/VS-2022%20%7C%202026-purple)
![License](https://img.shields.io/badge/license-MIT-blue)
![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)

---

## ✨ Características

- 🤖 **Chat con IA local** - Usa modelos de Ollama sin enviar código a la nube
- 📎 **Adjuntar archivos** - Incluye archivos de tu proyecto como contexto
- 🎨 **Interfaz moderna** - Diseño similar a GitHub Copilot Chat
- 🌙 **Tema adaptativo** - Se adapta al tema claro/oscuro de Visual Studio
- ⚡ **Streaming en tiempo real** - Ve las respuestas mientras se generan
- 🔧 **Configurable** - Personaliza la URL, modelo y system prompt

---

## 📋 Requisitos

### Software necesario

| Requisito | Versión | Enlace |
|-----------|---------|--------|
| Visual Studio | 2022 o 2026 | [Descargar](https://visualstudio.microsoft.com/) |
| Ollama | Última versión | [Descargar](https://ollama.ai/download) |
| .NET Framework | 4.8 | Incluido en Windows 10/11 |

### Modelos de Ollama recomendados para programación
ollama pull codellama ollama pull deepseek-coder-v2:16b ollama pull qwen2.5-coder:7b

### Modelos generales
ollama pull llama3:8b ollama pull llama3.1

---

## 🚀 Instalación

### Opción 1: Desde GitHub Releases

1. Ve a [Releases](https://github.com/anubisma/OllamaForVisualStudio/releases)
2. Descarga el archivo `OllamaForVisualStudio.vsix`
3. Doble clic en el archivo para instalar
4. Reinicia Visual Studio

### Opción 2: Compilar desde código fuente
## Clonar el repositorio

git clone https://github.com/anubisma/OllamaForVisualStudio.git

cd OllamaForVisualStudio

Abrir en Visual Studio y compilar (F5 para debug, Ctrl+Shift+B para release)


---

## 🎯 Uso

### 1. Iniciar Ollama

Antes de usar la extensión, asegúrate de que Ollama esté ejecutándose:
ollama serve


### 2. Abrir Ollama Chat

En Visual Studio, ve a:

**View > Ollama Chat**

O usa la búsqueda rápida (`Ctrl+Q`) y escribe "Ollama Chat".

### 3. Seleccionar un modelo

Usa el desplegable en la parte superior para seleccionar el modelo que quieres usar.

### 4. Chatear

Escribe tu pregunta y presiona **Enviar** o `Ctrl+Enter`.

### 5. Adjuntar archivos (opcional)

Haz clic en **📎 Adjuntar** o presiona `Ctrl+Shift+A` para incluir archivos de tu proyecto como contexto.

---

## ⚙️ Configuración

Ve a **Tools > Options > Ollama** para configurar:

| Opción | Descripción | Valor por defecto |
|--------|-------------|-------------------|
| **URL de Ollama** | Dirección del servidor Ollama | `http://localhost:11434` |
| **Modelo seleccionado** | Modelo a usar por defecto | `llama3` |
| **System Prompt** | Instrucciones personalizadas para el modelo | Asistente de programación |
| **Máximo de tokens** | Límite de tokens en respuestas | `2048` |

---

## 🛠️ Desarrollo

### Requisitos de desarrollo

- Visual Studio 2022/2026 con la carga de trabajo **"Desarrollo de extensiones de Visual Studio"**
- .NET Framework 4.8 SDK
