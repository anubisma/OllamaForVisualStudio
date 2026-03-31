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
using System.Windows.Documents;

namespace OllamaForVisualStudio
{
    public partial class OllamaChatControl : UserControl
    {
        private readonly OllamaApiClient _apiClient = new OllamaApiClient();
        private CancellationTokenSource _cancellationTokenSource;
        private TextBlock _currentAssistantMessage;
        private readonly List<AttachedFile> _attachedFiles = new List<AttachedFile>();
        private StackPanel _currentAssistantContentPanel;
        private string _currentAssistantText = "";

        // Autocompletado de archivos
        private List<ProjectFile> _allProjectFiles = new List<ProjectFile>();
        private int _hashPosition = -1;
        private bool _isAutocompleteActive = false;
        private bool _isInitialized = false; // Nueva variable

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

            // Detectar # para autocompletado
            HandleHashAutocomplete();
        }

        #region Autocompletado con #

        private void HandleHashAutocomplete()
        {
            var text = UserInput.Text;
            var caretIndex = UserInput.CaretIndex;

            if (string.IsNullOrEmpty(text) || caretIndex == 0)
            {
                FileAutocompletePopup.IsOpen = false;
                return;
            }

            // Buscar el último # antes del cursor
            int hashPos = -1;
            for (int i = caretIndex - 1; i >= 0; i--)
            {
                char c = text[i];
                if (c == '#')
                {
                    // Verificar que el # esté al inicio o después de un espacio/nueva línea
                    if (i == 0 || text[i - 1] == ' ' || text[i - 1] == '\n' || text[i - 1] == '\r')
                    {
                        hashPos = i;
                    }
                    break;
                }
                // Si encontramos espacio o nueva línea, no hay # activo
                if (c == ' ' || c == '\n' || c == '\r')
                {
                    break;
                }
            }

            if (hashPos >= 0)
            {
                _hashPosition = hashPos;
                _isAutocompleteActive = true;

                // Recargar archivos si la lista está vacía
                if (_allProjectFiles.Count == 0)
                {
                    LoadProjectFiles();
                }

                // Extraer el filtro (texto después del #)
                var filter = text.Substring(hashPos + 1, caretIndex - hashPos - 1).ToLower().Trim();

                // Filtrar archivos
                List<ProjectFile> filteredFiles;
                if (string.IsNullOrEmpty(filter))
                {
                    // Mostrar todos los archivos si solo escribió #
                    filteredFiles = _allProjectFiles.Take(15).ToList();
                }
                else
                {
                    filteredFiles = _allProjectFiles
                        .Where(f => f.FileName.ToLower().StartsWith(filter) ||
                                    f.FileName.ToLower().Contains(filter))
                        .OrderBy(f => !f.FileName.ToLower().StartsWith(filter))
                        .ThenBy(f => f.FileName)
                        .Take(15)
                        .ToList();
                }

                if (filteredFiles.Count > 0)
                {
                    FileAutocompleteList.ItemsSource = filteredFiles;
                    FileAutocompleteList.SelectedIndex = 0;
                    FileAutocompletePopup.IsOpen = true;
                }
                else
                {
                    FileAutocompletePopup.IsOpen = false;
                }
            }
            else
            {
                _isAutocompleteActive = false;
                _hashPosition = -1;
                FileAutocompletePopup.IsOpen = false;
            }
        }

