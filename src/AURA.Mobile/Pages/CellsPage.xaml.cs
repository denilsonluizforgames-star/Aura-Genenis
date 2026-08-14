using AURA.Core.Launchers;
using AURA.Core.Runtime;
using Cell = AURA.Core.Runtime.Cell;

namespace AURA.Mobile.Pages;

public partial class CellsPage : ContentPage
{
    private readonly SimulationRuntime _runtime;
    private readonly Runner _runner;
    private readonly RunPage _runPage;
    private bool _loaded;

    public CellsPage(SimulationRuntime runtime, Runner runner, RunPage runPage)
    {
        InitializeComponent();
        _runtime = runtime;
        _runner = runner;
        _runPage = runPage;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (!_loaded)
        {
            _loaded = true;
            try
            {
                await _runtime.LoadFromStoreAsync();
            }
            catch (Exception ex)
            {
                AuraLog.Exception("CellsPage.LoadFromStore", ex);
            }
        }

        Refresh();
    }

    private void Refresh()
    {
        CellsView.ItemsSource = _runtime.Cells
            .OrderBy(c => c.Id)
            .ToList();
    }

    private async void OnStartClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not Cell cell)
        {
            return;
        }

        try
        {
            await _runtime.StartCellAsync(cell.Id);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Erro", ex.Message, "OK");
        }

        Refresh();
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not Cell cell)
        {
            return;
        }

        _runtime.StopCell(cell.Id);
        Refresh();
    }

    private async void OnPauseClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not Cell cell)
        {
            return;
        }

        _runtime.PauseCell(cell.Id);
        Refresh();
    }

    private async void OnResumeClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not Cell cell)
        {
            return;
        }

        _runtime.ResumeCell(cell.Id);
        Refresh();
    }

    private async void OnLogClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not Cell cell)
        {
            return;
        }

        string log = _runtime.ReadCellLog(cell.Id, 300);
        await DisplayAlert("Log: " + cell.Id, log, "Fechar");
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if ((sender as Button)?.CommandParameter is not Cell cell)
        {
            return;
        }

        bool confirm = await DisplayAlertAsync(
            "Excluir célula",
            "Excluir '" + cell.Id + "' e todos os seus dados?",
            "Excluir",
            "Cancelar");

        if (!confirm)
        {
            return;
        }

        _runtime.DeleteCell(cell.Id);
        Refresh();
    }

    private async void OnNewClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(_runPage);
    }

    private void OnRefreshClicked(object sender, EventArgs e)
    {
        Refresh();
    }
}
