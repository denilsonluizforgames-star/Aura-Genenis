using AURA.Core.Events;
using AURA.Mobile.Pages;
using AURA.Modules;

namespace AURA.Mobile
{
    public class MainPage : TabbedPage
    {
        private readonly ModuleManager _manager;
        private readonly List<(string? ModuleId, string Section, string Label, Page Page)> _entries;
        private bool _permissionsAsked;

        public MainPage(
            EventBus events,
            ModuleManager manager,
            HomePage home,
            DiagnosticoPage diagnostico,
            ChatPage chat,
            AgentPage agent,
            MemoryPage memory,
            ExecutorsPage executors,
            ModulesPage modules,
            LogsPage logs,
            FixesPage fixes,
            TerminalPage terminal,
            BrowserPage browser,
            CellsPage cells,
            RunPage run)
        {
            AuraLog.Info("MainPage.ctor BEGIN");
            _manager = manager;

            events.Subscribe<ModuleStateChangedEvent>(_ =>
                MainThread.BeginInvokeOnMainThread(RebuildTabs));
            _entries = new List<(string?, string, string, Page)>
            {
                (null, "Sistema", "Início", home),
                ("system", "Sistema", "Diagnóstico", diagnostico),
                ("logs", "Sistema", "Logs", logs),
                ("logs", "Sistema", "Correções", fixes),
                ("ai", "Assistente", "Chat", chat),
                ("ai", "Assistente", "Agente", agent),
                ("memory", "Assistente", "Memória", memory),
                (null, "Assistente", "Navegador", browser),
                ("terminal", "Ferramentas", "Terminal", terminal),
                ("executors", "Ferramentas", "Executores", executors),
                (null, "Ferramentas", "Módulos", modules),
                ("cells", "Apps", "Células", cells),
                ("cells", "Apps", "Rodar programa", run)
            };

            BarBackgroundColor = Color.FromArgb("#0c0c12");
            BarTextColor = Color.FromArgb("#e8e8f0");

            AuraLog.Info("MainPage.ctor OK");
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            RebuildTabs();

            if (_permissionsAsked)
                return;
            _permissionsAsked = true;

            try
            {
                await StoragePermissionHelper.EnsureStorageAccessAsync();

                if (!StoragePermissionHelper.IsAllFilesAccessGranted()
                    && !Preferences.Get("all_files_access_asked", false))
                {
                    Preferences.Set("all_files_access_asked", true);
                    StoragePermissionHelper.RequestAllFilesAccess();
                }
            }
            catch (Exception ex)
            {
                AuraLog.Info("Permissões de armazenamento: " + ex.Message);
            }
        }

        /// <summary>
        /// Reconstrói as abas: só entra o núcleo (Módulos/Navegador) e os
        /// módulos que já foram baixados e aplicados.
        /// </summary>
        public void RebuildTabs()
        {
            Children.Clear();

            foreach (IGrouping<string, (string ModuleId, string Section, string Label, Page Page)> group
                in _entries.GroupBy(e => e.Section))
            {
                var items = group
                    .Where(e => e.ModuleId == null || _manager.IsApplied(e.ModuleId))
                    .Select(e => (e.Label, e.Page))
                    .ToArray();

                if (items.Length == 0)
                {
                    continue;
                }

                Children.Add(MakeSection(group.Key, items));
            }

            AuraLog.Info("MainPage.RebuildTabs: " + Children.Count + " seções ativas");
        }

        private static NavigationPage MakeSection(string title, params (string Label, Page Page)[] items)
        {
            var section = new SectionPage(title, items);
            return new NavigationPage(section) { Title = title };
        }
    }
}
