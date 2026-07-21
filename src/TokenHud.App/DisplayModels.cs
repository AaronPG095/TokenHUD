namespace TokenHud.App;

internal sealed record RecentChatDisplayItem(
    string Id,
    string Title,
    string Context,
    string Zone,
    System.Windows.Media.Brush ZoneBrush,
    string AutomationName);

internal sealed record ChatListItem(
    string Id,
    string Title,
    string Meta,
    string Context,
    string Zone,
    System.Windows.Media.Brush ZoneBrush,
    bool Archived,
    bool Pinned);

internal sealed record ThreadDisplayItem(
    string Marker,
    string Name,
    string Context,
    string Zone,
    string Input,
    string Cached,
    string Output,
    string Reasoning,
    string Total);
