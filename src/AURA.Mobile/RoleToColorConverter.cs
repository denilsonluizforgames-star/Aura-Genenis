using System.Globalization;

namespace AURA.Mobile;

/// <summary>
/// Converte o role de uma MemoryEntry (user / assistant / tool)
/// em uma cor de fundo para o chip de identificação.
/// </summary>
public sealed class RoleToColorConverter : IValueConverter
{
    public static readonly RoleToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value as string)?.ToLowerInvariant() switch
        {
            "user"      => Color.FromArgb("#1e3a6a"),   // azul accent
            "assistant" => Color.FromArgb("#0d3028"),   // teal escuro
            "tool"      => Color.FromArgb("#2a1f0a"),   // âmbar escuro
            _           => Color.FromArgb("#242438"),
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
