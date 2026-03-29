using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using OllamaChatExtension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OllamaForVisualStudio
{
    public partial class OllamaChatControl : UserControl
    {
        private readonly OllamaApiClient _apiClient = new OllamaApiClient();
        private CancellationTokenSource _cancellationTokenSource;
        private TextBlock _currentAssistantMessage;
        private readonly List<AttachedFile> _attachedFiles = new List<AttachedFile>();

        public OllamaChatControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            UserInput.TextChanged += UserInput_TextChanged;
        }

        private void UserInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            PlaceholderText.Visibility = string.IsNullOrEmpty(UserInput.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UserInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SendButton_Click(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.A && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                AttachFileButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await LoadSettingsAndModelsAsync();
            AddWelcomeMessage();
        }

        private void AddWelcomeMessage()
        {
            AddAssistantMessage("¡Hola! Soy tu asistente de programación.\n\n" +
                "💡 **Tips:**\n" +
                "• Usa **📎 Adjuntar** para incluir archivos del proyecto\n" +
                "• Presiona **Ctrl+Enter** para enviar\n" +
                "• Presiona **Ctrl+Shift+A** para adjuntar archivos");
        }

        private async Task LoadSettingsAndModelsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            string ollamaUrl = "http://localhost:11434";
            string selectedModel = "";

            try
            {
                var package = Package.GetGlobalService(typeof(OllamaChatPackage)) as OllamaChatPackage;
                if (package != null)
                {
                    var options = package.GetDialogPage(typeof(OllamaOptions)) as OllamaOptions;
                    if (options != null)
                    {
                        ollamaUrl = options.OllamaUrl;
                        selectedModel = options.SelectedModel;
                    }
                }
            }
            catch { }

            _apiClient.SetBaseUrl(ollamaUrl);

            try
            {
                var models = await _apiClient.GetAvailableModelsAsync();

                if (models.Count == 0)
                {
                    AddSystemMessage("⚠️ No se encontraron modelos. Verifica que Ollama esté ejecutándose.");
                    return;
                }

                ModelComboBox.ItemsSource = models;

                if (!string.IsNullOrEmpty(selectedModel) && models.Contains(selectedModel))
                    ModelComboBox.SelectedItem = selectedModel;
                else if (models.Count > 0)
                    ModelComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                AddSystemMessage(string.Format("❌ Error conectando a Ollama: {0}", ex.Message));
            }
        }

        private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadSettingsAndModelsAsync();
        }

        #region Adjuntar Archivos

        private void AttachFileButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            try
            {
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                if (dte == null)
                {
                    AddSystemMessage("⚠️ No se pudo acceder al entorno de Visual Studio.");
                    return;
                }

                // Mostrar diálogo para seleccionar archivos del proyecto
                var dialog = new AttachFileDialog(dte);
                if (dialog.ShowDialog() == true && dialog.SelectedFiles.Count > 0)
                {
                    foreach (var file in dialog.SelectedFiles)
                    {
                        AddAttachedFile(file);
                    }
                }
            }
            catch (Exception ex)
            {
                // Si falla el diálogo personalizado, usar el archivo activo
                try
                {
                    var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                    if (dte != null && dte.ActiveDocument != null)
                    {
                        var filePath = dte.ActiveDocument.FullName;
                        AddAttachedFile(filePath);
                    }
                }
                catch
                {
                    AddSystemMessage(string.Format("❌ Error: {0}", ex.Message));
                }
            }
        }

        private void AddAttachedFile(string filePath)
        {
            if (_attachedFiles.Any(f => f.FilePath == filePath))
            {
                AddSystemMessage(string.Format("ℹ️ El archivo ya está adjunto: {0}", Path.GetFileName(filePath)));
                return;
            }

            try
            {
                var content = File.ReadAllText(filePath);
                var fileName = Path.GetFileName(filePath);

                var attachedFile = new AttachedFile
                {
                    FilePath = filePath,
                    FileName = fileName,
                    Content = content
                };

                _attachedFiles.Add(attachedFile);
                UpdateAttachmentsUI();
                AddSystemMessage(string.Format("📎 Archivo adjunto: {0}", fileName));
            }
            catch (Exception ex)
            {
                AddSystemMessage(string.Format("❌ Error leyendo archivo: {0}", ex.Message));
            }
        }

        private void UpdateAttachmentsUI()
        {
            AttachedFilesList.Items.Clear();

            if (_attachedFiles.Count == 0)
            {
                AttachmentsPanel.Visibility = Visibility.Collapsed;
                return;
            }

            AttachmentsPanel.Visibility = Visibility.Visible;

            foreach (var file in _attachedFiles)
            {
                var chip = CreateFileChip(file);
                AttachedFilesList.Items.Add(chip);
            }
        }

        private Border CreateFileChip(AttachedFile file)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 65)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(10, 4, 6, 4),
                Margin = new Thickness(0, 0, 6, 4)
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            var icon = new TextBlock
            {
                Text = GetFileIcon(file.FileName),
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var name = new TextBlock
            {
                Text = file.FileName,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };

            var removeButton = new Button
            {
                Content = "✕",
                Background = Brushes.Transparent,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                FontSize = 10,
                Tag = file
            };
            removeButton.Click += RemoveAttachment_Click;

            stack.Children.Add(icon);
            stack.Children.Add(name);
            stack.Children.Add(removeButton);
            border.Child = stack;

            return border;
        }

        private string GetFileIcon(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            switch (ext)
            {
                case ".cs": return "🔷";
                case ".xaml": return "🎨";
                case ".json": return "📋";
                case ".xml": return "📄";
                case ".js": case ".ts": return "🟨";
                case ".html": return "🌐";
                case ".css": return "🎭";
                case ".sql": return "🗃️";
                case ".md": return "📝";
                default: return "📄";
            }
        }

        private void RemoveAttachment_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var file = button?.Tag as AttachedFile;
            if (file != null)
            {
                _attachedFiles.Remove(file);
                UpdateAttachmentsUI();
            }
        }

        #endregion

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var userMessage = UserInput.Text.Trim();
            if (string.IsNullOrEmpty(userMessage)) return;

            var selectedModel = ModelComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedModel))
            {
                AddSystemMessage("⚠️ Selecciona un modelo primero.");
                return;
            }

            UserInput.Clear();

            var fullMessage = BuildMessageWithContext(userMessage);
            AddUserMessage(userMessage, _attachedFiles.Select(f => f.FileName).ToList());

            // Mostrar indicador de "pensando"
            var thinkingBlock = CreateThinkingIndicator();

            _cancellationTokenSource = new CancellationTokenSource();
            SetUIState(isProcessing: true);

            bool firstChunkReceived = false;

            try
            {
                await Task.Run(async () =>
                {
                    await _apiClient.StreamChatAsync(
                        selectedModel,
                        fullMessage,
                        GetSystemPrompt(),
                        chunk =>
                        {
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                // Reemplazar indicador de pensando con el bloque de respuesta al recibir el primer chunk
                                if (!firstChunkReceived)
                                {
                                    firstChunkReceived = true;
                                    MessagesPanel.Items.Remove(thinkingBlock);
                                    _currentAssistantMessage = CreateAssistantMessageBlock();
                                }
                                AppendToCurrentMessage(chunk);
                            }));
                        },
                        _cancellationTokenSource.Token);
                });
            }
            catch (OperationCanceledException)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (!firstChunkReceived)
                    {
                        MessagesPanel.Items.Remove(thinkingBlock);
                        _currentAssistantMessage = CreateAssistantMessageBlock();
                    }
                    AppendToCurrentMessage("\n\n[Cancelado]");
                }));
            }
            catch (Exception ex)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (MessagesPanel.Items.Contains(thinkingBlock))
                    {
                        MessagesPanel.Items.Remove(thinkingBlock);
                    }

                    if (_currentAssistantMessage == null)
                    {
                        _currentAssistantMessage = CreateAssistantMessageBlock();
                    }
                    AppendToCurrentMessage(string.Format("❌ Error: {0}", ex.Message));
                }));
            }
            finally
            {
                SetUIState(isProcessing: false);
                _currentAssistantMessage = null;

                _attachedFiles.Clear();
                UpdateAttachmentsUI();

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }
        private Border CreateThinkingIndicator()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                CornerRadius = new CornerRadius(12, 12, 12, 0),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 8, 40, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            var dots = new TextBlock
            {
                Text = "🤖 Pensando",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontStyle = FontStyles.Italic
            };

            // Animación simple de puntos
            var dotsAnimation = new TextBlock
            {
                Text = "...",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13
            };

            stack.Children.Add(dots);
            stack.Children.Add(dotsAnimation);
            border.Child = stack;

            MessagesPanel.Items.Add(border);
            ScrollToBottom();

            return border;
        }
        private string BuildMessageWithContext(string userMessage)
        {
            if (_attachedFiles.Count == 0)
                return userMessage;

            var contextBuilder = new System.Text.StringBuilder();
            contextBuilder.AppendLine("Contexto de archivos adjuntos:");
            contextBuilder.AppendLine();

            foreach (var file in _attachedFiles)
            {
                contextBuilder.AppendLine(string.Format("--- Archivo: {0} ---", file.FileName));
                contextBuilder.AppendLine(file.Content);
                contextBuilder.AppendLine();
            }

            contextBuilder.AppendLine("--- Pregunta del usuario ---");
            contextBuilder.AppendLine(userMessage);

            return contextBuilder.ToString();
        }

        private void AddUserMessage(string message, List<string> attachedFileNames)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(14, 99, 156)),
                CornerRadius = new CornerRadius(12, 12, 0, 12),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(40, 8, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var stack = new StackPanel();

            var header = new TextBlock
            {
                Text = "👤 Tú",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4),
                Opacity = 0.8
            };

            stack.Children.Add(header);

            // Mostrar archivos adjuntos
            if (attachedFileNames != null && attachedFileNames.Count > 0)
            {
                var filesText = new TextBlock
                {
                    Text = "📎 " + string.Join(", ", attachedFileNames),
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 220, 255)),
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 0, 0, 6),
                    TextWrapping = TextWrapping.Wrap
                };
                stack.Children.Add(filesText);
            }

            var content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13
            };

            stack.Children.Add(content);
            border.Child = stack;

            MessagesPanel.Items.Add(border);
            ScrollToBottom();
        }

        private void AddAssistantMessage(string message)
        {
            var block = CreateAssistantMessageBlock();
            block.Text = message;
        }

        private TextBlock CreateAssistantMessageBlock()
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(45, 45, 48)),
                CornerRadius = new CornerRadius(12, 12, 12, 0),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 8, 40, 8),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var stack = new StackPanel();

            var header = new TextBlock
            {
                Text = string.Format("🤖 {0}", ModelComboBox.SelectedItem ?? "Ollama"),
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 149, 237)),
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var content = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13
            };

            stack.Children.Add(header);
            stack.Children.Add(content);
            border.Child = stack;

            MessagesPanel.Items.Add(border);
            ScrollToBottom();

            return content;
        }

        private void AddSystemMessage(string message)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(20, 8, 20, 8),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var content = new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                TextAlignment = TextAlignment.Center
            };

            border.Child = content;
            MessagesPanel.Items.Add(border);
            ScrollToBottom();
        }

        private void AppendToCurrentMessage(string text)
        {
            if (_currentAssistantMessage != null)
            {
                _currentAssistantMessage.Text += text;
                ScrollToBottom();
            }
        }

        private void ScrollToBottom()
        {
            ChatScrollViewer.ScrollToEnd();
        }

        private static string GetSystemPrompt()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var package = Package.GetGlobalService(typeof(OllamaChatPackage)) as OllamaChatPackage;
                if (package != null)
                {
                    var options = package.GetDialogPage(typeof(OllamaOptions)) as OllamaOptions;
                    if (options != null)
                    {
                        return options.SystemPrompt;
                    }
                }
            }
            catch { }
            return "Eres un asistente de programación experto. Responde de forma clara y concisa. Cuando analices código, proporciona sugerencias útiles.";
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            MessagesPanel.Items.Clear();
            _attachedFiles.Clear();
            UpdateAttachmentsUI();
            AddWelcomeMessage();
        }

        private void SetUIState(bool isProcessing)
        {
            SendButton.IsEnabled = !isProcessing;
            SendButton.Visibility = isProcessing ? Visibility.Collapsed : Visibility.Visible;
            StopButton.IsEnabled = isProcessing;
            StopButton.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            UserInput.IsEnabled = !isProcessing;
            AttachFileButton.IsEnabled = !isProcessing;
        }
    }

    public class AttachedFile
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Content { get; set; }
    }
}