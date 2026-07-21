namespace TokenHud.App;

internal sealed record ChatListItem(string Id, string Title, string Meta, bool Archived, bool Pinned);

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
