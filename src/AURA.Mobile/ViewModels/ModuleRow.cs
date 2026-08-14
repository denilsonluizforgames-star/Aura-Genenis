using AURA.Modules;

namespace AURA.Mobile.ViewModels
{
    /// <summary>
    /// Linha exibida na Central de Módulos: envolve o ModuleInfo do catálogo
    /// com o estado calculado (núcleo / disponível / baixado / aplicado / em
    /// breve) e o texto do botão de ação.
    /// </summary>
    public sealed class ModuleRow
    {
        public ModuleInfo Module { get; init; }

        public string StateText { get; init; }

        public string ActionText { get; init; }

        public bool ShowAction { get; init; }
    }
}
