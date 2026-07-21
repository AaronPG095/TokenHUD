namespace TokenHud.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\TokenHUD.SingleInstance.5EB7A6C9";
    private const string EventName = @"Local\TokenHUD.Show.5EB7A6C9";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private volatile bool _stopping;
    private Task? _listener;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsPrimary = createdNew;
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
    }

    public bool IsPrimary { get; }

    public void SignalPrimary() => _showEvent.Set();

    public void StartListening(Action show)
    {
        if (!IsPrimary)
        {
            return;
        }

        _listener = Task.Run(() =>
        {
            while (!_stopping)
            {
                _showEvent.WaitOne();
                if (!_stopping)
                {
                    show();
                }
            }
        });
    }

    public void Dispose()
    {
        _stopping = true;
        _showEvent.Set();
        try
        {
            _listener?.Wait(TimeSpan.FromSeconds(1));
        }
        catch (AggregateException)
        {
        }

        _showEvent.Dispose();
        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
    }
}
