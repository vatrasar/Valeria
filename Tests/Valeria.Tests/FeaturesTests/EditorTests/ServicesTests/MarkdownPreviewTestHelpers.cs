using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;

namespace Valeria.Tests.FeaturesTests.EditorTests.ServicesTests;

internal static class MarkdownPreviewTestHelpers
{
    public static Task<T> EvaluateAsync<T>(Func<T> evaluate)
    {
        HeadlessUnitTestSession session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());

        return session.Dispatch(evaluate, CancellationToken.None);
    }

    public static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        if (root is T match)
            yield return match;

        foreach (Control child in VisualChildrenOf(root))
            foreach (T nested in Descendants<T>(child))
                yield return nested;
    }

    public static IEnumerable<Control> VisualChildrenOf(Control control)
    {
        foreach (Control child in control.GetVisualChildren().OfType<Control>())
            yield return child;

        if (control.GetVisualChildren().Count() == 0 && control is ILogical logical)
            foreach (ILogical child in logical.LogicalChildren)
                if (child is Control childControl)
                    yield return childControl;
    }
}
