namespace FileViewerApp.Models
{
    /// <summary>
    /// Placeholder per istruzioni rimosse (mostrate in rosso fino al salvataggio)
    /// </summary>
    public class RemovedInstructionPlaceholder
    {
        public int Number { get; init; }
        public string Display => $"[RIMOSSA #{Number}]";
    }
}
