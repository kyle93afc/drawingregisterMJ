using System;
using System.Windows;
using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using DrawingRegister.App.Models;

namespace DrawingRegister.App.Services;

/// <summary>
/// Manages application-wide theme palette switching based on the active organization profile.
/// Updates brush and color keys in Application.Current.Resources dynamically.
/// </summary>
public static class OrganizationThemeManager
{
    public static void ApplyTheme(OrganizationProfile? org)
    {
        if (Application.Current == null) return;

        var profile = org ?? OrganizationRegistry.Default;
        var baseColor = ParseColor(profile.BrandColorHex, "#eb1845");
        var hoverColor = ParseColor(profile.BrandHoverColorHex, profile.BrandColorHex);
        var pressedColor = ParseColor(profile.BrandPressedColorHex, profile.BrandColorHex);
        var lightColor = ParseColor(profile.BrandLightColorHex, "#FEF2F4");

        UpdateBrush("BrandRedBrush", baseColor);
        UpdateBrush("BrandRedHoverBrush", hoverColor);
        UpdateBrush("BrandRedPressedBrush", pressedColor);
        UpdateBrush("BrandRedLightBrush", lightColor);
        UpdateBrush("InputBorderFocusBrush", baseColor);

        Application.Current.Resources["BrandRedColor"] = baseColor;
        Application.Current.Resources["BrandRedHoverColor"] = hoverColor;
        Application.Current.Resources["BrandRedPressedColor"] = pressedColor;
        Application.Current.Resources["BrandRedLightColor"] = lightColor;
    }

    private static void UpdateBrush(string key, Color color)
    {
        if (Application.Current.Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
        {
            brush.Color = color;
        }
        else
        {
            Application.Current.Resources[key] = new SolidColorBrush(color);
        }
    }

    private static Color ParseColor(string? hex, string fallbackHex)
    {
        var targetHex = !string.IsNullOrWhiteSpace(hex) ? hex : fallbackHex;
        try
        {
            return (Color)ColorConverter.ConvertFromString(targetHex);
        }
        catch
        {
            return (Color)ColorConverter.ConvertFromString(fallbackHex);
        }
    }
}
