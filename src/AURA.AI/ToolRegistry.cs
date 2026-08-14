using System;
using System.Collections.Generic;
using System.Linq;

namespace AURA.AI
{
    /// <summary>
    /// Registro e resolução de <see cref="AgentTool"/> para o loop agêntico.
    /// Centraliza lookup por nome e exposição de definições ao LLM,
    /// sem alterar o contrato <c>ExecuteAsync → string</c>.
    /// </summary>
    public sealed class ToolRegistry
    {
        private readonly Dictionary<string, AgentTool> _byName =
            new Dictionary<string, AgentTool>(StringComparer.Ordinal);

        public ToolRegistry()
        {
        }

        public ToolRegistry(IEnumerable<AgentTool> tools)
        {
            if (tools == null)
            {
                return;
            }

            foreach (AgentTool tool in tools)
            {
                Register(tool);
            }
        }

        /// <summary>Quantidade de ferramentas registradas.</summary>
        public int Count
        {
            get { return _byName.Count; }
        }

        /// <summary>
        /// Registra a ferramenta. Nome vazio ou nulo lanca;
        /// nome duplicado substitui a anterior (ultimo ganha).
        /// </summary>
        public void Register(AgentTool tool)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            string name = tool.Definition?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "A ferramenta precisa de Definition.Name não vazio.", nameof(tool));
            }

            _byName[name] = tool;
        }

        /// <summary>
        /// Registra somente se o nome ainda não existir.
        /// Retorna true se registrou, false se já havia outra com o mesmo nome.
        /// </summary>
        public bool TryRegister(AgentTool tool)
        {
            if (tool == null)
            {
                throw new ArgumentNullException(nameof(tool));
            }

            string name = tool.Definition?.Name ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "A ferramenta precisa de Definition.Name não vazio.", nameof(tool));
            }

            if (_byName.ContainsKey(name))
            {
                return false;
            }

            _byName[name] = tool;
            return true;
        }

        /// <summary>Resolve pelo nome exato da definição, ou null.</summary>
        public AgentTool? Resolve(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            AgentTool? tool;
            return _byName.TryGetValue(name, out tool) ? tool : null;
        }

        /// <summary>True se existe ferramenta com esse nome.</summary>
        public bool Contains(string name)
        {
            return !string.IsNullOrEmpty(name) && _byName.ContainsKey(name);
        }

        /// <summary>Definições na ordem de registro (estável para o prompt).</summary>
        public IReadOnlyList<AgentToolDefinition> Definitions()
        {
            return _byName.Values.Select(t => t.Definition).ToList();
        }

        /// <summary>Ferramentas registradas (ordem de inserção do dicionário).</summary>
        public IReadOnlyList<AgentTool> Tools()
        {
            return _byName.Values.ToList();
        }
    }
}
