using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Headless;
using Valeria.Src.Core.Domain.Enums;
using Valeria.Src.Features.Editor.Domain.Services;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class ThemeResourcesTests
{
    [Fact]
    public void ThemeResources_DefaultActivePalette_IsCool()
    {
        ThemeResources theme = new();
        Assert.Equal(HeadingColorPalette.Cool, theme.ActivePalette);
    }

    [Fact]
    public void HeadingSizes_AreProperlyGraduated()
    {
        ThemeResources theme = new();
        Assert.Equal(32.0, theme.HeadingFontSize(1));
        Assert.Equal(25.0, theme.HeadingFontSize(2));
        Assert.Equal(21.0, theme.HeadingFontSize(3));
        Assert.Equal(18.0, theme.HeadingFontSize(4));
        Assert.Equal(15.5, theme.HeadingFontSize(5));
        Assert.Equal(14.0, theme.HeadingFontSize(6));
    }

    [Fact]
    public async Task Resolve_WhenClassicPalette_HeadingBrushesAreUniform()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        await session.Dispatch(() =>
        {
            ThemeResources theme = ThemeResources.Resolve(HeadingColorPalette.Classic);

            Assert.Equal(HeadingColorPalette.Classic, theme.ActivePalette);
            Assert.Equal(6, theme.HeadingBrushes.Count);

            for (int level = 1; level <= 6; level++)
            {
                Assert.Same(theme.Heading, theme.HeadingBrush(level));
            }

            return Task.CompletedTask;
        }, CancellationToken.None);
    }

    [Fact]
    public async Task Resolve_WhenDefaultWithoutArgs_UsesCoolPalette()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        await session.Dispatch(() =>
        {
            ThemeResources theme = ThemeResources.Resolve();

            Assert.Equal(HeadingColorPalette.Cool, theme.ActivePalette);
            Assert.Equal(6, theme.HeadingBrushes.Count);

            return Task.CompletedTask;
        }, CancellationToken.None);
    }
}
