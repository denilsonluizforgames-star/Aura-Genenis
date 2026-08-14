# AURA Mobile

Aplicação AURA para dispositivos Android via .NET MAUI

## Requisitos

- .NET 10 SDK (ou mais recente)
- Android SDK (mínimo 24 - Android 7.0)
- .NET MAUI Workload

## Instalação

```bash
# Instalar .NET MAUI Workload
dotnet workload install maui

# Configurar Android SDK
export ANDROID_HOME=/path/to/android/sdk
```

## Build Automatizado

Este repositório utiliza GitHub Actions para build automatizado:

- **Branch main**: Build automático em push
- **Artefatos**: APK gerado em cada build

## Estrutura do Projeto

Repositório auto-suficiente (standalone): contém o app MAUI e todos os projetos
de que ele depende, para compilar sozinho no GitHub Actions.

```
src/
├── AURA.Mobile/              # Projeto .NET MAUI (app Android)
├── AURA.Abstractions/        # Interfaces e modelos compartilhados
├── AURA.Core/                # Núcleo do runtime (cells, launchers, eventos)
├── AURA.Modules/             # Gerenciamento de módulos e executors
├── AURA.Agents/              # Gerenciamento de agentes
├── AURA.Memory/              # Persistência de memória
├── AURA.AI/                  # Cliente OpenRouter e provedores de IA
├── AURA.Network/             # Gerenciamento de rede
└── AURA.SystemInfo/          # Diagnóstico do sistema
```

## Executar Localmente

```bash
# Restaurar dependências
dotnet restore

# Build em Release
dotnet build -c Release

# Publicar APK
dotnet publish -c Release
```

## Configurações Especiais

- **AndroidLinkMode**: SdkOnly (necessário para reflexão dinâmica)
- **PublishTrimmed**: false (necessário para Dynamic Assembly Load)
- **RunAOTCompilation**: false (compatibilidade com Termux)

## Assets Incluídos

- `kokoro-v1.0.int8.onnx` - Modelo de inferência de linguagem
- `pf_dora.f32` - Voz TTS (português brasileiro)
- `kokoro-config.json` - Configurações de voz

## CI/CD

| Workflow | Trigger | Output |
|----------|---------|--------|
| mobile-build | push | AURA.apk |

## Licença

Projeto AURA Genesis - Desenvolvimento Open Source