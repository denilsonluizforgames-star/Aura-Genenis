using System.Collections.Generic;
using AURA.Core.Abstractions;

namespace AURA.Modules
{
    /// <summary>
    /// Describes one of AURA's capability modules shown in the
    /// "Módulos" catalog/manager.
    ///
    /// Two kinds exist:
    ///  - Core: always present, cannot be removed (e.g. Navegador, Central de
    ///    Módulos). IsCore = true and no package URL.
    ///  - Downloadable: hosted as a JSON package in the repository
    ///    (PackageUrl). The user downloads, applies and removes them at will.
    /// </summary>
    public sealed class ModuleInfo : IModule
    {
        public string Id { get; set; }

        public string DisplayName { get; set; }

        public string Icon { get; set; }

        public string ShortDescription { get; set; }

        /// <summary>Módulo fixo no núcleo (não pode ser removido nem precisa baixar).</summary>
        public bool IsCore { get; set; }

        /// <summary>URL pública do pacote JSON deste módulo (ex.: raw.githubusercontent).</summary>
        public string PackageUrl { get; set; }

        /// <summary>Versão do pacote, se conhecida (ex.: "1.0.0").</summary>
        public string PackageVersion { get; set; }

        /// <summary>Tamanho aproximado do pacote em bytes (0 quando desconhecido).</summary>
        public long SizeBytes { get; set; }

        /// <summary>Recursos/rotas que este módulo habilita no app.</summary>
        public List<string> Features { get; set; }

        public List<string> Includes { get; set; }

        public List<string> ImplementationSteps { get; set; }

        public List<string> AcquiredCapabilities { get; set; }

        public ModuleDifficulty Difficulty { get; set; }

        public string EstimatedTime { get; set; }

        /// <summary>Estado real: implementado (em uso) ou só planejado.</summary>
        public ModuleStatus Status { get; set; } = ModuleStatus.Planejado;
    }
}
