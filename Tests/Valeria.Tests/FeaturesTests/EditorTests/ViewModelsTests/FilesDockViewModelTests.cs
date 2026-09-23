using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Valeria.Src.Core.Domain.Models;
using Valeria.Src.Features.Editor.Domain.Enums;
using Valeria.Src.Features.Editor.Domain.Services;
using Valeria.Src.Features.Editor.UI.Screens.EditorScreen.ScreenComponents.FilesDock;
using Xunit;

namespace Valeria.Tests.FeaturesTests.EditorTests.ViewModelsTests;

public sealed class FilesDockViewModelTests
{
    private readonly FakeFilesDockService _service = new();

    [Fact]
    public void InitialState_DefaultModeIsFavorites()
    {
        FilesDockViewModel viewModel = new(_service);

        Assert.Equal(FilesDockMode.Favorites, viewModel.State.Mode);
        Assert.True(viewModel.State.IsDockExpanded);
    }

    [Fact]
    public void InitialState_WhenDockExpandedFalse_IsCollapsed()
    {
        FilesDockViewModel viewModel = new(_service, isDockExpanded: false);

        Assert.False(viewModel.State.IsDockExpanded);
    }

    [Theory]
    [InlineData("test-sample-document.md")]
    [InlineData("/tmp/sample-document.md")]
    [InlineData("Welcome.md")]
    public async Task NotifyFileOpened_WhenSampleOrTestDocument_DoesNotSetCurrentFile(string samplePath)
    {
        FilesDockViewModel viewModel = new(_service);

        await viewModel.NotifyFileOpenedAsync(samplePath);

        Assert.Null(viewModel.State.CurrentFilePath);
        Assert.False(viewModel.State.CanAddCurrentFile);
    }

    [Fact]
    public void SetMode_SwitchesBetweenFavoritesAndRecent()
    {
        FilesDockViewModel viewModel = new(_service);

        viewModel.SetRecentModeCommand.Execute().Subscribe();
        Assert.Equal(FilesDockMode.Recent, viewModel.State.Mode);

        viewModel.SetFavoritesModeCommand.Execute().Subscribe();
        Assert.Equal(FilesDockMode.Favorites, viewModel.State.Mode);

        viewModel.SwitchModeCommand.Execute().Subscribe();
        Assert.Equal(FilesDockMode.Recent, viewModel.State.Mode);
    }

    [Fact]
    public async Task OpenAddFavoriteForm_WhenFileOpened_PrefillsFileNameWithoutExtension()
    {
        FilesDockViewModel viewModel = new(_service);
        await viewModel.NotifyFileOpenedAsync("/home/user/project_plan.md");

        viewModel.OpenAddFavoriteFormCommand.Execute().Subscribe();

        Assert.True(viewModel.State.IsAddFavoriteFormOpen);
        Assert.Equal("project_plan", viewModel.State.NewFavoriteCustomName);
    }

    [Fact]
    public async Task ConfirmAddFavorite_WhenValid_AddsFavoriteWithCustomAliasAndClosesForm()
    {
        FilesDockViewModel viewModel = new(_service);
        await viewModel.NotifyFileOpenedAsync("/home/user/my_notes.md");

        viewModel.OpenAddFavoriteFormCommand.Execute().Subscribe();
        viewModel.SetNewFavoriteCustomName("Custom Workspace Notes");

        viewModel.ConfirmAddFavoriteCommand.Execute().Subscribe();

        Assert.False(viewModel.State.IsAddFavoriteFormOpen);
        Assert.Single(viewModel.State.Favorites);
        Assert.Equal("Custom Workspace Notes", viewModel.State.Favorites[0].CustomName);
        Assert.Equal("/home/user/my_notes.md", viewModel.State.Favorites[0].FilePath);
    }

    [Fact]
    public async Task RemoveFavorite_WhenCalled_RemovesFavoriteAndRefreshes()
    {
        FilesDockViewModel viewModel = new(_service);
        await viewModel.NotifyFileOpenedAsync("/home/user/my_notes.md");
        await _service.AddFavoriteAsync("/home/user/my_notes.md", "Notes");
        await viewModel.RefreshAsync();

        Assert.Single(viewModel.State.Favorites);
        int favoriteId = viewModel.State.Favorites[0].Id;

        viewModel.RemoveFavoriteCommand.Execute(favoriteId).Subscribe();

        Assert.Empty(viewModel.State.Favorites);
    }

