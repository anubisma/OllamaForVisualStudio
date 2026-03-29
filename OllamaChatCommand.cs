using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using Task = System.Threading.Tasks.Task;

namespace OllamaForVisualStudio
{
    internal sealed class OllamaChatCommand
    {
        public const int CommandId = 0x0100;
        public static readonly Guid CommandSet = new Guid("B2C3D4E5-F6A7-8901-BCDE-F12345678901");

        private readonly AsyncPackage _package;

        private OllamaChatCommand(AsyncPackage package, IMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));

            if (commandService == null)
                throw new ArgumentNullException(nameof(commandService));

            var menuCommandId = new CommandID(CommandSet, CommandId);
            var menuItem = new OleMenuCommand(Execute, menuCommandId);
            commandService.AddCommand(menuItem);
        }

        public static OllamaChatCommand Instance { get; private set; }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            var commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as IMenuCommandService;
            Instance = new OllamaChatCommand(package, commandService);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var window = _package.FindToolWindow(typeof(OllamaChatToolWindow), 0, true);
            if (window == null || window.Frame == null)
            {
                throw new NotSupportedException("No se pudo crear la ventana de chat.");
            }

            var windowFrame = window.Frame as IVsWindowFrame;
            if (windowFrame == null)
            {
                throw new NotSupportedException("No se pudo crear la ventana de chat.");
            }

            Microsoft.VisualStudio.ErrorHandler.ThrowOnFailure(windowFrame.Show());
        }
    }
}