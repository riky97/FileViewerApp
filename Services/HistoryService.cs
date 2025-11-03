using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FileViewerApp.Models;

namespace FileViewerApp.Services
{
    public class HistoryService
    {
        private readonly List<HistoryEntry> _entries = new();
        private int _nextId = 1;

        public IReadOnlyList<HistoryEntry> Entries => _entries;

        public HistoryEntry CaptureSnapshot(IEnumerable<EditableInstruction> instructions, HistoryActionType actionType, string description, bool forceAdd = false)
        {
            var snap = instructions
                .OrderBy(i => i.Number)
                .Select(i => new InstructionSnapshot
                {
                    Number = i.Number,
                    OpCode = i.OpCode,
                    Name = i.Name,
                    Parameters = i.GetParameters(),
                    IsModified = i.IsModified
                }).ToList();

            var hash = ComputeHash(snap);

            // Skip duplicate only if not forced
            if (!forceAdd && _entries.Count > 0 && _entries.Last().Hash == hash)
            {
                return _entries.Last();
            }

            var entry = new HistoryEntry
            {
                Id = _nextId++,
                ActionType = actionType,
                Description = description,
                Snapshot = snap,
                Hash = hash,
                Timestamp = DateTime.UtcNow
            };
            _entries.Add(entry);
            return entry;
        }

        public HistoryEntry? GetEntry(int id) => _entries.FirstOrDefault(e => e.Id == id);

        public IEnumerable<HistoryEntry> GetRecent(int count = 50) => _entries.OrderByDescending(e => e.Id).Take(count);

        public List<InstructionSnapshot> RevertTo(int id)
        {
            var target = GetEntry(id);
            if (target == null) throw new InvalidOperationException("History entry not found");
            return target.Snapshot.Select(s => new InstructionSnapshot
            {
                Number = s.Number,
                OpCode = s.OpCode,
                Name = s.Name,
                Parameters = s.Parameters.ToArray(),
                IsModified = false
            }).ToList();
        }

        private string ComputeHash(IEnumerable<InstructionSnapshot> snapshot)
        {
            using var sha = SHA256.Create();
            var sb = new StringBuilder();
            foreach (var s in snapshot)
            {
                sb.Append(s.Number).Append('|').Append(s.OpCode).Append('|').Append(s.Name).Append('|');
                for (int i = 0; i < s.Parameters.Length; i++) sb.Append(s.Parameters[i]).Append(',');
                sb.Append(';');
            }
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return Convert.ToHexString(sha.ComputeHash(bytes));
        }
    }
}
