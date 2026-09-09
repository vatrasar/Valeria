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
}
