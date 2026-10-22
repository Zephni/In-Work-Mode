namespace WorkMode.Models
{
    public sealed class CustomAction
    {
        public const string BatchCommand = "bat";
        public const string PythonCommand = "python";
        public const string PowerShellCommand = "powershell";
        public const string TerminalCommandName = "Terminal";
        public const string CloseTerminalCommandName = "Terminal (close after execution)";
        public const string BackgroundCommandName = "Run in background";

        public string Title { get; set; }
        public string Type { get; set; }
        public string Command { get; set; }
        public string WorkingDirectory { get; set; }
        public bool OpenInTerminal { get; set; }
        public bool CloseTerminalAfterExecution { get; set; }

        public static string GetLanguageName(string type)
        {
            switch (type)
            {
                case BatchCommand: return "Batch";
                case PythonCommand: return "Python 3";
                case PowerShellCommand: return "PowerShell";
                default: return "Unsupported: " + type;
            }
        }

        public string Description
        {
            get
            {
                return GetLanguageName(Type) + " - " +
                    (OpenInTerminal
                        ? (CloseTerminalAfterExecution ? CloseTerminalCommandName : TerminalCommandName)
                        : BackgroundCommandName);
            }
        }

        public CustomAction()
        {
            Title = string.Empty;
            Type = BatchCommand;
            Command = string.Empty;
            WorkingDirectory = string.Empty;
        }
    }
}
