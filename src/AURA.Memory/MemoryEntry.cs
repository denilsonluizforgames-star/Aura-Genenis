using System;
using System.Text.Json.Serialization;

namespace AURA.Memory
{
    public enum MemoryKind
    {
        Turn,
        CellEvent
    }

    public sealed class MemoryEntry
    {
        public MemoryKind Kind { get; set; }

        public DateTimeOffset TimestampUtc { get; set; } = DateTimeOffset.UtcNow;

        // For Turn: the role (user/assistant) and text.
        public string Role { get; set; }

        public string Text { get; set; }

        // For CellEvent: cell id + state transition.
        public string CellId { get; set; }

        public string Detail { get; set; }

        [JsonConstructor]
        public MemoryEntry()
        {
        }

        public static MemoryEntry Question(string question)
        {
            return new MemoryEntry { Kind = MemoryKind.Turn, Role = "user", Text = question };
        }

        public static MemoryEntry Answer(string answer)
        {
            return new MemoryEntry { Kind = MemoryKind.Turn, Role = "assistant", Text = answer };
        }

        public static MemoryEntry CellStateChange(string cellId, string state)
        {
            return new MemoryEntry { Kind = MemoryKind.CellEvent, CellId = cellId, Detail = state };
        }
    }
}
