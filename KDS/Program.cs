using KDS.Components;
using KDS.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();


// registering the clover service here:
builder.Services.AddHttpClient<CloverServer>();

// orders + bump state shared by every screen, and the loop that checks clover
builder.Services.AddSingleton<KitchenService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<KitchenService>());

// per-screen settings (stored in the browser)
builder.Services.AddScoped<ScreenSettingsService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
