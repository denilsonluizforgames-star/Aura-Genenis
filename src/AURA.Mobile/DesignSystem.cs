// DesignSystem.cs — AURA centralized design tokens
// Generated from design system migration audit
// DO NOT EDIT: Update via design_system_migration.md instead

using Microsoft.Maui.Graphics;

namespace AURA.Mobile;

/// <summary>
/// Centralized design system tokens for AURA Mobile.
/// Single source of truth for colors, typography, spacing, and component metrics.
/// </summary>
public static class DesignSystem
{
    // ─── Paleta de Cores (Reference-aligned) ───
    
    /// <summary>Primary app background - dark navy #0c0c12</summary>
    public static readonly Color AuraBackground = Color.FromArgb("#0c0c12");
    
    /// <summary>Main surface color for cards/headers - #13131d</summary>
    public static readonly Color AuraSurface = Color.FromArgb("#13131d");
    
    /// <summary>Secondary surface for inputs/borders - #1c1c2a</summary>
    public static readonly Color AuraSurface2 = Color.FromArgb("#1c1c2a");
    
    /// <summary>Primary accent color - original aura blue #4f8aff</summary>
    public static readonly Color AuraAccent = Color.FromArgb("#4f8aff");
    
    /// <summary>Dimmed accent variant for subtle backgrounds - #1a2a4a</summary>
    public static readonly Color AuraAccentDim = Color.FromArgb("#1a2a4a");
    
    /// <summary>Accent glow effect - #0d1f3c</summary>
    public static readonly Color AuraAccentGlow = Color.FromArgb("#0d1f3c");
    
    /// <summary>Cyan accent for assistant identity - #38d9c0</summary>
    public static readonly Color AuraCyan = Color.FromArgb("#38d9c0");
    
    /// <summary>Dimmed cyan variant - #0d2e2a</summary>
    public static readonly Color AuraCyanDim = Color.FromArgb("#0d2e2a");
    
    /// <summary>Primary text color - #e8e8f0</summary>
    public static readonly Color AuraTextPrimary = Color.FromArgb("#e8e8f0");
    
    /// <summary>Secondary text color - reference #7a7a90 (fixes contrast)</summary>
    public static readonly Color AuraTextSecondary = Color.FromArgb("#7a7a90");
    
    /// <summary>Muted/placeholder text - #45455a</summary>
    public static readonly Color AuraTextMuted = Color.FromArgb("#45455a");
    
    /// <summary>Success state - #3ec97a</summary>
    public static readonly Color AuraSuccess = Color.FromArgb("#3ec97a");
    
    /// <summary>Error state - #e05560</summary>
    public static readonly Color AuraError = Color.FromArgb("#e05560");
    
    /// <summary>Warning state - #f0a050</summary>
    public static readonly Color AuraWarning = Color.FromArgb("#f0a050");
    
    /// <summary>Default border color - #242438</summary>
    public static readonly Color AuraBorder = Color.FromArgb("#242438");
    
    /// <summary>Accent-colored border - #2a3a6a</summary>
    public static readonly Color AuraBorderAccent = Color.FromArgb("#2a3a6a");
    
    /// <summary>User message bubble - #1e2d54</summary>
    public static readonly Color AuraUserBubble = Color.FromArgb("#1e2d54");
    
    /// <summary>Agent/assistant message bubble - #13131d</summary>
    public static readonly Color AuraAgentBubble = Color.FromArgb("#13131d");
    
    /// <summary>Tool/system message bubble - #0f1420</summary>
    public static readonly Color AuraToolBubble = Color.FromArgb("#0f1420");

    // ─── Extended Colors (Destination additions) ───
    
    /// <summary>Secondary purple accent (new in destination) - #8a5ae0</summary>
    public static readonly Color AuraAccent2 = Color.FromArgb("#8a5ae0");
    
    /// <summary>Glass/backdrop effect background - #990d0f18 (alpha 60%)</summary>
    public static readonly Color AuraGlass = Color.FromArgb("#990d0f18");
    
    /// <summary>Glass border with alpha - #33ffffff (alpha 20%)</summary>
    public static readonly Color AuraGlassBorder = Color.FromArgb("#33ffffff");

    // ─── Tipografia ───
    
    /// <summary>Primary font family</summary>
    public const string FontFamily = "OpenSans";
    
    /// <summary>Font sizes in points</summary>
    public static class FontSize
    {
        public const double Caption = 10;
        public const double Label = 11;
        public const double BodySmall = 12;
        public const double Body = 13;
        public const double BodyLarge = 14;
        public const double HeadingSmall = 15;
        public const double Heading = 16;
        public const double HeadingLarge = 18;
        public const double Display = 20;
        public const double Hero = 32;
    }

    // ─── Espaçamento ───
    
    /// <summary>Spacing scale (multiples of 4px)</summary>
    public static class Spacing
    {
        public const double Xs = 4;
        public const double Sm = 8;
        public const double Md = 12;
        public const double Lg = 16;
        public const double Xl = 20;
        public const double Xxl = 24;
        public const double Xxxl = 32;
    }

    // ─── Bordas e Raio ───
    
    /// <summary>Border corner radii</summary>
    public static class CornerRadius
    {
        public const int Small = 6;
        public const int Button = 8;
        public const int ButtonLarge = 10;
        public const int Card = 12;
        public const int CardLarge = 14;
        public const int Round = 22;
        public const int Pill = 999;
    }

