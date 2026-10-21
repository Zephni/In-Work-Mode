namespace WorkMode.Models
{
    public sealed class CustomAction
    {
        public const string BatchCommand = "bat";

        public string Title { get; set; }
        public string Type { get; set; }
        public string Command { get; set; }
        public string WorkingDirectory { get; set; }

        public CustomAction()
        {
            Title = string.Empty;
            Type = BatchCommand;
            Command = string.Empty;
            WorkingDirectory = string.Empty;
        }
    }
}