        private void UserInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!FileAutocompletePopup.IsOpen) return;

            switch (e.Key)
            {
                case Key.Down:
                    if (FileAutocompleteList.SelectedIndex < FileAutocompleteList.Items.Count - 1)
                    {
                        FileAutocompleteList.SelectedIndex++;
                        FileAutocompleteList.ScrollIntoView(FileAutocompleteList.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Up:
                    if (FileAutocompleteList.SelectedIndex > 0)
                    {
                        FileAutocompleteList.SelectedIndex--;
                        FileAutocompleteList.ScrollIntoView(FileAutocompleteList.SelectedItem);
                    }
                    e.Handled = true;
                    break;

                case Key.Enter:
                case Key.Tab:
                    if (FileAutocompleteList.SelectedItem != null)
                    {
                        SelectAutocompleteFile(FileAutocompleteList.SelectedItem as ProjectFile);
                        e.Handled = true;
                    }
                    break;

                case Key.Escape:
                    FileAutocompletePopup.IsOpen = false;
                    _isAutocompleteActive = false;
                    e.Handled = true;
                    break;
            }
        }

        private void FileAutocompleteList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Solo para feedback visual
        }

        private void FileAutocompleteList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FileAutocompleteList.SelectedItem != null)
            {
                SelectAutocompleteFile(FileAutocompleteList.SelectedItem as ProjectFile);
            }
        }

        private void SelectAutocompleteFile(ProjectFile file)
        {
            if (file == null || _hashPosition < 0) return;

            // Cerrar popup
            FileAutocompletePopup.IsOpen = false;
            _isAutocompleteActive = false;

            // Reemplazar #filtro con #nombrearchivo
            var text = UserInput.Text;
            var caretIndex = UserInput.CaretIndex;
            var beforeHash = text.Substring(0, _hashPosition);
            var afterCaret = caretIndex < text.Length ? text.Substring(caretIndex) : "";

            // Insertar el nombre del archivo
            UserInput.Text = beforeHash + "#" + file.FileName + " " + afterCaret.TrimStart();
            UserInput.CaretIndex = _hashPosition + file.FileName.Length + 2;

            // Adjuntar el archivo
            AddAttachedFile(file.FullPath);

            _hashPosition = -1;
            UserInput.Focus();
        }

        private void LoadProjectFiles()
        {
            _allProjectFiles.Clear();

            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = Package.GetGlobalService(typeof(DTE)) as DTE2;
                if (dte == null)
                {
                    System.Diagnostics.Debug.WriteLine("DTE is null - cannot load project files");
                    return;
                }

                var files = new List<ProjectFile>();
                var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".cs", ".xaml", ".json", ".xml", ".js", ".ts", ".html", ".css",
                    ".sql", ".md", ".txt", ".config", ".csproj", ".sln", ".razor",
                    ".cshtml", ".vb", ".fs", ".py", ".java", ".cpp", ".h", ".c"
                };

                // Agregar archivo activo primero
                try
                {
                    if (dte.ActiveDocument != null)
                    {
                        var path = dte.ActiveDocument.FullName;
                        if (File.Exists(path))
                        {
                            var ext = Path.GetExtension(path).ToLower();
                            if (extensions.Contains(ext))
                            {
                                files.Add(new ProjectFile
                                {
                                    FullPath = path,
                                    FileName = Path.GetFileName(path),
                                    Icon = GetFileIcon(path)
                                });
                            }
                        }
                    }
                }
                catch { }

                // Agregar archivos de la solución
                try
                {
                    if (dte.Solution != null && dte.Solution.Projects != null)
                    {
                        foreach (Project project in dte.Solution.Projects)
                        {
                            try
                            {
                                if (project.ProjectItems != null)
                                {
                                    AddProjectFilesRecursive(project.ProjectItems, files, extensions);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }

                // También buscar por directorio de la solución como fallback
                try
                {
                    if (files.Count == 0 && dte.Solution != null && !string.IsNullOrEmpty(dte.Solution.FullName))
                    {
                        var solutionDir = Path.GetDirectoryName(dte.Solution.FullName);
                        if (Directory.Exists(solutionDir))
                        {
                            foreach (var ext in extensions)
                            {
                                try
                                {
                                    var foundFiles = Directory.GetFiles(solutionDir, "*" + ext, SearchOption.AllDirectories);
                                    foreach (var filePath in foundFiles.Take(100)) // Limitar a 100 por extensión
                                    {
                                        // Excluir carpetas bin, obj, packages, node_modules, .vs
                                        if (filePath.Contains("\\bin\\") || filePath.Contains("\\obj\\") ||
                                            filePath.Contains("\\packages\\") || filePath.Contains("\\node_modules\\") ||
                                            filePath.Contains("\\.vs\\") || filePath.Contains("\\.git\\"))
                                            continue;

                                        files.Add(new ProjectFile
                                        {
                                            FullPath = filePath,
                                            FileName = Path.GetFileName(filePath),
                                            Icon = GetFileIcon(filePath)
                                        });
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }
                catch { }

                // Ordenar y quitar duplicados
                _allProjectFiles = files
                    .GroupBy(f => f.FullPath.ToLower())
                    .Select(g => g.First())
                    .OrderBy(f => f.FileName)
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"Loaded {_allProjectFiles.Count} project files");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading project files: {ex.Message}");
            }
        }

        private void AddProjectFilesRecursive(ProjectItems items, List<ProjectFile> files, HashSet<string> extensions)
        {
            if (items == null) return;

            try
            {
                foreach (ProjectItem item in items)
                {
                    try
                    {
                        // Intentar obtener el archivo
                        for (short i = 1; i <= item.FileCount; i++)
                        {
                            try
                            {
                                var filePath = item.FileNames[i];
                                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                                {
                                    var ext = Path.GetExtension(filePath);
                                    if (extensions.Contains(ext))
                                    {
                                        files.Add(new ProjectFile
                                        {
                                            FullPath = filePath,
                                            FileName = Path.GetFileName(filePath),
                                            Icon = GetFileIcon(filePath)
                                        });
                                    }
                                }
                            }
                            catch { }
                        }

                        // Recurrir en subcarpetas/items anidados
                        if (item.ProjectItems != null && item.ProjectItems.Count > 0)
                        {
                            AddProjectFilesRecursive(item.ProjectItems, files, extensions);
                        }

                        // También verificar SubProject (para solution folders)
                        try
                        {
                            if (item.SubProject != null && item.SubProject.ProjectItems != null)
                            {
                                AddProjectFilesRecursive(item.SubProject.ProjectItems, files, extensions);
                            }
                        }
                        catch { }
                    }
                    catch { }
                }
            }
            catch { }
        }

        #endregion

        private void UserInput_KeyDown(object sender, KeyEventArgs e)
        {
            // Si el popup está abierto, no procesar Ctrl+Enter
            if (FileAutocompletePopup.IsOpen) return;

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SendButton_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Solo inicializar la primera vez
            if (_isInitialized) return;
            _isInitialized = true;

            await LoadSettingsAndModelsAsync();

            // Cargar archivos del proyecto en el hilo UI
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            LoadProjectFiles();

            AddWelcomeMessage();
        }

        private void AddWelcomeMessage()
        {
            AddAssistantMessage("Hello! I'm your programming assistant.");
        }

        private async Task LoadSettingsAndModelsAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            string ollamaUrl = "http://localhost:11434";
            string selectedModel = "";
            int timeoutSeconds = 300;

            try
            {
                var options = GetOllamaOptions();
                if (options != null)
                {
                    ollamaUrl = options.OllamaUrl;
                    selectedModel = options.SelectedModel;
                    timeoutSeconds = options.TimeoutSeconds;
                }
            }
            catch { }

            _apiClient.SetBaseUrl(ollamaUrl);
            _apiClient.SetTimeout(timeoutSeconds);

            try
            {
                var models = await _apiClient.GetAvailableModelsAsync();

                if (models.Count == 0)
                {
                    AddSystemMessage("⚠️ No models found. Make sure Ollama is running.");
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
                AddSystemMessage(string.Format("❌ Error connecting to Ollama: {0}", ex.Message));
            }
        }

        private OllamaOptions GetOllamaOptions()
        {
            try
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var package = Package.GetGlobalService(typeof(OllamaChatPackage)) as OllamaChatPackage;
                if (package != null)
                {
                    return package.GetDialogPage(typeof(OllamaOptions)) as OllamaOptions;
                }
            }
            catch { }
            return null;
        }

        private OllamaGenerationOptions GetGenerationOptions()
        {
            var options = GetOllamaOptions();
            if (options != null)
            {
                return new OllamaGenerationOptions
                {
                    MaxTokens = options.MaxTokens,
                    ContextSize = options.ContextSize,
                    Seed = options.Seed,
                    NumThreads = options.NumThreads,
                    NumGpu = options.NumGpu,
                    Temperature = options.Temperature,
                    TopP = options.TopP,
                    TopK = options.TopK,
                    MinP = options.MinP,
                    RepeatPenalty = options.RepeatPenalty,
                    RepeatLastN = options.RepeatLastN,
                    PresencePenalty = options.PresencePenalty,
                    FrequencyPenalty = options.FrequencyPenalty,
                    Mirostat = options.Mirostat,
                    MirostatTau = options.MirostatTau,
                    MirostatEta = options.MirostatEta,
                    TfsZ = options.TfsZ,
                    TypicalP = options.TypicalP,
                    PenalizeNewline = options.PenalizeNewline,
                    StopTokens = options.GetStopTokensArray(),
                    KeepConversationHistory = options.KeepConversationHistory,
                    MaxHistoryMessages = options.MaxHistoryMessages,
                    KeepAlive = options.KeepAlive,
                    KeepAliveSeconds = options.KeepAliveSeconds
                };
            }
            return new OllamaGenerationOptions();
        }

        private async void RefreshModelsButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadSettingsAndModelsAsync();
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            LoadProjectFiles();
            AddSystemMessage("🔄 Models and files reloaded");
        }

        #region Archivos Adjuntos

        private void AddAttachedFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;

            if (_attachedFiles.Any(f => f.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase)))
            {
                return; // Ya está adjunto
            }

            try
            {
                if (!File.Exists(filePath))
                {
                    AddSystemMessage(string.Format("⚠️ File not found: {0}", Path.GetFileName(filePath)));
                    return;
                }

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

                System.Diagnostics.Debug.WriteLine($"Attached file: {fileName}");
            }
            catch (Exception ex)
            {
                AddSystemMessage(string.Format("❌ Error reading file: {0}", ex.Message));
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
                case ".xml": case ".config": return "📄";
                case ".js": case ".ts": return "🟨";
                case ".html": case ".cshtml": case ".razor": return "🌐";
                case ".css": return "🎭";
                case ".sql": return "🗃️";
                case ".md": return "📝";
                case ".csproj": case ".sln": return "📦";
                case ".py": return "🐍";
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

                // También remover la mención del texto
                var mention = "#" + file.FileName;
                if (UserInput.Text.Contains(mention))
                {
                    UserInput.Text = UserInput.Text.Replace(mention + " ", "").Replace(mention, "").Trim();
                }
            }
        }

        #endregion

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            var userMessage = UserInput.Text.Trim();
            if (string.IsNullOrEmpty(userMessage) && _attachedFiles.Count == 0) return;

            var selectedModel = ModelComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedModel))
            {
                AddSystemMessage("⚠️ Select a model first.");
                return;
            }

            // Limpiar menciones de archivos del mensaje visible
            var displayMessage = CleanFileReferences(userMessage);

            UserInput.Clear();

            var fullMessage = BuildMessageWithContext(userMessage);
            AddUserMessage(displayMessage, _attachedFiles.Select(f => f.FileName).ToList());

            var thinkingBlock = CreateThinkingIndicator();

            _cancellationTokenSource = new CancellationTokenSource();
            SetUIState(isProcessing: true);

            // Guardar archivos adjuntos para el mensaje y luego limpiar
            var attachedFilesCopy = _attachedFiles.ToList();
            _attachedFiles.Clear();
            UpdateAttachmentsUI();

            bool firstChunkReceived = false;
            string finalText = "";
            var generationOptions = GetGenerationOptions();
            var systemPrompt = GetSystemPrompt();

            try
            {
                await Task.Run(async () =>
                {
                    await _apiClient.StreamChatAsync(
                        selectedModel,
                        fullMessage,
                        systemPrompt,
                        generationOptions,
                        chunk =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (!firstChunkReceived)
                                {
                                    firstChunkReceived = true;
                                    MessagesPanel.Items.Remove(thinkingBlock);
                                    CreateAssistantMessageBlock();
                                }
                                AppendToCurrentMessage(chunk);
                            });
                        },
                        _cancellationTokenSource.Token);
                });

                finalText = _currentAssistantText;
            }
            catch (OperationCanceledException)
            {
                finalText = _currentAssistantText + "\n\n[Cancelado]";
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    if (MessagesPanel.Items.Contains(thinkingBlock))
                    {
                        MessagesPanel.Items.Remove(thinkingBlock);
                    }
                    if (_currentAssistantContentPanel == null)
                    {
                        CreateAssistantMessageBlock();
                    }
                });
                finalText = _currentAssistantText + string.Format("\n\n❌ Error: {0}", ex.Message);
            }
            finally
            {
                if (_currentAssistantContentPanel != null && !string.IsNullOrEmpty(finalText))
                {
                    _currentAssistantContentPanel.Children.Clear();

                    var markdownContent = MarkdownRenderer.RenderMarkdown(finalText);

                    var children = new List<UIElement>();
                    foreach (UIElement child in markdownContent.Children)
                    {
                        children.Add(child);
                    }

                    foreach (var child in children)
                    {
                        markdownContent.Children.Remove(child);
                        _currentAssistantContentPanel.Children.Add(child);
                    }

                    ScrollToBottom();
                }

                SetUIState(isProcessing: false);
                _currentAssistantContentPanel = null;
                _currentAssistantText = "";

                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }

        private string CleanFileReferences(string text)
        {
            var result = text;
            foreach (var file in _attachedFiles)
            {
                result = result.Replace("#" + file.FileName, "").Trim();
            }
            while (result.Contains("  "))
            {
                result = result.Replace("  ", " ");
            }
            return result.Trim();
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
                Text = "🤖 Thinking...",
                Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontStyle = FontStyles.Italic
            };

            stack.Children.Add(dots);
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
            contextBuilder.AppendLine("Context files:");
            contextBuilder.AppendLine();

            foreach (var file in _attachedFiles)
            {
                contextBuilder.AppendLine(string.Format("=== {0} ===", file.FileName));
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine(file.Content);
                contextBuilder.AppendLine("```");
                contextBuilder.AppendLine();
            }

            contextBuilder.AppendLine("User question:");
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
                Text = string.IsNullOrWhiteSpace(message) ? "(archivos adjuntos)" : message,
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
            CreateAssistantMessageBlock();
            _currentAssistantText = message;
            FinalizeCurrentMessage();
        }

        private StackPanel CreateAssistantMessageBlock()
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

            var contentPanel = new StackPanel();

            stack.Children.Add(header);
            stack.Children.Add(contentPanel);
            border.Child = stack;

            MessagesPanel.Items.Add(border);
            ScrollToBottom();

            _currentAssistantText = "";
            _currentAssistantContentPanel = contentPanel;

            return contentPanel;
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
            if (_currentAssistantContentPanel != null)
            {
                _currentAssistantText += text;

                _currentAssistantContentPanel.Children.Clear();
                var tempText = new TextBox
                {
                    Text = _currentAssistantText,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    IsReadOnly = true,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 13
                };
                _currentAssistantContentPanel.Children.Add(tempText);
                ScrollToBottom();
            }
        }

        private void FinalizeCurrentMessage()
        {
            if (_currentAssistantContentPanel != null && !string.IsNullOrEmpty(_currentAssistantText))
            {
                _currentAssistantContentPanel.Children.Clear();

                var markdownContent = MarkdownRenderer.RenderMarkdown(_currentAssistantText);

                var children = new List<UIElement>();
                foreach (UIElement child in markdownContent.Children)
                {
                    children.Add(child);
                }

                foreach (var child in children)
                {
                    markdownContent.Children.Remove(child);
                    _currentAssistantContentPanel.Children.Add(child);
                }

                ScrollToBottom();
            }
        }

        private void ScrollToBottom()
        {
            ChatScrollViewer.ScrollToEnd();
        }

        private string GetSystemPrompt()
        {
            var options = GetOllamaOptions();
            if (options != null)
            {
                return options.SystemPrompt;
            }
            return "You are a programming expert assistant. Respond clearly and concisely.";
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
            _apiClient.ClearHistory();

            // Recargar archivos del proyecto
            ThreadHelper.ThrowIfNotOnUIThread();
            LoadProjectFiles();

            AddWelcomeMessage();
            AddSystemMessage("🧹 Cleared, the model will not remember previous conversations.");
        }

        private void SetUIState(bool isProcessing)
        {
            SendButton.IsEnabled = !isProcessing;
            SendButton.Visibility = isProcessing ? Visibility.Collapsed : Visibility.Visible;
            StopButton.IsEnabled = isProcessing;
            StopButton.Visibility = isProcessing ? Visibility.Visible : Visibility.Collapsed;
            UserInput.IsEnabled = !isProcessing;
        }
    }

    public class AttachedFile
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public string Content { get; set; }
    }

    public class ProjectFile
    {
        public string FullPath { get; set; }
        public string FileName { get; set; }
        public string Icon { get; set; }

        public override string ToString()
        {
            return Icon + " " + FileName;
        }
    }
}