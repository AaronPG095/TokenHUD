namespace TokenHud.App;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string MutexName = @"Local\TokenHUD.SingleInstance.5EB7A6C9";
    private const string EventName = @"Local\TokenHUD.Show.5EB7A6C9";
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _showEvent;
    private volatile bool _stopping;
    private bool _disposed;
    private Task? _listener;

    public SingleInstanceCoordinator()
    {
        _mutex = new Mutex(initiallyOwned: false, MutexName);
        try
        {
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, EventName);
        }
        catch
        {
            _mutex.Dispose();
            throw;
        }

        try
        {
            IsPrimary = _mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            IsPrimary = true;
        }
    }

    public bool IsPrimary { get; }

    public void SignalPrimary() => _showEvent.Set();

    public void StartListening(Action show)
    {
        if (!IsPrimary || _listener is not null || _disposed)
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
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _stopping = true;
        try
        {
            if (_listener is not null)
            {
                _showEvent.Set();
                _listener.GetAwaiter().GetResult();
            }
        }
        finally
        {
            _showEvent.Dispose();
            if (IsPrimary)
            {
                _mutex.ReleaseMutex();
            }

            _mutex.Dispose();
        }
    }
}
