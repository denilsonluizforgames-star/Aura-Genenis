using AURA.AI;
using AURA.Agents;
using AURA.Core.Configuration;
using AURA.Core.Events;
using AURA.Core.Logging;
using AURA.Core.Launchers;
using AURA.Core.Runtime;
using AURA.Memory;
using AURA.Mobile.Pages;
using AURA.Modules;
using AURA.Modules.Executors;
using AURA.Network;
using AURA.SystemInfo;
using AURA.Mobile.Speech;
using CommunityToolkit.Maui;

namespace AURA.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AuraLog.Info("MauiProgram.CreateMauiApp BEGIN");
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            // MediaElement v10: parâmetro isAndroidForegroundServiceEnabled (não enableForegroundService).
            // false = sem serviço em background; só toca com a página visível.
            .UseMauiCommunityToolkitMediaElement(isAndroidForegroundServiceEnabled: false);

#if ANDROID
        // Handler Android do WebView: mantém o comportamento do MAUI e corrige
        // rolagem + downloads + target=_blank (ver AuraWebViewHandler).
        builder.ConfigureMauiHandlers(handlers =>
            handlers.AddHandler<Microsoft.Maui.Controls.WebView, AURA.Mobile.Platforms.Android.WebView.AuraWebViewHandler>());
#endif

        AuraLog.Info("MauiProgram: builder created");

        // --- Infraestrutura AURA (mesmo Core/Abstractions usados no CLI/Termux) ---
        builder.Services.AddSingleton<ILogger, ConsoleLogger>();
        builder.Services.AddSingleton<EventBus>();

        // Configuração persistida (settings.json/modules.json na pasta privada do app).
        string configDir = Path.Combine(FileSystem.AppDataDirectory, "config");
        builder.Services.AddSingleton(sp => new ConfigLoader(sp.GetRequiredService<ILogger>())
            .LoadSettings(Path.Combine(configDir, "settings.json")));
        builder.Services.AddSingleton(sp => new ConfigLoader(sp.GetRequiredService<ILogger>())
            .LoadModules(Path.Combine(configDir, "modules.json")));

        // Gestor de módulos opcionais: baixa o pacote, aplica (ativa em
        // modules.json) e remove (desativa + limpa dados locais).
        // O repositório é privado, então o raw.githubusercontent.com devolve 404
        // para o HttpClient anônimo: passamos o leitor dos pacotes embarcados no
        // APK como fallback para "Baixar" funcionar sempre (inclusive offline).
        builder.Services.AddSingleton(sp => new ModuleManager(
            sp.GetRequiredService<ILogger>(),
            Path.Combine(FileSystem.AppDataDirectory, "modules"),
            Path.Combine(configDir, "modules.json"),
            sp.GetRequiredService<EventBus>(),
            localPackageProvider: ReadEmbeddedModulePackageAsync));

        // Memória persistente do app: pasta privada do Android (sem permissão extra).
        builder.Services.AddSingleton(sp => new MemoryStore(
            sp.GetRequiredService<ILogger>(),
            Path.Combine(FileSystem.AppDataDirectory, "memory.json")));

        // IA (OpenRouter) — mesma stack do AURA.AI usado no CLI.
        builder.Services.AddSingleton(sp => new OpenRouterClient(new OpenRouterOptions
        {
            ApiKey = Preferences.Default.Get("ai_api_key", string.Empty),
            BaseUrl = "https://openrouter.ai/api/v1/chat/completions",
            Model = "qwen/qwen-plus",
            MaxTokens = 1500
        }, sp.GetRequiredService<ILogger>()));
        builder.Services.AddSingleton<AiAssistant>();

        // Voz da AURA: TTS nativo do Android (texto arbitrário, offline, pt-br)
        // com Kokoro on-device como fallback. O VoiceAssistantService guarda a
        // última resposta e expõe falar/parar para o botão flutuante.
        builder.Services.AddSingleton<ISpeechService, HybridSpeechService>();
        builder.Services.AddSingleton<VoiceAssistantService>();

        builder.Services.AddSingleton(sp => new AgentManager(sp.GetRequiredService<ILogger>())
        {
            Events = sp.GetRequiredService<EventBus>()
        });
        builder.Services.AddSingleton<SystemAnalyzer>();
        builder.Services.AddSingleton<NetworkManager>();

        // Executores do repo (Shell/Git/Python/Node) expostos na UI de status.
        builder.Services.AddSingleton<ShellExecutor>();
        builder.Services.AddSingleton<GitExecutor>();
        builder.Services.AddSingleton<PythonExecutor>();
        builder.Services.AddSingleton<NodeExecutor>();

        // Runtime de células + runner ("AURA decide como rodar"), mesmo core do CLI.
        // Células ficam na pasta privada do app (sem permissão extra).
        builder.Services.AddSingleton(sp => new SimulationRuntime(
            sp.GetRequiredService<ILogger>(),
            Path.Combine(FileSystem.AppDataDirectory, "cells"),
            new DirectoryCellBackend())
        {
            Events = sp.GetRequiredService<EventBus>()
        });
        builder.Services.AddSingleton<Runner>();

        // Páginas
        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<HomePage>();
        builder.Services.AddSingleton<DiagnosticoPage>();
        builder.Services.AddSingleton<ChatPage>();
        builder.Services.AddSingleton<AgentPage>();
        builder.Services.AddSingleton<MemoryPage>();
        builder.Services.AddSingleton<ExecutorsPage>();
        builder.Services.AddSingleton<ModulesPage>();
        builder.Services.AddSingleton<LogsPage>();
        builder.Services.AddSingleton<FixesPage>();
        builder.Services.AddSingleton<TerminalPage>();
        builder.Services.AddSingleton<BrowserPage>();
        builder.Services.AddSingleton<ImageSearchPage>();
        builder.Services.AddSingleton<CellsPage>();
        builder.Services.AddSingleton<RunPage>();

        AuraLog.Info("MauiProgram: services registered");

        var app = builder.Build();

        // Memória registra eventos de ciclo de vida das células (reativa MemoryKind.CellEvent).
        try
        {
            var bus = app.Services.GetRequiredService<EventBus>();
            var memory = app.Services.GetRequiredService<MemoryStore>();
            bus.Subscribe<CellStateChangedEvent>(evt =>
                memory.Append(MemoryEntry.CellStateChange(evt.CellId, evt.To)));
        }
        catch (Exception ex)
        {
            AuraLog.Exception("MauiProgram.MemoryEventSink", ex);
        }

        AuraLog.Info("MauiProgram.CreateMauiApp OK");
        return app;
    }

    /// <summary>
    /// Lê o manifesto (module.json) embarcado no APK como MauiAsset. Usado como
    /// fallback pelo ModuleManager quando o download remoto falha — o
    /// repositório é privado e o raw.githubusercontent.com responde 404 a
    /// requisições sem autenticação.
    /// </summary>
    private static async Task<string?> ReadEmbeddedModulePackageAsync(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        // O LogicalName usa a pasta do módulo; em builds Windows o RecursiveDir
        // pode vir com '\\', por isso tentamos as duas variações.
        string[] candidates =
        {
            $"modulepkgs/{id}/module.json",
            $"modulepkgs\\{id}\\module.json"
        };

        foreach (string path in candidates)
        {
            try
            {
                using Stream stream = await FileSystem.OpenAppPackageFileAsync(path);
                using var reader = new StreamReader(stream);
                string json = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    AuraLog.Info($"Pacote embarcado lido para o módulo '{id}' ({path}).");
                    return json;
                }
            }
            catch (Exception ex)
            {
                AuraLog.Info($"Asset '{path}' indisponível ({ex.GetType().Name}).");
            }
        }

        AuraLog.Warning($"Nenhum pacote embarcado encontrado para o módulo '{id}'.");
        return null;
    }
}
