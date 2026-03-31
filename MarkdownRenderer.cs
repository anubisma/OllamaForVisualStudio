using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace OllamaForVisualStudio
{
    public static class MarkdownRenderer
    {
        // ========== REGEX PATTERNS - TODOS LOS ESTÁNDARES DE MARKDOWN ==========

        // Código inline `code`
        private static readonly Regex InlineCodeRegex = new Regex(@"`([^`\r\n]+)`", RegexOptions.Compiled);

        // Negrita+Cursiva ***text*** o ___text___ (debe procesarse ANTES que negrita/cursiva)
        private static readonly Regex BoldItalicRegex = new Regex(@"\*\*\*(.+?)\*\*\*|___(.+?)___", RegexOptions.Compiled);

        // Negrita **text** o __text__
        private static readonly Regex BoldRegex = new Regex(@"\*\*(.+?)\*\*|__(.+?)__", RegexOptions.Compiled);

        // Cursiva *text* o _text_ (evita conflictos con negrita)
        private static readonly Regex ItalicRegex = new Regex(@"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)|(?<!_)_(?!_)(.+?)(?<!_)_(?!_)", RegexOptions.Compiled);

        // Tachado ~~text~~
        private static readonly Regex StrikethroughRegex = new Regex(@"~~(.+?)~~", RegexOptions.Compiled);

        // Enlaces [text](url) o [text](url "title")
        private static readonly Regex LinkRegex = new Regex(@"\[([^\]]+)\]\(([^\s\)]+)(?:\s+""[^""]*"")?\)", RegexOptions.Compiled);

        // Imágenes ![alt](url)
        private static readonly Regex ImageRegex = new Regex(@"!\[([^\]]*)\]\(([^\)]+)\)", RegexOptions.Compiled);

        // Auto-links <url> o <email>
        private static readonly Regex AutoLinkRegex = new Regex(@"<(https?://[^\s>]+|[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,})>", RegexOptions.Compiled);

        // URLs directas (sin formato)
        private static readonly Regex UrlRegex = new Regex(@"(?<![(\[<])(https?://[^\s\)\]>]+)", RegexOptions.Compiled);

        // Línea horizontal --- o *** o ___ (3+ caracteres)
        private static readonly Regex HorizontalRuleRegex = new Regex(@"^(\s*[-*_]\s*){3,}$", RegexOptions.Compiled);

        // Lista con viñeta (-, *, +) con posible indentación
        private static readonly Regex BulletListRegex = new Regex(@"^(\s*)([-*+])\s+(.+)$", RegexOptions.Compiled);

        // Lista numerada con posible indentación
        private static readonly Regex NumberedListRegex = new Regex(@"^(\s*)(\d+)\.\s+(.+)$", RegexOptions.Compiled);

        // Checkbox [ ] o [x] o [X]
        private static readonly Regex CheckboxRegex = new Regex(@"^\s*[-*+]\s+\[([ xX])\]\s+(.+)$", RegexOptions.Compiled);

        // Encabezados # hasta ######
        private static readonly Regex HeadingRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);

        // Citas > (una o más)
        private static readonly Regex QuoteRegex = new Regex(@"^(>+)\s*(.*)$", RegexOptions.Compiled);

        // Encabezados estilo Setext (línea debajo con === o ---)
        private static readonly Regex SetextH1Regex = new Regex(@"^=+\s*$", RegexOptions.Compiled);
        private static readonly Regex SetextH2Regex = new Regex(@"^-+\s*$", RegexOptions.Compiled);

        public static StackPanel RenderMarkdown(string markdown)
        {
            var panel = new StackPanel();

            if (string.IsNullOrEmpty(markdown))
                return panel;

            try
            {
                markdown = markdown.Replace("\r\n", "\n").Replace("\r", "\n");

                var codeBlocks = new Dictionary<string, CodeBlockInfo>();
                string processed = ExtractCodeBlocks(markdown, codeBlocks);

                var lines = processed.Split(new[] { "\n" }, StringSplitOptions.None);
                var currentParagraph = new List<string>();
                var quoteBuffer = new List<string>();
                string previousLine = null;

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var trimmed = line.Trim();

                    // Placeholder de código
                    if (trimmed.StartsWith("§§CODE_") && trimmed.EndsWith("§§"))
                    {
                        FlushParagraph(panel, currentParagraph);
                        FlushQuotes(panel, quoteBuffer);
                        if (codeBlocks.TryGetValue(trimmed, out var info))
                            panel.Children.Add(CreateCodeBlock(info.Code, info.Language));
                        previousLine = null;
                        continue;
                    }

                    // Línea horizontal
                    if (HorizontalRuleRegex.IsMatch(trimmed) && trimmed.Length >= 3)
                    {
                        FlushParagraph(panel, currentParagraph);
                        FlushQuotes(panel, quoteBuffer);
                        panel.Children.Add(CreateHorizontalRule());
                        previousLine = null;
                        continue;
                    }

                    // Encabezado estilo Setext (=== para H1, --- para H2)
                    if (previousLine != null && !string.IsNullOrWhiteSpace(previousLine))
                    {
                        if (SetextH1Regex.IsMatch(trimmed))
                        {
                            // Remover la última línea del párrafo y crear H1
                            if (currentParagraph.Count > 0)
                            {
                                var lastLine = currentParagraph[currentParagraph.Count - 1];
                                currentParagraph.RemoveAt(currentParagraph.Count - 1);
                                FlushParagraph(panel, currentParagraph);
                                panel.Children.Add(CreateHeading(lastLine.Trim(), 1));
                                previousLine = null;
                                continue;
                            }
                        }
                        else if (SetextH2Regex.IsMatch(trimmed) && trimmed.Length >= 2 && !BulletListRegex.IsMatch(previousLine))
                        {
                            if (currentParagraph.Count > 0)
                            {
                                var lastLine = currentParagraph[currentParagraph.Count - 1];
                                currentParagraph.RemoveAt(currentParagraph.Count - 1);
                                FlushParagraph(panel, currentParagraph);
                                panel.Children.Add(CreateHeading(lastLine.Trim(), 2));
                                previousLine = null;
                                continue;
                            }
                        }
                    }

                    // Encabezado con #
                    var headingMatch = HeadingRegex.Match(trimmed);
                    if (headingMatch.Success)
                    {
                        FlushParagraph(panel, currentParagraph);
                        FlushQuotes(panel, quoteBuffer);
                        panel.Children.Add(CreateHeading(headingMatch.Groups[2].Value, headingMatch.Groups[1].Value.Length));
                        previousLine = null;
                        continue;
                    }

                    // Checkbox
                    var checkboxMatch = CheckboxRegex.Match(trimmed);
                    if (checkboxMatch.Success)
                    {
                        FlushParagraph(panel, currentParagraph);
                        FlushQuotes(panel, quoteBuffer);
                        bool isChecked = checkboxMatch.Groups[1].Value.ToLower() == "x";
                        panel.Children.Add(CreateCheckboxItem(checkboxMatch.Groups[2].Value, isChecked));
                        previousLine = line;
                        continue;
                    }

                    // Lista con viñeta
                    var bulletMatch = BulletListRegex.Match(line);
                    if (bulletMatch.Success)
                    {
                        FlushParagraph(panel, currentParagraph);
                        FlushQuotes(panel, quoteBuffer);
                        int indent = bulletMatch.Groups[1].Value.Length;
                        panel.Children.Add(CreateListItem(bulletMatch.Groups[3].Value, "•", indent));
                        previousLine = line;
                        continue;
                    }

                    // Lista numerada
                    var numMatch = NumberedListRegex.Match(line);
                    if (numMatch.Success)
                    {
                        FlushParagraph(panel, currentParagraph);
                        FlushQuotes(panel, quoteBuffer);
                        int indent = numMatch.Groups[1].Value.Length;
                        panel.Children.Add(CreateListItem(numMatch.Groups[3].Value, numMatch.Groups[2].Value + ".", indent));
                        previousLine = line;
                        continue;
                    }

                    // Cita (acumular múltiples líneas)
                    var quoteMatch = QuoteRegex.Match(trimmed);
                    if (quoteMatch.Success)
                    {
                        FlushParagraph(panel, currentParagraph);
                        int depth = quoteMatch.Groups[1].Value.Length;
                        string quoteText = quoteMatch.Groups[2].Value;
                        quoteBuffer.Add(new string('>', depth) + " " + quoteText);
                        previousLine = line;
                        continue;
                    }

                    // Si hay buffer de citas y la línea actual no es cita, flush
                    if (quoteBuffer.Count > 0)
                    {
                        FlushQuotes(panel, quoteBuffer);
                    }

                    // Línea vacía
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        FlushParagraph(panel, currentParagraph);
                        previousLine = null;
                        continue;
                    }

                    currentParagraph.Add(line);
                    previousLine = line;
                }

                FlushParagraph(panel, currentParagraph);
                FlushQuotes(panel, quoteBuffer);
            }
            catch (Exception ex)
            {
                panel.Children.Clear();
                panel.Children.Add(new TextBox
                {
                    Text = markdown + "\n\n[Error: " + ex.Message + "]",
                    IsReadOnly = true,
                    TextWrapping = TextWrapping.Wrap,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220))
                });
            }

            return panel;
        }

        private static string ExtractCodeBlocks(string text, Dictionary<string, CodeBlockInfo> blocks)
        {
            var result = new System.Text.StringBuilder();
            var lines = text.Split('\n');
            bool inCode = false;
            string lang = "";
            var code = new System.Text.StringBuilder();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (!inCode && trimmed.StartsWith("```"))
                {
                    inCode = true;
                    lang = trimmed.Length > 3 ? trimmed.Substring(3).Trim() : "";
                    code.Clear();
                    continue;
                }

                if (inCode && trimmed == "```")
                {
                    inCode = false;
                    var key = string.Format("§§CODE_{0}§§", blocks.Count);
                    blocks[key] = new CodeBlockInfo { Language = lang, Code = code.ToString().TrimEnd() };
                    result.AppendLine(key);
                    continue;
                }

                if (inCode)
                {
                    if (code.Length > 0) code.AppendLine();
                    code.Append(line);
                }
                else
                {
                    result.AppendLine(line);
                }
            }

            if (inCode && code.Length > 0)
            {
                var key = string.Format("§§CODE_{0}§§", blocks.Count);
                blocks[key] = new CodeBlockInfo { Language = lang, Code = code.ToString().TrimEnd() };
                result.AppendLine(key);
            }

            return result.ToString();
        }

        private static void FlushParagraph(StackPanel panel, List<string> lines)
        {
            if (lines.Count == 0) return;
            var text = string.Join(" ", lines);
            panel.Children.Add(CreateSelectableFormattedText(text, 13, new Thickness(0, 0, 0, 8)));
            lines.Clear();
        }

        private static void FlushQuotes(StackPanel panel, List<string> quoteLines)
        {
            if (quoteLines.Count == 0) return;

            // Procesar todas las líneas de cita juntas
            var combinedText = new List<string>();
            foreach (var line in quoteLines)
            {
                var match = QuoteRegex.Match(line.Trim());
                if (match.Success)
                    combinedText.Add(match.Groups[2].Value);
                else
                    combinedText.Add(line);
            }

            panel.Children.Add(CreateQuote(string.Join(" ", combinedText)));
            quoteLines.Clear();
        }

        private static UIElement CreateSelectableFormattedText(string text, double fontSize, Thickness margin, FontWeight? fontWeight = null)
        {
            var rtb = new RichTextBox
            {
                IsReadOnly = true,
                IsDocumentEnabled = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(-4, 0, 0, 0),
                Margin = margin,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = fontSize,
                Cursor = Cursors.IBeam
            };

            if (fontWeight.HasValue)
                rtb.FontWeight = fontWeight.Value;

            var paragraph = new Paragraph { Margin = new Thickness(0), LineHeight = fontSize * 1.4 };
            ParseInlines(text, paragraph.Inlines);

            var doc = new FlowDocument { PagePadding = new Thickness(0) };
            doc.Blocks.Add(paragraph);
            rtb.Document = doc;

            return rtb;
        }

        private static void ParseInlines(string text, InlineCollection inlines)
        {
            var segments = new List<TextSegment> { new TextSegment { Start = 0, End = text.Length, Text = text, Type = "text" } };

            // ORDEN IMPORTANTE: de más específico a menos específico
            segments = ExtractMatches(text, segments, ImageRegex, "image");
            segments = ExtractMatches(text, segments, LinkRegex, "link");
            segments = ExtractMatches(text, segments, AutoLinkRegex, "autolink");
            segments = ExtractMatches(text, segments, UrlRegex, "url");
            segments = ExtractMatches(text, segments, InlineCodeRegex, "code");
            segments = ExtractMatches(text, segments, BoldItalicRegex, "bolditalic");
            segments = ExtractMatches(text, segments, BoldRegex, "bold");
            segments = ExtractMatches(text, segments, ItalicRegex, "italic");
            segments = ExtractMatches(text, segments, StrikethroughRegex, "strikethrough");

            segments.Sort((a, b) => a.Start.CompareTo(b.Start));

            foreach (var seg in segments)
            {
                switch (seg.Type)
                {
                    case "bolditalic":
                        inlines.Add(new Run(seg.Content) { FontWeight = FontWeights.Bold, FontStyle = FontStyles.Italic });
                        break;
                    case "bold":
                        inlines.Add(new Run(seg.Content) { FontWeight = FontWeights.Bold });
                        break;
                    case "italic":
                        inlines.Add(new Run(seg.Content) { FontStyle = FontStyles.Italic });
                        break;
                    case "strikethrough":
                        inlines.Add(new Run(seg.Content) { TextDecorations = TextDecorations.Strikethrough });
                        break;
                    case "code":
                        inlines.Add(new Run(seg.Content)
                        {
                            FontFamily = new FontFamily("Cascadia Code, Consolas"),
                            Foreground = new SolidColorBrush(Color.FromRgb(206, 145, 120)),
                            Background = new SolidColorBrush(Color.FromRgb(40, 40, 45))
                        });
                        break;
                    case "link":
                    case "autolink":
                    case "url":
                        var linkUrl = seg.Url ?? seg.Content;
                        var linkText = seg.Content ?? linkUrl;
                        try
                        {
                            var link = new Hyperlink(new Run(linkText))
                            {
                                NavigateUri = new Uri(linkUrl, UriKind.RelativeOrAbsolute),
                                Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214))
                            };
                            link.RequestNavigate += (s, e) =>
                            {
                                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
                                catch { }
                            };
                            inlines.Add(link);
                        }
                        catch
                        {
                            inlines.Add(new Run(linkText) { Foreground = new SolidColorBrush(Color.FromRgb(86, 156, 214)) });
                        }
                        break;
                    case "image":
                        try
                        {
                            var imageContainer = new InlineUIContainer(CreateImageElement(seg.Url, seg.Content));
                            inlines.Add(imageContainer);
                        }
                        catch
                        {
                            inlines.Add(new Run("[Imagen: " + (seg.Content ?? seg.Url) + "]") { FontStyle = FontStyles.Italic });
                        }
                        break;
                    default:
                        if (!string.IsNullOrEmpty(seg.Text))
                            inlines.Add(new Run(seg.Text));
                        break;
                }
            }
        }

        private static UIElement CreateImageElement(string url, string alt)
        {
            try
            {
                var image = new Image
                {
                    MaxWidth = 400,
                    MaxHeight = 300,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(4, 2, 4, 2)
                };

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(url, UriKind.RelativeOrAbsolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                image.Source = bitmap;

                if (!string.IsNullOrEmpty(alt))
                    image.ToolTip = alt;

                return image;
            }
            catch
            {
                return new TextBlock
                {
                    Text = "[Imagen: " + (alt ?? url) + "]",
                    FontStyle = FontStyles.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150))
                };
            }
        }

        private static List<TextSegment> ExtractMatches(string fullText, List<TextSegment> segments, Regex regex, string type)
        {
            var result = new List<TextSegment>();

            foreach (var seg in segments)
            {
                if (seg.Type != "text")
                {
                    result.Add(seg);
                    continue;
                }

                int lastEnd = 0;
                foreach (Match m in regex.Matches(seg.Text))
                {
                    if (m.Index > lastEnd)
                    {
                        result.Add(new TextSegment
                        {
                            Start = seg.Start + lastEnd,
                            End = seg.Start + m.Index,
                            Text = seg.Text.Substring(lastEnd, m.Index - lastEnd),
                            Type = "text"
                        });
                    }

                    // Obtener contenido del grupo correcto
                    string content = null;
                    string url = null;

                    if (type == "image")
                    {
                        content = m.Groups[1].Value; // alt text
                        url = m.Groups[2].Value;     // url
                    }
                    else if (type == "link")
                    {
                        content = m.Groups[1].Value; // text
                        url = m.Groups[2].Value;     // url
                    }
                    else if (type == "autolink" || type == "url")
                    {
                        content = m.Groups[1].Success ? m.Groups[1].Value : m.Value;
                        url = content;
                    }
                    else
                    {
                        // Para formatos como bold, italic, etc. que pueden tener alternativas
                        if (m.Groups[1].Success && !string.IsNullOrEmpty(m.Groups[1].Value))
                            content = m.Groups[1].Value;
                        else if (m.Groups.Count > 2 && m.Groups[2].Success && !string.IsNullOrEmpty(m.Groups[2].Value))
                            content = m.Groups[2].Value;
                        else
                            content = m.Groups[1].Value;
                    }

                    result.Add(new TextSegment
                    {
                        Start = seg.Start + m.Index,
                        End = seg.Start + m.Index + m.Length,
                        Content = content,
                        Url = url,
                        Type = type
                    });

                    lastEnd = m.Index + m.Length;
                }

                if (lastEnd < seg.Text.Length)
                {
                    result.Add(new TextSegment
                    {
                        Start = seg.Start + lastEnd,
                        End = seg.Start + seg.Text.Length,
                        Text = seg.Text.Substring(lastEnd),
                        Type = "text"
                    });
                }
            }

            return result;
        }

        private static UIElement CreateHeading(string text, int level)
        {
            double size = level == 1 ? 22 : level == 2 ? 19 : level == 3 ? 16 : level == 4 ? 14 : level == 5 ? 13 : 12;

            var rtb = new RichTextBox
            {
                IsReadOnly = true,
                IsDocumentEnabled = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(-4, 0, 0, 0),
                Margin = new Thickness(0, 12, 0, 6),
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = size,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.IBeam
            };

            var paragraph = new Paragraph { Margin = new Thickness(0) };
            ParseInlines(text, paragraph.Inlines);

            var doc = new FlowDocument { PagePadding = new Thickness(0) };
            doc.Blocks.Add(paragraph);
            rtb.Document = doc;

            return rtb;
        }

        private static UIElement CreateListItem(string text, string bullet, int indent = 0)
        {
            int indentLevel = indent / 2; // Cada 2 espacios = 1 nivel
            var grid = new Grid { Margin = new Thickness(8 + (indentLevel * 16), 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Diferentes viñetas según nivel de indentación
            string displayBullet = bullet;
            if (bullet == "•" && indentLevel > 0)
            {
                displayBullet = indentLevel % 2 == 0 ? "•" : "◦";
            }

            var bulletTb = new TextBlock
            {
                Text = displayBullet,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 149, 237)),
                FontSize = 13,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 2, 8, 0)
            };
            Grid.SetColumn(bulletTb, 0);

            var rtb = new RichTextBox
            {
                IsReadOnly = true,
                IsDocumentEnabled = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(-4, 0, 0, 0),
                Margin = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(220, 220, 220)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Cursor = Cursors.IBeam
            };

            var paragraph = new Paragraph { Margin = new Thickness(0) };
            ParseInlines(text, paragraph.Inlines);

            var doc = new FlowDocument { PagePadding = new Thickness(0) };
            doc.Blocks.Add(paragraph);
            rtb.Document = doc;

            Grid.SetColumn(rtb, 1);

            grid.Children.Add(bulletTb);
            grid.Children.Add(rtb);
            return grid;
        }

        private static UIElement CreateCheckboxItem(string text, bool isChecked)
        {
            var grid = new Grid { Margin = new Thickness(8, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var checkbox = new TextBlock
            {
                Text = isChecked ? "☑" : "☐",
                Foreground = new SolidColorBrush(isChecked ? Color.FromRgb(100, 200, 100) : Color.FromRgb(150, 150, 150)),
                FontSize = 14,
                TextAlignment = TextAlignment.Right,
                Margin = new Thickness(0, 1, 8, 0)
            };
            Grid.SetColumn(checkbox, 0);

            var rtb = new RichTextBox
            {
                IsReadOnly = true,
                IsDocumentEnabled = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(-4, 0, 0, 0),
                Margin = new Thickness(0),
                Foreground = new SolidColorBrush(isChecked ? Color.FromRgb(150, 150, 150) : Color.FromRgb(220, 220, 220)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                Cursor = Cursors.IBeam
            };

            if (isChecked)
            {
                var paragraph = new Paragraph { Margin = new Thickness(0) };
                paragraph.Inlines.Add(new Run(text) { TextDecorations = TextDecorations.Strikethrough });
                var doc = new FlowDocument { PagePadding = new Thickness(0) };
                doc.Blocks.Add(paragraph);
                rtb.Document = doc;
            }
            else
            {
                var paragraph = new Paragraph { Margin = new Thickness(0) };
                ParseInlines(text, paragraph.Inlines);
                var doc = new FlowDocument { PagePadding = new Thickness(0) };
                doc.Blocks.Add(paragraph);
                rtb.Document = doc;
            }

            Grid.SetColumn(rtb, 1);

            grid.Children.Add(checkbox);
            grid.Children.Add(rtb);
            return grid;
        }

        private static UIElement CreateQuote(string text)
        {
            var border = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 149, 237)),
                BorderThickness = new Thickness(3, 0, 0, 0),
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 45)),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 4, 0, 8)
            };

            var rtb = new RichTextBox
            {
                IsReadOnly = true,
                IsDocumentEnabled = true,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(-4, 0, 0, 0),
                Margin = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 13,
                FontStyle = FontStyles.Italic,
                Cursor = Cursors.IBeam
            };

            var paragraph = new Paragraph { Margin = new Thickness(0) };
            ParseInlines(text, paragraph.Inlines);

            var doc = new FlowDocument { PagePadding = new Thickness(0) };
            doc.Blocks.Add(paragraph);
            rtb.Document = doc;

            border.Child = rtb;
            return border;
        }

        private static UIElement CreateHorizontalRule()
        {
            return new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Color.FromRgb(80, 80, 85)),
                Margin = new Thickness(0, 12, 0, 12)
            };
        }

        private static UIElement CreateCodeBlock(string code, string language)
        {
            var container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(30, 30, 30)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 6, 0, 10)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                CornerRadius = new CornerRadius(6, 6, 0, 0),
                Padding = new Thickness(12, 6, 12, 6)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var langLabel = new TextBlock
            {
                Text = string.IsNullOrEmpty(language) ? "código" : language.ToLower(),
                Foreground = new SolidColorBrush(Color.FromRgb(130, 130, 130)),
                FontSize = 11
            };
            Grid.SetColumn(langLabel, 0);

            var copyBtn = new Button
            {
                Content = "📋 Copiar",
                Background = new SolidColorBrush(Color.FromRgb(55, 55, 58)),
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                FontSize = 11,
                Tag = code
            };
            copyBtn.Click += (s, e) =>
            {
                try
                {
                    Clipboard.SetText(code);
                    copyBtn.Content = "✓ Copiado";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                    timer.Tick += (_, __) => { copyBtn.Content = "📋 Copiar"; timer.Stop(); };
                    timer.Start();
                }
                catch { }
            };
            Grid.SetColumn(copyBtn, 1);

            headerGrid.Children.Add(langLabel);
            headerGrid.Children.Add(copyBtn);
            header.Child = headerGrid;
            Grid.SetRow(header, 0);

            var codeBox = new TextBox
            {
                Text = code,
                FontFamily = new FontFamily("Cascadia Code, Consolas, Courier New"),
                FontSize = 13,
                Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212)),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(12, 8, 12, 8),
                MaxHeight = 400
            };
            Grid.SetRow(codeBox, 1);

            grid.Children.Add(header);
            grid.Children.Add(codeBox);
            container.Child = grid;

            return container;
        }

        private class TextSegment
        {
            public int Start { get; set; }
            public int End { get; set; }
            public string Text { get; set; }
            public string Content { get; set; }
            public string Url { get; set; }
            public string Type { get; set; }
        }

        private class CodeBlockInfo
        {
            public string Language { get; set; }
            public string Code { get; set; }
        }
    }
}