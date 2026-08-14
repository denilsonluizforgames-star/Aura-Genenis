using System.Collections.Generic;
using System.Text.Json;

namespace AURA.Mobile.Diagnostics
{
    /// <summary>Uma correção proposta pela IA, aplicável sem recompilar.</summary>
    public sealed class FixProposal
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Current { get; set; } = string.Empty;
        public string Suggested { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public bool Selected { get; set; } = true;

        public override string ToString() =>
            $"{Label}: {Current} → {Suggested}";
    }

    public static class FixProposalParser
    {
        public static List<FixProposal> Parse(string json)
        {
            var result = new List<FixProposal>();
            try
            {
                using var doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;

                if (root.TryGetProperty("fixes", out JsonElement fixes) && fixes.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement f in fixes.EnumerateArray())
                    {
                        var proposal = new FixProposal
                        {
                            Key = GetString(f, "key") ?? string.Empty,
                            Label = GetString(f, "label") ?? string.Empty,
                            Current = GetString(f, "current") ?? string.Empty,
                            Suggested = GetString(f, "suggested") ?? string.Empty,
                            Reason = GetString(f, "reason") ?? string.Empty
                        };
                        if (!string.IsNullOrWhiteSpace(proposal.Key))
                        {
                            result.Add(proposal);
                        }
                    }
                }
            }
            catch
            {
                // Prompt inválido ou resposta fora do JSON esperado.
            }

            return result;
        }

        private static string? GetString(JsonElement el, string name)
        {
            if (el.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }
    }
}
