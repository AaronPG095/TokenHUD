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

internal abstract class DisplayRow<T>(string id, T values) : INotifyPropertyChanged
{
    public string Id { get; } = id;
    protected T Values { get; private set; } = values;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Update(T next)
    {
        if (EqualityComparer<T>.Default.Equals(Values, next))
        {
            return;
        }

        Values = next;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
    }
}

internal sealed class ChatListItem(string id, ChatRowValues values) : DisplayRow<ChatRowValues>(id, values)
{
    public string Title => Values.Title;
    public string Meta => Values.Meta;
    public string Context => Values.Context;
    public string Zone => Values.Zone;
    public Brush ZoneBrush => Values.ZoneBrush;
    public bool Archived => Values.Archived;
    public bool Pinned => Values.Pinned;
}

internal sealed record ChatRowValues(
    string Title,
    string Meta,
    string Context,
    string Zone,
    Brush ZoneBrush,
    bool Archived,
    bool Pinned);

internal sealed class ThreadDisplayItem(string id, ThreadRowValues values) : DisplayRow<ThreadRowValues>(id, values)
{
    public string Marker => Values.Marker;
    public string Name => Values.Name;
    public string Context => Values.Context;
    public string Zone => Values.Zone;
    public string Input => Values.Input;
    public string Cached => Values.Cached;
    public string Output => Values.Output;
    public string Reasoning => Values.Reasoning;
    public string Total => Values.Total;
}

internal sealed record ThreadRowValues(
    string Marker,
    string Name,
    string Context,
    string Zone,
    string Input,
    string Cached,
    string Output,
    string Reasoning,
    string Total);