    [Fact]
    public async Task Favorites_SortedByLastUsed_MostRecentOnTop()
    {
        FilesDockViewModel viewModel = new(_service);
        await _service.AddFavoriteAsync("/path/first.md", "First");
        await Task.Delay(20);
        await _service.AddFavoriteAsync("/path/second.md", "Second");

        await viewModel.RefreshAsync();

        Assert.Equal(2, viewModel.State.Favorites.Count);
        Assert.Equal("/path/second.md", viewModel.State.Favorites[0].FilePath);
        Assert.Equal("/path/first.md", viewModel.State.Favorites[1].FilePath);

        await _service.RecordFileOpenAsync("/path/first.md");
        await viewModel.RefreshAsync();

        Assert.Equal("/path/first.md", viewModel.State.Favorites[0].FilePath);
        Assert.Equal("/path/second.md", viewModel.State.Favorites[1].FilePath);
    }

    [Fact]
    public async Task LastOpenedNonFavorite_PositionedAtSummit_WhenNonFavoriteOpened()
    {
        FilesDockViewModel viewModel = new(_service);
        await _service.AddFavoriteAsync("/path/fav.md", "Favorite Document");

        await viewModel.NotifyFileOpenedAsync("/path/external_readme.md");

        Assert.NotNull(viewModel.State.LastOpenedNonFavorite);
        Assert.Equal("/path/external_readme.md", viewModel.State.LastOpenedNonFavorite.FilePath);
    }

    [Fact]
    public void OpenFile_WhenInvoked_EmitsFileSelectedObservable()
    {
        FilesDockViewModel viewModel = new(_service);
        string? selectedFile = null;

        viewModel.FileSelected.Subscribe(path => selectedFile = path);

        viewModel.OpenFileCommand.Execute("/path/to/open.md").Subscribe();

        Assert.Equal("/path/to/open.md", selectedFile);
    }

    private sealed class FakeFilesDockService : IFilesDockService
    {
        private readonly List<FavoriteFile> _favorites = new();
        private readonly List<RecentFile> _recent = new();
        private int _nextFavId = 1;
        private int _nextRecId = 1;

        public Task<IReadOnlyList<FavoriteFile>> GetFavoritesAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<FavoriteFile> sorted = _favorites.OrderByDescending(f => f.LastUsedAtUtc).ToList();
            return Task.FromResult(sorted);
        }

        public Task<FavoriteFile> AddFavoriteAsync(string filePath, string customName, CancellationToken cancellationToken = default)
        {
            _favorites.RemoveAll(f => f.FilePath == filePath);
            FavoriteFile fav = new(_nextFavId++, filePath, customName, DateTime.UtcNow, DateTime.UtcNow);
            _favorites.Add(fav);
            return Task.FromResult(fav);
        }

        public Task<bool> RemoveFavoriteAsync(int id, CancellationToken cancellationToken = default)
        {
            int removed = _favorites.RemoveAll(f => f.Id == id);
            return Task.FromResult(removed > 0);
        }

        public Task<bool> IsFavoriteAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_favorites.Any(f => f.FilePath == filePath));
        }

        public Task RecordFileOpenAsync(string filePath, CancellationToken cancellationToken = default)
        {
            _recent.RemoveAll(r => r.FilePath == filePath);
            _recent.Add(new RecentFile(_nextRecId++, filePath, System.IO.Path.GetFileName(filePath), DateTime.UtcNow));

            int favIndex = _favorites.FindIndex(f => f.FilePath == filePath);
            if (favIndex >= 0)
            {
                FavoriteFile current = _favorites[favIndex];
                _favorites[favIndex] = current with { LastUsedAtUtc = DateTime.UtcNow };
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RecentFile>> GetRecentFilesWithin24HoursAsync(CancellationToken cancellationToken = default)
        {
            DateTime cutoff = DateTime.UtcNow.AddHours(-24);
            IReadOnlyList<RecentFile> filtered = _recent
                .Where(r => r.LastOpenedAtUtc >= cutoff)
                .OrderByDescending(r => r.LastOpenedAtUtc)
                .ToList();
            return Task.FromResult(filtered);
        }

        public Task<RecentFile?> GetLastOpenedNonFavoriteAsync(CancellationToken cancellationToken = default)
        {
            HashSet<string> favPaths = _favorites.Select(f => f.FilePath).ToHashSet();
            RecentFile? nonFav = _recent
                .Where(r => !favPaths.Contains(r.FilePath))
                .OrderByDescending(r => r.LastOpenedAtUtc)
                .FirstOrDefault();

            return Task.FromResult(nonFav);
        }
    }
}
