using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen;
using Valeria.Src.Features.Shell.UI.Screens.Main;
using Valeria.Src.Infrastructure;
using ReactiveUI;
using Splat;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

public sealed class EditorScreenIntegrationTests
{
    private const int PreviewWaitMilliseconds = 1200;

    [Fact]
    public async Task ViewModelText_PopulatesSourceEditorAndPreview()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                Assert.False(editor.State.IsEditorVisible);
                editor.ToggleEditorCommand.Execute().Subscribe();
                editor.SetMarkdownText("# Hi\n\nHello **bold**");
            }, CancellationToken.None);
            await Task.Delay(PreviewWaitMilliseconds);

            (string viewModelText, string sourceText, int previewCount, string? error, double editorWidth, string titleLabel) =
                await session.Dispatch(() =>
                {
                    EditorView view = RequireView(window);
                    TextEditor source = RequireSourceEditor(view);
                    ItemsControl preview = view.FindControl<ItemsControl>("PreviewBlocksControl")
                        ?? throw new InvalidOperationException("PreviewBlocksControl not found.");
                    TextBlock title = view.FindControl<TextBlock>("DocumentTitleLabel")
                        ?? throw new InvalidOperationException("DocumentTitleLabel not found.");

                    return (
                        editor.State.MarkdownText,
                        source.Text,
                        preview.ItemCount,
                        editor.State.ErrorMessage,
                        source.Bounds.Width,
                        title.Text ?? string.Empty);
                }, CancellationToken.None);

            Assert.True(string.IsNullOrEmpty(error), $"ErrorMessage: {error}");
            Assert.True(titleLabel.Length > 0, "Status bar was never rendered, view activation did not run.");
            Assert.True(viewModelText.Contains("# Hi"), $"viewModelText was '{viewModelText}'");
            Assert.True(sourceText.Contains("# Hi"), $"sourceText was '{sourceText}'");
            Assert.True(previewCount > 0);
            Assert.True(editorWidth > 100);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() => window.Close(), CancellationToken.None);
        }
    }

    [Fact]
    public async Task TypingInSourceEditor_UpdatesViewModelAndPreview()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            await session.Dispatch(() =>
            {
                EditorView view = RequireView(window);
                TextEditor source = RequireSourceEditor(view);
                source.Text = "# typed\n\nsome text";
            }, CancellationToken.None);

            await Task.Delay(PreviewWaitMilliseconds);

            (string viewModelText, int previewCount) = await session.Dispatch(() =>
                (editor.State.MarkdownText, editor.State.PreviewBlocks.Count), CancellationToken.None);

            Assert.Contains("typed", viewModelText);
            Assert.True(previewCount > 0);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() => window.Close(), CancellationToken.None);
        }
    }

    [Fact]
    public async Task ToggleEditor_SwitchesVisibility()
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        Window? window = null;

        try
        {
            (window, EditorViewModel editor) = await SetupEditorAsync(session);

            bool initial = await session.Dispatch(() => editor.State.IsEditorVisible, CancellationToken.None);
            Assert.False(initial);

            await session.Dispatch(() => editor.ToggleEditorCommand.Execute().Subscribe(), CancellationToken.None);
            bool afterFirstToggle = await session.Dispatch(() => editor.State.IsEditorVisible, CancellationToken.None);
            Assert.True(afterFirstToggle);

            await session.Dispatch(() => editor.ToggleEditorCommand.Execute().Subscribe(), CancellationToken.None);
            bool afterSecondToggle = await session.Dispatch(() => editor.State.IsEditorVisible, CancellationToken.None);
            Assert.False(afterSecondToggle);
        }
        finally
        {
            if (window is not null)
                await session.Dispatch(() => window.Close(), CancellationToken.None);
        }
    }

    private static async Task<(Window Window, EditorViewModel Editor)> SetupEditorAsync(HeadlessUnitTestSession session)
    {
        return await session.Dispatch(() =>
        {
            RxApp.MainThreadScheduler = AvaloniaScheduler.Instance;
            Locator.CurrentMutable.Register(() => new AvaloniaActivationForViewFetcher(), typeof(IActivationForViewFetcher));

            IConfiguration configuration = new ConfigurationBuilder().Build();
            ServiceProvider provider = new ServiceCollection().AddValeria(configuration).BuildServiceProvider();

            MainWindowViewModel shell = provider.GetRequiredService<MainWindowViewModel>();
            EditorViewModel editor = ActivatorUtilities.CreateInstance<EditorViewModel>(provider, shell);
            EditorView view = new() { ViewModel = editor };

            Window window = new() { Content = view, Width = 1280, Height = 800 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            return (window, editor);
        }, CancellationToken.None);
    }

    private static EditorView RequireView(Window? window)
    {
        EditorView? view = window?.GetVisualDescendants().OfType<EditorView>().FirstOrDefault();

        if (view is null)
            throw new InvalidOperationException("EditorView was not created.");

        return view;
    }

    private static TextEditor RequireSourceEditor(EditorView view)
    {
        TextEditor? source = view.GetVisualDescendants().OfType<TextEditor>().FirstOrDefault(editor => !editor.IsReadOnly);

        if (source is null)
            throw new InvalidOperationException("Editable source TextEditor was not found.");

        return source;
    }
}
