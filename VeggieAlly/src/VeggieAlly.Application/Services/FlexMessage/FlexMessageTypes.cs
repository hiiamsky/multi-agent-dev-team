namespace VeggieAlly.Application.Services.FlexMessage;

/// <summary>
/// LINE Flex Message Bubble
/// </summary>
public record FlexBubble
{
    public string Type { get; init; } = "bubble";
    public FlexBox? Header { get; init; }
    public FlexBox? Body { get; init; }
    public FlexBox? Footer { get; init; }
    public FlexBubbleAction? Action { get; init; }
}

/// <summary>
/// LINE Flex Box Component
/// </summary>
public record FlexBox : IFlexComponent
{
    public string Type { get; init; } = "box";
    public string Layout { get; init; } = "vertical";  // vertical, horizontal, baseline
    public string? Spacing { get; init; }  // none, xs, sm, md, lg, xl, xxl
    public string? Margin { get; init; }
    public string? PaddingAll { get; init; }
    public string? PaddingTop { get; init; }
    public string? PaddingBottom { get; init; }
    public string? PaddingStart { get; init; }
    public string? PaddingEnd { get; init; }
    public string? BackgroundColor { get; init; }
    public List<IFlexComponent> Contents { get; init; } = [];
}

/// <summary>
/// LINE Flex Text Component
/// </summary>
public record FlexText : IFlexComponent
{
    public string Type { get; init; } = "text";
    public required string Text { get; init; }
    public int? Flex { get; init; }
    public string? Size { get; init; }  // xxs, xs, sm, md, lg, xl, xxl, 3xl, 4xl, 5xl
    public string? Weight { get; init; }  // regular, bold
    public string? Color { get; init; }
    public string? Align { get; init; }  // start, end, center
    public string? Gravity { get; init; }  // top, bottom, center
    public string? Margin { get; init; }
    public bool? Wrap { get; init; }
    public int? MaxLines { get; init; }
    public FlexAction? Action { get; init; }
}

/// <summary>
/// LINE Flex Separator Component
/// </summary>
public record FlexSeparator : IFlexComponent
{
    public string Type { get; init; } = "separator";
    public string? Color { get; init; }
    public string? Margin { get; init; }
}

/// <summary>
/// LINE Flex Spacer Component
/// </summary>
public record FlexSpacer : IFlexComponent
{
    public string Type { get; init; } = "spacer";
    public string? Size { get; init; }
}

/// <summary>
/// Flex Component 基礎介面
/// </summary>
public interface IFlexComponent
{
    string Type { get; }
}

/// <summary>
/// LINE Flex Action (PostbackAction)
/// </summary>
public record FlexPostbackAction : FlexAction
{
    public string Type { get; init; } = "postback";
    public required string Data { get; init; }
    public string? DisplayText { get; init; }
    public string? InputOption { get; init; }
    public string? FillInText { get; init; }
}

/// <summary>
/// LINE Flex Action (UriAction)
/// </summary>
public record FlexUriAction : FlexAction
{
    public string Type { get; init; } = "uri";
    public required string Uri { get; init; }
}

/// <summary>
/// Flex Action 基礎類別
/// </summary>
public abstract record FlexAction;

/// <summary>
/// LINE Flex Bubble Action
/// </summary>
public record FlexBubbleAction : FlexAction
{
    public string Type { get; init; } = "uri";
    public required string Uri { get; init; }
}