using System;
using System.Collections.Generic;

namespace FileViewerApp.Models
{
    public enum HistoryActionType
    {
        Add,
        Remove,
        Modify,
        Move,
        Reorder,
        Save,
        Discard,
        Revert,
        Load
    }

    public class InstructionSnapshot
    {
        public int Number { get; set; }
        public int OpCode { get; set; }
        public string Name { get; set; } = string.Empty;
        public int[] Parameters { get; set; } = Array.Empty<int>();
        public bool IsModified { get; set; }
    }

    public class HistoryEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public HistoryActionType ActionType { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<InstructionSnapshot> Snapshot { get; set; } = new();
        public string Hash { get; set; } = string.Empty;
    }
}
