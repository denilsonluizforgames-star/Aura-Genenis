using System;
using System.Collections.Generic;
using System.Linq;

namespace AURA.Modules
{
    /// <summary>
    /// Static catalog of AURA modules. Three groups:
    ///  1. Core — always present, cannot be removed (Navegador + Central de Módulos).
    ///  2. Downloadable — exist in the app, but only activated after the user
    ///     downloads and applies the JSON package (hosted in this repository).
    ///  3. Planned — future modules (no code/package yet).
    /// Returns copies so callers cannot mutate the catalog.
    /// </summary>
    public static class ModuleCatalog
    {
        private const string PackageBase =
            "https://raw.githubusercontent.com/denilsonluiz3-sys/AURA_assistente/main/modules/packages";

        private static readonly List<ModuleInfo> Modules = new List<ModuleInfo>
        {
            // ------------------------- Núcleo (sempre ativos) -------------------------
            new ModuleInfo
            {
                Id = "browser",
                DisplayName = "Navegador",
                Icon = "[B]",
                ShortDescription = "Navegador integrado com buscador multi-servidor, busca de imagem reversa e VPN/Tor.",
                IsCore = true,
                Features = new List<string> { "Navegador Web", "Buscador (7 provedores)", "Imagem reversa", "VPN", "Tor (.onion)" },
                Includes = new List<string> { "WebView", "SearchCatalog", "VpnHelper" },
                Difficulty = ModuleDifficulty.Basico,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },
            new ModuleInfo
            {
                Id = "modules",
                DisplayName = "Central de Módulos",
                Icon = "[M]",
                ShortDescription = "Baixa, aplica e remove os módulos opcionais do app.",
                IsCore = true,
                Features = new List<string> { "Catálogo de módulos", "Download de pacotes", "Aplicar/remover" },
                Includes = new List<string> { "ModuleManager", "ModuleCatalog" },
                Difficulty = ModuleDifficulty.Basico,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },

            // ------------------------- Baixáveis (implementados) -------------------------
            new ModuleInfo
            {
                Id = "system",
                DisplayName = "Início e Sistema",
                Icon = "[S]",
                ShortDescription = "Painel inicial com diagnóstico do sistema, rede e agentes disponíveis.",
                PackageUrl = PackageBase + "/system/module.json",
                PackageVersion = "1.0.0",
                SizeBytes = 2048,
                Features = new List<string> { "Página Início", "Diagnóstico", "Status de rede" },
                Includes = new List<string> { "SystemAnalyzer", "NetworkManager" },
                ImplementationSteps = new List<string>
                {
                    "Painel com SO/CPU/RAM/disco",
                    "Status de rede e IP local",
                    "Lista de agentes disponíveis"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Visão do estado do aparelho",
                    "Conexão e latência"
                },
                Difficulty = ModuleDifficulty.Basico,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },
            new ModuleInfo
            {
                Id = "ai",
                DisplayName = "IA",
                Icon = "[AI]",
                ShortDescription = "Assistente inteligente: chat com a IA (OpenRouter) e agente de arquivos com ferramentas.",
                PackageUrl = PackageBase + "/ai/module.json",
                PackageVersion = "1.0.0",
                SizeBytes = 3072,
                Features = new List<string> { "Chat", "Agente", "Workspace e ferramentas" },
                Includes = new List<string> { "OpenRouterClient", "AgentManager" },
                ImplementationSteps = new List<string>
                {
                    "Chat direto com modelo OpenRouter",
                    "Agente com ferramentas de arquivo",
                    "Reuso da chave salva nas configurações"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Conversa natural em português",
                    "Execução de tarefas em arquivos"
                },
                Difficulty = ModuleDifficulty.Intermediario,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },
            new ModuleInfo
            {
                Id = "memory",
                DisplayName = "Memória",
                Icon = "[ME]",
                ShortDescription = "Guarda preferências e histórico para a AURA lembrar do contexto entre sessões.",
                PackageUrl = PackageBase + "/memory/module.json",
                PackageVersion = "1.0.0",
                SizeBytes = 2048,
                Features = new List<string> { "Memória persistente", "Histórico de células" },
                Includes = new List<string> { "MemoryStore" },
                ImplementationSteps = new List<string>
                {
                    "Formato persistente de memória",
                    "Registro de eventos de células",
                    "Edição e limpeza pelo usuário"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Continuidade de contexto entre sessões",
                    "Perfil personalizado do usuário"
                },
                Difficulty = ModuleDifficulty.Basico,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },
            new ModuleInfo
            {
                Id = "executors",
                DisplayName = "Executores",
                Icon = "[E]",
                ShortDescription = "Executa comandos Shell, Git, Python e Node com saída capturada.",
                PackageUrl = PackageBase + "/executors/module.json",
                PackageVersion = "1.0.0",
                SizeBytes = 2048,
                Features = new List<string> { "Shell", "Git", "Python", "Node" },
                Includes = new List<string> { "ShellExecutor", "GitExecutor", "PythonExecutor", "NodeExecutor" },
                ImplementationSteps = new List<string>
                {
                    "Integração com cada executor",
                    "Captura de saída e erros",
                    "Exibição de status"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Rodar scripts e comandos no aparelho"
                },
                Difficulty = ModuleDifficulty.Intermediario,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },
            new ModuleInfo
            {
                Id = "terminal",
                DisplayName = "Terminal",
                Icon = "[T]",
                ShortDescription = "Console interativo para comandos com histórico de saída.",
                PackageUrl = PackageBase + "/terminal/module.json",
                PackageVersion = "1.0.0",
                SizeBytes = 2048,
                Features = new List<string> { "Console de comandos", "Log de saída" },
                Includes = new List<string> { "TerminalPage" },
                ImplementationSteps = new List<string>
                {
                    "Entrada de comandos no aparelho",
                    "Exibição em tempo real da saída"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Interação direta com o shell"
                },
                Difficulty = ModuleDifficulty.Intermediario,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },
            new ModuleInfo
            {
                Id = "cells",
                DisplayName = "Células",
                Icon = "[C]",
                ShortDescription = "Processos isolados com ciclo de vida, limites e a rota de execução automática.",
                PackageUrl = PackageBase + "/cells/module.json",
                PackageVersion = "1.0.0",
                SizeBytes = 4096,
                Features = new List<string> { "Células", "Rodar programa", "Limites de CPU/RAM" },
                Includes = new List<string> { "SimulationRuntime", "Runner" },
                ImplementationSteps = new List<string>
                {
                    "Ciclo de vida de células",
                    "Escolha automática de como rodar",
                    "Aplicação de limites (prlimit)"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Processos isolados e controlados"
                },
                Difficulty = ModuleDifficulty.Avancado,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },
            new ModuleInfo
            {
                Id = "logs",
                DisplayName = "Logs e Correções",
                Icon = "[L]",
                ShortDescription = "Visualiza o log de execução da AURA e aplica correções de sistema.",
                PackageUrl = PackageBase + "/logs/module.json",
                PackageVersion = "1.0.0",
                SizeBytes = 2048,
                Features = new List<string> { "Log de execução", "Correções de sistema" },
                Includes = new List<string> { "LogsPage", "FixesPage" },
                ImplementationSteps = new List<string>
                {
                    "Exibição do log persistente",
                    "Testes e correções rápidas"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Diagnóstico e reparo pelo log"
                },
                Difficulty = ModuleDifficulty.Basico,
                EstimatedTime = "Feito",
                Status = ModuleStatus.Implementado
            },

            // ------------------------- Planejados (futuro) -------------------------
            new ModuleInfo
            {
                Id = "windows",
                DisplayName = "Assistente Windows",
                Icon = "[W]",
                ShortDescription = "Automatiza tarefas do Windows: WMI, Registro, Serviços e PowerShell.",
                Includes = new List<string> { "WMI", "Registro", "Serviços", "PowerShell" },
                ImplementationSteps = new List<string>
                {
                    "Mapear os comandos de administração mais úteis",
                    "Integrar execução de PowerShell com saída capturada",
                    "Criar automações prontas (limpeza, otimização, diagnóstico)"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Controle de serviços e processos",
                    "Automação de tarefas administrativas"
                },
                Difficulty = ModuleDifficulty.Avancado,
                EstimatedTime = "2 semanas",
                Status = ModuleStatus.Planejado
            },
            new ModuleInfo
            {
                Id = "automation",
                DisplayName = "Automação",
                Icon = "[A]",
                ShortDescription = "Cria rotinas e macros para repetir tarefas do dia a dia automaticamente.",
                Includes = new List<string> { "Rotinas", "Macros", "Agendador" },
                ImplementationSteps = new List<string>
                {
                    "Definir um formato de rotina por scripts",
                    "Criar o agendador de tarefas recorrentes",
                    "Adicionar gatilhos por evento do sistema"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Execução automática de tarefas",
                    "Rotinas agendadas sem intervenção"
                },
                Difficulty = ModuleDifficulty.Intermediario,
                EstimatedTime = "2 semanas",
                Status = ModuleStatus.Planejado
            },
            new ModuleInfo
            {
                Id = "plugins",
                DisplayName = "Plugins",
                Icon = "[P]",
                ShortDescription = "Permite estender a AURA com novos recursos desenvolvidos pela comunidade.",
                Includes = new List<string> { "Carregador de plugins", "API de extensão", "Repositório" },
                ImplementationSteps = new List<string>
                {
                    "Definir a API pública de plugins (IPlugin)",
                    "Implementar o carregamento dinâmico de assemblies",
                    "Criar um repositório local de plugins instaláveis"
                },
                AcquiredCapabilities = new List<string>
                {
                    "Extensibilidade pela comunidade",
                    "Instalação de recursos sem recompilar a AURA"
                },
                Difficulty = ModuleDifficulty.Avancado,
                EstimatedTime = "4 semanas",
                Status = ModuleStatus.Planejado
            }
        };

        public static List<ModuleInfo> GetAll()
        {
            return Modules.ToList();
        }

        /// <summary>Módulos do núcleo: sempre ativos e sem pacote para baixar.</summary>
        public static List<ModuleInfo> GetCore()
        {
            return Modules.Where(m => m.IsCore).ToList();
        }

        /// <summary>Módulos baixáveis: existem no app, mas dependem de pacote baixado + aplicado.</summary>
        public static List<ModuleInfo> GetDownloadable()
        {
            return Modules.Where(m => !m.IsCore && !string.IsNullOrWhiteSpace(m.PackageUrl)).ToList();
        }

        public static ModuleInfo GetById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            return Modules.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        }
    }
}
