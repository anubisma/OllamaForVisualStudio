using EnvDTE;
using EnvDTE80;
using System.Collections.Generic;
using System.IO;
using System.Windows;

namespace OllamaForVisualStudio
{
    public partial class AttachFileDialog : System.Windows.Window
    {
        public List<string> SelectedFiles { get; private set; } = new List<string>();

        public AttachFileDialog(DTE2 dte)
        {
            InitializeComponent();
            LoadProjectFiles(dte);
        }

        private void LoadProjectFiles(DTE2 dte)
        {
            var files = new List<string>();

            try
            {
                // Agregar archivo activo primero
                if (dte.ActiveDocument != null)
                {
                    files.Add(dte.ActiveDocument.FullName);
                }

                // Agregar archivos de la solución
                if (dte.Solution != null)
                {
                    foreach (Project project in dte.Solution.Projects)
                    {
                        AddProjectFiles(project.ProjectItems, files);
                    }
                }
            }
            catch { }

            // Filtrar y ordenar
            var codeFiles = new List<string>();
            var extensions = new[] { ".cs", ".xaml", ".json", ".xml", ".js", ".ts", ".html", ".css", ".sql", ".md", ".txt" };

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLower();
                foreach (var validExt in extensions)
                {
                    if (ext == validExt && !codeFiles.Contains(file))
                    {
                        codeFiles.Add(file);
                        break;
                    }
                }
            }

            codeFiles.Sort((a, b) => Path.GetFileName(a).CompareTo(Path.GetFileName(b)));

            foreach (var file in codeFiles)
            {
                FileListBox.Items.Add(new FileItem 
                { 
                    FullPath = file, 
                    DisplayName = Path.GetFileName(file) 
                });
            }
        }

        private void AddProjectFiles(ProjectItems items, List<string> files)
        {
            if (items == null) return;

            foreach (ProjectItem item in items)
            {
                try
                {
                    if (item.FileCount > 0)
                    {
                        var filePath = item.FileNames[1];
                        if (File.Exists(filePath))
                        {
                            files.Add(filePath);
                        }
                    }

                    if (item.ProjectItems != null && item.ProjectItems.Count > 0)
                    {
                        AddProjectFiles(item.ProjectItems, files);
                    }
                }
                catch { }
            }
        }

        private void Attach_Click(object sender, RoutedEventArgs e)
        {
            foreach (FileItem item in FileListBox.SelectedItems)
            {
                SelectedFiles.Add(item.FullPath);
            }
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class FileItem
    {
        public string FullPath { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}