using KDS.Models;

namespace KDS.Services;

public enum CloverStatus
{
    Demo,       // no merchant id / token in config
    Connecting,
    Live,
    Error
}

// The kitchen's state lives here on the server, so every screen shows the
// same orders and a bump on one screen bumps it everywhere.
// It also checks Clover for new orders every few seconds.
public class KitchenService : BackgroundService
{
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(10);
    private const int HistoryLimit = 200;
    private const int MaxDemoOrders = 12;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<KitchenService> _log;
    private readonly TestOrders _testOrders = new();
    private readonly object _lock = new();
    private readonly Random _random = new();

    private readonly List<OrderCard> _active = new();
    private readonly List<HistoryEntry> _history = new(); // oldest first

    public KitchenService(IServiceScopeFactory scopes, IConfiguration config, ILogger<KitchenService> log)
    {
        _scopes = scopes;
        _log = log;
        MerchantId = config["Clover:MerchantId"] ?? "";
    }

    // something changed, re-render
    public event Action? Changed;

    // new orders came in (for the chime)
    public event Action? OrdersArrived;

    public CloverStatus Status { get; private set; } = CloverStatus.Demo;
    public string? StatusMessage { get; private set; }
    public DateTimeOffset? LastSync { get; private set; }
    public string MerchantId { get; }
    public bool DemoOrders { get; private set; } = true;

    public List<OrderCard> ActiveOrders
    {
        get { lock (_lock) return _active.OrderBy(o => o.time).ToList(); }
    }

    public List<HistoryEntry> History
    {
        get { lock (_lock) return _history.ToList(); }
    }

    public int ItemsLeft
    {
        get { lock (_lock) return _active.Sum(o => o.items.Count(i => !i.isComplete)); }
    }


    // order actions

    public void ToggleItem(string orderId, int index)
    {
        lock (_lock)
        {
            var order = _active.FirstOrDefault(o => o.id == orderId);
            if (order == null || index < 0 || index >= order.items.Count) return;
            order.items[index].isComplete = !order.items[index].isComplete;
        }
        Changed?.Invoke();
    }

    public void ToggleAll(string orderId)
    {
        lock (_lock)
        {
            var order = _active.FirstOrDefault(o => o.id == orderId);
            if (order == null) return;
            var markDone = !order.IsComplete;
            order.items.ForEach(i => i.isComplete = markDone);
        }
        Changed?.Invoke();
    }

    public void Bump(string orderId)
    {
        lock (_lock)
        {
            var order = _active.FirstOrDefault(o => o.id == orderId);
            if (order == null) return;
            _active.Remove(order);
            order.orderStatus = OrderStatus.ready;
            _history.Add(new HistoryEntry { order = order, bumpedAt = DateTimeOffset.UtcNow });
            if (_history.Count > HistoryLimit) _history.RemoveRange(0, _history.Count - HistoryLimit);
        }
        Changed?.Invoke();
    }

    // no id = the last one bumped
    public void Recall(string? orderId = null)
    {
        lock (_lock)
        {
            var entry = orderId == null ? _history.LastOrDefault() : _history.FirstOrDefault(e => e.order.id == orderId);
            if (entry == null) return;
            _history.Remove(entry);
            entry.order.orderStatus = OrderStatus.inProgress;
            entry.order.addedAt = DateTimeOffset.UtcNow;
            _active.Add(entry.order);
        }
        Changed?.Invoke();
    }

    public void AddTestOrder()
    {
        lock (_lock) _active.Add(_testOrders.Make());
        Changed?.Invoke();
        OrdersArrived?.Invoke();
    }

    public void ClearHistory()
    {
        lock (_lock) _history.Clear();
        Changed?.Invoke();
    }

    public void SetDemoOrders(bool on)
    {
        DemoOrders = on;
        Changed?.Invoke();
    }


    // background loop: clover polling, or demo orders when clover isn't set up

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        bool live;
        using (var scope = _scopes.CreateScope())
        {
            live = scope.ServiceProvider.GetRequiredService<CloverServer>().IsConfigured;
        }

        if (!live)
        {
            SeedDemoOrders();
            await RunDemo(stop);
            return;
        }

        Status = CloverStatus.Connecting;
        Changed?.Invoke();

        while (!stop.IsCancellationRequested)
        {
            await PollClover(stop);
            try { await Task.Delay(PollEvery, stop); } catch (TaskCanceledException) { }
        }
    }

    private async Task PollClover(CancellationToken stop)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var clover = scope.ServiceProvider.GetRequiredService<CloverServer>();
            var fetched = await clover.GetRecentOrders(stop);
            var added = Merge(fetched);

            Status = CloverStatus.Live;
            StatusMessage = null;
            LastSync = DateTimeOffset.UtcNow;
            Changed?.Invoke();
            if (added > 0) OrdersArrived?.Invoke();
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Clover poll failed");
            Status = CloverStatus.Error;
            StatusMessage = e is HttpRequestException http && http.StatusCode != null
                ? $"Clover answered {(int)http.StatusCode} {http.StatusCode}. Check the merchant ID and API token."
                : e.Message;
            Changed?.Invoke();
        }
    }

    // add new orders, refresh the items of ones already on the board.
    // returns how many were new
    private int Merge(List<OrderCard> fetched)
    {
        int added = 0;
        lock (_lock)
        {
            foreach (var order in fetched)
            {
                // orders with no items yet are still being rung up
                if (order.items.Count == 0) continue;
                if (_history.Any(e => e.order.id == order.id)) continue;

                var existing = _active.FirstOrDefault(o => o.id == order.id);
                if (existing == null)
                {
                    _active.Add(order);
                    added++;
                    continue;
                }

                // items changed at the register: keep the checkmarks we already have
                var done = existing.items.Where(i => i.isComplete).Select(i => i.GroupKey).ToHashSet();
                order.items.ForEach(i => i.isComplete = done.Contains(i.GroupKey));
                existing.items = order.items;
                existing.paid = order.paid;
                existing.note = order.note;
                existing.customer = order.customer;
            }
        }
        return added;
    }

    private void SeedDemoOrders()
    {
        var now = DateTimeOffset.UtcNow;
        lock (_lock)
        {
            foreach (var minutesAgo in new[] { 12, 6, 2, 0.2 })
            {
                var order = _testOrders.Make(now.AddMinutes(-minutesAgo));
                order.addedAt = now.AddMinutes(-1);
                _active.Add(order);
            }
        }
        Changed?.Invoke();
    }

    // a fake order every 25-60 seconds
    private async Task RunDemo(CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(_random.Next(25, 61)), stop); }
            catch (TaskCanceledException) { return; }

            int count;
            lock (_lock) count = _active.Count;
            if (DemoOrders && count < MaxDemoOrders) AddTestOrder();
        }
    }
}
