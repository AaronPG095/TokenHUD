using System.ComponentModel;
using Brush = System.Windows.Media.Brush;

namespace TokenHud.App;

internal sealed class RecentChatDisplayItem(string id) : INotifyPropertyChanged
{
    public string Id { get; } = id;
    public string Title { get; private set; } = string.Empty;
    public string Context { get; private set; } = string.Empty;
    public string Zone { get; private set; } = string.Empty;
    public Brush? ZoneBrush { get; private set; }
    public string AutomationName { get; private set; } = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(string title, string context, string zone, Brush zoneBrush, string automationName)
    {
        if (Title == title && Context == context && Zone == zone && ZoneBrush == zoneBrush && AutomationName == automationName)
        {
            return;
        }

        Title = title;
        Context = context;
        Zone = zone;
        ZoneBrush = zoneBrush;
        AutomationName = automationName;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}

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
