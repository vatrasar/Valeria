# Welcome to Valeria

A dark markdown editor with a live styled preview. Edit on the left, read on the right.

## Typography

Regular paragraph with *italic*, **bold**, ***both***, ~~struck~~ and `inline code` spanning multiple styles in one place.

> Blockquotes render with an accent bar and muted text.
>
> > Nested quotes keep working too.

## Lists

- First bullet
- Second bullet with **bold** and `code`
  - Nested bullet
  - Another nested bullet

1. First ordered item
2. Second ordered item
3. Third ordered item

- [x] Finished task
- [ ] Open task

## Code blocks

Code blocks get full syntax highlighting for many languages:

```csharp
public sealed record MarkdownContent(ImmutableList<MarkdownBlock> Blocks)
{
    public static readonly MarkdownContent Empty = new(ImmutableList<MarkdownBlock>.Empty);

    public int Count => Blocks.Count;
}
```

```python
def render(blocks):
    for block in blocks:
        print(f"{block.kind}: {block.text[:40]}")
```

```axaml
<TextBlock Text="{x:Static res:EditorStrings.Bold}"
           Foreground="{DynamicResource AccentBrush}" />
```

```json
{
  "editor": { "fontSize": 14, "tabWidth": 4 },
  "preview": { "maxWidth": 860 }
}
```

## Table

| Feature | Status | Notes |
| :--- | :---: | ---: |
| Live preview | Done | Debounced rendering |
| Code highlight | Done | TextMate grammars |
| Images | Placeholder | Remote loading later |

## Links and images

Links render in accent color and open in the browser, like [this example link](https://example.com).

![Sample image](https://example.com/sample.png)

---

Happy writing!
