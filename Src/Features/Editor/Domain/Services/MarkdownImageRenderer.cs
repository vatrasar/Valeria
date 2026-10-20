using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Material.Icons;
using Material.Icons.Avalonia;
using Valeria.Src.Core.Config;
using Valeria.Src.Core.Markdown;

namespace Valeria.Src.Features.Editor.Domain.Services;

internal sealed class MarkdownImageRenderer
{
    private readonly IMarkdownImageLoader _imageLoader;
    private readonly PreviewOptions _options;

    public MarkdownImageRenderer(IMarkdownImageLoader imageLoader, PreviewOptions options)
    {
        _imageLoader = imageLoader;
        _options = options;
    }

    public Control CreateImageControl(ImageSpan image, ThemeResources theme, string? baseDirectory)
    {
        Border container = new()
        {
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Margin = new Thickness(0, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        ToolTip.SetTip(container, FormatImageTooltip(image));

        if (_imageLoader.TryGetCached(image.Url, baseDirectory, out Bitmap? cachedBitmap))
        {
            if (cachedBitmap is not null)
            {
                container.Child = CreateImageView(cachedBitmap);
                return container;
            }

            container.Child = CreateBrokenImagePlaceholder(image, theme);
            return container;
        }

        container.Child = CreateLoadingPlaceholder(image, theme);
        InitiateAsyncImageLoad(image, baseDirectory, container, theme);

        return container;
    }

    private void InitiateAsyncImageLoad(
        ImageSpan image,
        string? baseDirectory,
        Border container,
        ThemeResources theme)
    {
        _ = _imageLoader.LoadImageAsync(image.Url, baseDirectory)
            .ContinueWith(task =>
            {
                Bitmap? bitmap = task.IsCompletedSuccessfully ? task.Result : null;
                Dispatcher.UIThread.Post(() =>
                {
                    if (bitmap is not null)
                        container.Child = CreateImageView(bitmap);
                    else
                        container.Child = CreateBrokenImagePlaceholder(image, theme);
                });
            }, TaskScheduler.Default);
    }

    private Image CreateImageView(Bitmap bitmap)
    {
        return new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            MaxWidth = _options.MaxWidth,
            HorizontalAlignment = HorizontalAlignment.Left
        };
    }

    private static Control CreateLoadingPlaceholder(ImageSpan image, ThemeResources theme)
    {
        TextBlock caption = new()
        {
            Text = FormatImageLabel(image),
            FontFamily = theme.ProseFont,
            FontSize = theme.CodeFontSize,
            Foreground = theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        panel.Children.Add(new MaterialIcon { Kind = MaterialIconKind.ImageOutline, Width = 16, Height = 16 });
        panel.Children.Add(caption);

        return new Border
        {
            BorderBrush = theme.CellBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Child = panel
        };
    }

    private static Control CreateBrokenImagePlaceholder(ImageSpan image, ThemeResources theme)
    {
        TextBlock caption = new()
        {
            Text = FormatImageLabel(image),
            FontFamily = theme.ProseFont,
            FontSize = theme.CodeFontSize,
            Foreground = theme.Muted,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };

        StackPanel panel = new()
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8
        };

        panel.Children.Add(new MaterialIcon { Kind = MaterialIconKind.ImageBrokenVariant, Width = 16, Height = 16 });
        panel.Children.Add(caption);

        return new Border
        {
            BorderBrush = theme.CellBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(10, 6),
            Child = panel
        };
    }

    private static string FormatImageLabel(ImageSpan image)
    {
        if (string.IsNullOrWhiteSpace(image.AlternativeText))
            return $"{Resources.EditorStrings.ImagePlaceholder} ({image.Url})";

        return $"{Resources.EditorStrings.ImagePlaceholder}: {image.AlternativeText} ({image.Url})";
    }

    private static string FormatImageTooltip(ImageSpan image)
    {
        if (string.IsNullOrWhiteSpace(image.AlternativeText))
            return image.Url;

        return $"{image.AlternativeText} ({image.Url})";
    }
}