    /// <summary>Border stroke thicknesses</summary>
    public static class StrokeThickness
    {
        public const double Hairline = 0.5;
        public const double Thin = 1;
        public const double Medium = 2;
        public const double Thick = 3;
    }

    // ─── Component Metrics ───
    
    /// <summary>Button component sizing</summary>
    public static class ButtonMetrics
    {
        public static readonly Thickness PrimaryPadding = new(16, 10);
        public static readonly Thickness GhostPadding = new(14, 8);
        public static readonly Thickness DangerPadding = new(14, 8);
        public const double PrimaryFontSize = 14;
        public const double GhostFontSize = 13;
        public const double DangerFontSize = 13;
    }

    /// <summary>Card component sizing</summary>
    public static class CardMetrics
    {
        public static readonly Thickness Padding = new(16, 14);
        public const int CornerRadius = 14;
        public const double StrokeThickness = 1;
    }

    /// <summary>Input field sizing</summary>
    public static class InputMetrics
    {
        public const double PaddingHorizontal = 12;
        public const double PaddingVertical = 6;
        public const int CornerRadius = 12;
        public const double MinHeight = 40;
        public const double MaxHeight = 120;
    }

    /// <summary>Chat bubble sizing</summary>
    public static class BubbleMetrics
    {
        public static readonly Thickness Padding = new(12, 8);
        public const int CornerRadius = 14;
        public const double MaxWidth = 340;
        public const double ToolFontSize = 12;
        public const double UserFontSize = 14;
    }

    // ─── Resource Keys (for XAML DynamicResource lookup) ───
    
    /// <summary>XAML resource dictionary keys matching App.xaml</summary>
    public static class ResourceKeys
    {
        // Colors
        public const string AuraBackground = "AuraBackground";
        public const string AuraSurface = "AuraSurface";
        public const string AuraSurface2 = "AuraSurface2";
        public const string AuraAccent = "AuraAccent";
        public const string AuraAccentDim = "AuraAccentDim";
        public const string AuraAccentGlow = "AuraAccentGlow";
        public const string AuraAccent2 = "AuraAccent2";
        public const string AuraCyan = "AuraCyan";
        public const string AuraCyanDim = "AuraCyanDim";
        public const string AuraTextPrimary = "AuraTextPrimary";
        public const string AuraTextSecondary = "AuraTextSecondary";
        public const string AuraTextMuted = "AuraTextMuted";
        public const string AuraSuccess = "AuraSuccess";
        public const string AuraError = "AuraError";
        public const string AuraWarning = "AuraWarning";
        public const string AuraBorder = "AuraBorder";
        public const string AuraBorderAccent = "AuraBorderAccent";
        public const string AuraUserBubble = "AuraUserBubble";
        public const string AuraAgentBubble = "AuraAgentBubble";
        public const string AuraToolBubble = "AuraToolBubble";
        public const string AuraGlass = "AuraGlass";
        public const string AuraGlassBorder = "AuraGlassBorder";

        // Styles
        public const string BtnPrimary = "BtnPrimary";
        public const string BtnGhost = "BtnGhost";
        public const string BtnDanger = "BtnDanger";
        public const string AuraCard = "AuraCard";
        public const string AuraCardAccent = "AuraCardAccent";
        public const string AuraGlassBar = "AuraGlassBar";
    }

    // ─── Validation Helpers ───

    /// <summary>
    /// Validate contrast ratio between two colors meets WCAG AA (4.5:1 for text)
    /// </summary>
    public static bool HasAdequateContrast(Color foreground, Color background, double minRatio = 4.5)
    {
        var fgLuminance = GetRelativeLuminance(foreground);
        var bgLuminance = GetRelativeLuminance(background);
        var lighter = Math.Max(fgLuminance, bgLuminance);
        var darker = Math.Min(fgLuminance, bgLuminance);
        var ratio = (lighter + 0.05) / (darker + 0.05);
        return ratio >= minRatio;
    }

    private static double GetRelativeLuminance(Color color)
    {
        double r = GetLinearComponent(color.Red);
        double g = GetLinearComponent(color.Green);
        double b = GetLinearComponent(color.Blue);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double GetLinearComponent(double component)
    {
        component = component <= 1.0 ? component : component / 255.0;
        return component <= 0.03928 ? component / 12.92 : Math.Pow((component + 0.055) / 1.055, 2.4);
    }

    /// <summary>
    /// Validate current design system meets accessibility standards
    /// </summary>
    public static void ValidateAccessibility()
    {
        var checks = new[]
        {
            ("TextPrimary on Background", AuraTextPrimary, AuraBackground),
            ("TextSecondary on Background", AuraTextSecondary, AuraBackground),
            ("TextPrimary on Surface", AuraTextPrimary, AuraSurface),
            ("TextSecondary on Surface", AuraTextSecondary, AuraSurface),
            ("Accent text on Accent bg", Colors.White, AuraAccent),
            ("Ghost button text on Surface2", AuraAccent, AuraSurface2),
        };

        foreach (var (name, fg, bg) in checks)
        {
            var ok = HasAdequateContrast(fg, bg);
            var ratio = (Math.Max(GetRelativeLuminance(fg), GetRelativeLuminance(bg)) + 0.05) /
                       (Math.Min(GetRelativeLuminance(fg), GetRelativeLuminance(bg)) + 0.05);
            System.Diagnostics.Debug.WriteLine($"[DesignSystem] {name}: {(ok ? "���" : "���")} ratio={ratio:F2}");
        }
    }
}