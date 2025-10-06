// Program.cs
using ABCRetailers.Services;
using Microsoft.AspNetCore.Http.Features;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Typed HttpClient for your Azure Functions
builder.Services.AddHttpClient("Functions", (sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var baseUrl = cfg["Functions:BaseUrl"] ?? throw new InvalidOperationException("Functions:BaseUrl missing");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/api/"); // adjust if your Functions don't use /api
    client.Timeout = TimeSpan.FromSeconds(100);
});

// Use the typed client (replaces IAzureStorageService everywhere)
builder.Services.AddScoped<IFunctionsApi, FunctionsApiClient>();

// Optional: allow larger multipart uploads (images, proofs, etc.)
builder.Services.Configure<FormOptions>(o =>
{
    o.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB
});

// Optional: logging is added by default, keeping this is harmless
builder.Services.AddLogging();

var app = builder.Build();

// Set culture to South Africa (for Rand currency symbol)
var culture = new CultureInfo("en-ZA");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
