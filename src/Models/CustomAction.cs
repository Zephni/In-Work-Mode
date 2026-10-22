namespace WorkMode.Models
{
    public sealed class CustomAction
    {
        public const string BatchCommand = "bat";
        public const string TerminalCommandName = "Open in terminal";
        public const string BackgroundCommandName = "Run in background";

        public string Title { get; set; }
        public string Type { get; set; }
        public string Command { get; set; }
        public string WorkingDirectory { get; set; }
        public bool OpenInTerminal { get; set; }

        public CustomAction()
        {
            Title = string.Empty;
            Type = BatchCommand;
            Command = string.Empty;
            WorkingDirectory = string.Empty;
        }
    }
}
