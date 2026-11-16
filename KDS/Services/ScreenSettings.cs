using Microsoft.JSInterop;

namespace KDS.Services;

// Settings for this one screen (text size, colors, remote buttons..).
// Saved in the browser's localStorage by wwwroot/js/kds.js, so a screen on
// the line and one at the pass can be set up differently.
public class ScreenSettings
{
    public double TextScale { get; set; } = 1; // 1 | 1.15 | 1.3
    public bool HighContrast { get; set; }
    public bool StatusLabels { get; set; }
    public bool ReduceMotion { get; set; }
    public bool DarkBoard { get; set; }
    public int WarnAfterMin { get; set; } = 5;
    public int LateAfterMin { get; set; } = 10;
    public bool Sound { get; set; }
    public bool RemoteEnabled { get; set; } = true;
    public bool RemoteHints { get; set; } = true;
    public bool SidebarCollapsed { get; set; }
    // same defaults as kds.js
    public Dictionary<string, List<string>> RemoteKeys { get; set; } = new()
    {
        ["left"] = new() { "ArrowLeft" },
        ["right"] = new() { "ArrowRight" },
        ["up"] = new() { "ArrowUp" },
        ["down"] = new() { "ArrowDown" },
        ["select"] = new() { "Enter", " " },
        ["bump"] = new() { "b", "." },
        ["next"] = new() { "PageDown" },
        ["prev"] = new() { "PageUp" },
        ["recall"] = new() { "r" },
        ["clear"] = new() { "Escape", "Backspace", "GoBack", "BrowserBack" },
    };
}

// one per browser tab (scoped = per Blazor circuit)
public class ScreenSettingsService
{
    private readonly IJSRuntime _js;

    public ScreenSettingsService(IJSRuntime js)
    {
        _js = js;
    }

    public ScreenSettings Current { get; private set; } = new();
    public bool Loaded { get; private set; }
    public TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Local;

    public event Action? Changed;

    // call from OnAfterRenderAsync (JS isn't available while prerendering)
    public async Task LoadAsync()
    {
        if (Loaded) return;
        Current = await _js.InvokeAsync<ScreenSettings>("KDS.settings.load");
        var zone = await _js.InvokeAsync<string>("KDS.timeZone");
        if (!string.IsNullOrEmpty(zone) && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out var tz))
        {
            TimeZone = tz;
        }
        Loaded = true;
        Changed?.Invoke();
    }

    public async Task SaveAsync()
    {
        // keep warning before late
        if (Current.LateAfterMin <= Current.WarnAfterMin) Current.LateAfterMin = Current.WarnAfterMin + 1;
        await _js.InvokeVoidAsync("KDS.settings.save", Current);
        Changed?.Invoke();
    }

    public async Task ResetAsync()
    {
        Current = await _js.InvokeAsync<ScreenSettings>("KDS.settings.reset");
        Changed?.Invoke();
    }

    public async Task ResetRemoteKeysAsync()
    {
        Current.RemoteKeys = await _js.InvokeAsync<Dictionary<string, List<string>>>("KDS.settings.defaultRemoteKeys");
        await SaveAsync();
    }

    // order times are stored in UTC, show them in the screen's time zone
    public string Clock(DateTimeOffset time) => TimeZoneInfo.ConvertTime(time, TimeZone).ToString("h:mm tt");

    public Task Beep() => _js.InvokeVoidAsync("KDS.beep").AsTask();
}
