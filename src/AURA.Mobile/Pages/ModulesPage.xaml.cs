using System.Linq;
using AURA.Modules;
using AURA.Mobile.ViewModels;

namespace AURA.Mobile.Pages
{
    public partial class ModulesPage : ContentPage
    {
        private readonly ModuleManager _manager;

        public ModulesPage(ModuleManager manager)
        {
            InitializeComponent();
            _manager = manager;
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await RefreshAsync();
        }

        private async void OnActionClicked(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var row = (ModuleRow)button.CommandParameter;
            try
            {
                switch (row.ActionText)
                {
                    case "Baixar":
                        await SetBusyAsync($"Baixando {row.Module.DisplayName}...");
                        await _manager.DownloadAsync(row.Module.Id);
                        await ShowStatus($"Módulo '{row.Module.DisplayName}' baixado. Toque em Aplicar para ativá-lo.");
                        break;
                    case "Aplicar":
                        _manager.Apply(row.Module.Id);
                        await ShowStatus($"Módulo '{row.Module.DisplayName}' aplicado.");
                        break;
                    case "Remover":
                        _manager.Remove(row.Module.Id);
                        await ShowStatus($"Módulo '{row.Module.DisplayName}' removido.");
                        break;
                }
            }
            catch (Exception ex)
            {
                AuraLog.Exception("ModulesPage.Action " + row.Module.Id, ex);
                await ShowStatus("Falha: " + ex.Message);
            }
            finally
            {
                await RefreshAsync();
            }
        }

        private async void OnDownloadAllClicked(object sender, EventArgs e)
        {
            var pendentes = ModuleCatalog.GetDownloadable().Where(m => !_manager.IsDownloaded(m.Id)).ToList();
            if (pendentes.Count == 0)
            {
                await ShowStatus("Nenhum módulo pendente de download.");
                return;
            }

            await SetBusyAsync($"Baixando {pendentes.Count} módulo(s)...");
            int ok = 0;
            foreach (ModuleInfo m in pendentes)
            {
                try
                {
                    await _manager.DownloadAsync(m.Id);
                    ok++;
                }
                catch (Exception ex)
                {
                    AuraLog.Exception("ModulesPage.DownloadAll " + m.Id, ex);
                }
            }

            await ShowStatus($"{ok}/{pendentes.Count} baixados. Toque em 'Aplicar todos' para ativá-los.");
            await RefreshAsync();
        }

        private async void OnApplyAllClicked(object sender, EventArgs e)
        {
            var baixados = ModuleCatalog.GetDownloadable()
                .Where(m => _manager.IsDownloaded(m.Id) && !_manager.IsApplied(m.Id))
                .ToList();
            if (baixados.Count == 0)
            {
                await ShowStatus("Nenhum módulo baixado e não aplicado.");
                return;
            }

            foreach (ModuleInfo m in baixados)
            {
                _manager.Apply(m.Id);
            }

            await ShowStatus($"{baixados.Count} módulo(s) aplicado(s).");
            await RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            var rows = ModuleCatalog.GetAll().Select(m =>
            {
                if (m.IsCore)
                {
                    return new ModuleRow { Module = m, StateText = "Núcleo (sempre ativo)" };
                }

                if (string.IsNullOrWhiteSpace(m.PackageUrl))
                {
                    return new ModuleRow { Module = m, StateText = "Em breve" };
                }

                if (_manager.IsApplied(m.Id))
                {
                    return new ModuleRow { Module = m, StateText = "Aplicado", ActionText = "Remover", ShowAction = true };
                }

                if (_manager.IsDownloaded(m.Id))
                {
                    return new ModuleRow { Module = m, StateText = "Baixado", ActionText = "Aplicar", ShowAction = true };
                }

                return new ModuleRow { Module = m, StateText = "Disponível", ActionText = "Baixar", ShowAction = true };
            }).ToList();

            ModulesView.ItemsSource = rows;

            int aplicados = rows.Count(r => r.StateText == "Aplicado");
            SummaryLabel.Text = $"Módulos do núcleo (navegador + central) são sempre ativos. " +
                                $"Aplicados: {aplicados} de {rows.Count(r => !string.IsNullOrWhiteSpace(r.ActionText)) + aplicados} baixáveis.";
        }

        private async Task SetBusyAsync(string message)
        {
            StatusLabel.Text = message;
            StatusLabel.IsVisible = true;
            DownloadAllButton.IsEnabled = false;
            ApplyAllButton.IsEnabled = false;
            await Task.CompletedTask;
        }

        private async Task ShowStatus(string message)
        {
            StatusLabel.Text = message;
            StatusLabel.IsVisible = true;
            DownloadAllButton.IsEnabled = true;
            ApplyAllButton.IsEnabled = true;
            AuraLog.Info("ModulesPage: " + message);
            await Task.CompletedTask;
        }
    }
}
