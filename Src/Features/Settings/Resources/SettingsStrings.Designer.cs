using System.Globalization;
using System.Resources;

#nullable enable

namespace Valeria.Src.Features.Settings.Resources;

/// <summary>
/// Strongly-typed access to Settings/Resources/SettingsStrings.resx.
/// Mirrors the output of PublicResXFileCodeGenerator.
/// </summary>
public static class SettingsStrings
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    public static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "Valeria.Src.Features.Settings.Resources.SettingsStrings",
                typeof(SettingsStrings).Assembly);

            return _resourceManager;
        }
    }

    public static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    public static string SettingsTitle => ResourceManager.GetString("SettingsTitle", _resourceCulture)!;

    public static string BackButton => ResourceManager.GetString("BackButton", _resourceCulture)!;

    public static string BackTooltip => ResourceManager.GetString("BackTooltip", _resourceCulture)!;

    public static string EditorSection => ResourceManager.GetString("EditorSection", _resourceCulture)!;

    public static string AutoSaveTitle => ResourceManager.GetString("AutoSaveTitle", _resourceCulture)!;

    public static string AutoSaveDescription => ResourceManager.GetString("AutoSaveDescription", _resourceCulture)!;

    public static string AutoSaveStatusOn => ResourceManager.GetString("AutoSaveStatusOn", _resourceCulture)!;

    public static string AutoSaveStatusOff => ResourceManager.GetString("AutoSaveStatusOff", _resourceCulture)!;

    public static string AutoSaveDelayTitle => ResourceManager.GetString("AutoSaveDelayTitle", _resourceCulture)!;

    public static string AutoSaveDelayDescription => ResourceManager.GetString("AutoSaveDelayDescription", _resourceCulture)!;

    public static string FontSizeTitle => ResourceManager.GetString("FontSizeTitle", _resourceCulture)!;

    public static string FontSizeDescription => ResourceManager.GetString("FontSizeDescription", _resourceCulture)!;

    public static string TabWidthTitle => ResourceManager.GetString("TabWidthTitle", _resourceCulture)!;

    public static string TabWidthDescription => ResourceManager.GetString("TabWidthDescription", _resourceCulture)!;

    public static string SavedStatus => ResourceManager.GetString("SavedStatus", _resourceCulture)!;
 
    public static string PreviewSection => ResourceManager.GetString("PreviewSection", _resourceCulture)!;

    public static string HeadingPaletteTitle => ResourceManager.GetString("HeadingPaletteTitle", _resourceCulture)!;

    public static string HeadingPaletteDescription => ResourceManager.GetString("HeadingPaletteDescription", _resourceCulture)!;

    public static string HeadingPaletteSemantic => ResourceManager.GetString("HeadingPaletteSemantic", _resourceCulture)!;

    public static string HeadingPaletteCool => ResourceManager.GetString("HeadingPaletteCool", _resourceCulture)!;
 
    public static string HeadingPaletteClassic => ResourceManager.GetString("HeadingPaletteClassic", _resourceCulture)!;
 
    public static string HeadingNumberingTitle => ResourceManager.GetString("HeadingNumberingTitle", _resourceCulture)!;

    public static string HeadingNumberingDescription => ResourceManager.GetString("HeadingNumberingDescription", _resourceCulture)!;
}
