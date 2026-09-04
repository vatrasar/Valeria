using System.Globalization;
using System.Resources;

#nullable enable

namespace NewMarkText.Src.Shared.Resources;

/// <summary>
/// Strongly-typed access to Shared/Resources/GlobalStrings.resx.
/// Mirrors the output of PublicResXFileCodeGenerator.
/// </summary>
public static class GlobalStrings
{
    private static ResourceManager? _resourceManager;
    private static CultureInfo? _resourceCulture;

    public static ResourceManager ResourceManager
    {
        get
        {
            _resourceManager ??= new ResourceManager(
                "NewMarkText.Src.Shared.Resources.GlobalStrings",
                typeof(GlobalStrings).Assembly);

            return _resourceManager;
        }
    }

    public static CultureInfo? Culture
    {
        get => _resourceCulture;
        set => _resourceCulture = value;
    }

    public static string AppTitle => ResourceManager.GetString("AppTitle", _resourceCulture)!;

    public static string UntitledDocument => ResourceManager.GetString("UntitledDocument", _resourceCulture)!;

    public static string Open => ResourceManager.GetString("Open", _resourceCulture)!;

    public static string Save => ResourceManager.GetString("Save", _resourceCulture)!;

    public static string Cancel => ResourceManager.GetString("Cancel", _resourceCulture)!;
}
