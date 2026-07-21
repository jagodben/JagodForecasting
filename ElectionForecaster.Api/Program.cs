using System.IO.Compression;
using System.Threading.RateLimiting;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.DataSources.Candidates;
using ElectionForecaster.Infrastructure.DataSources.Fundamentals;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Polling;
using ElectionForecaster.Infrastructure.DataSources.PredictionMarkets;
using ElectionForecaster.Infrastructure.Forecasting;
using ElectionForecaster.Infrastructure.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Election Forecaster API", Version = "v1" });
});

builder.Services.AddDbContext<ForecastDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ForecastDb")));

builder.Services.AddMemoryCache();

// Compress the large forecast JSON payloads (the House batch is ~500KB uncompressed).
// Brotli "Optimal" maps to quality 11 in .NET and is pathologically slow, so both
// providers run at Fastest — still a ~5x reduction with negligible added latency.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);
builder.Services.Configure<GzipCompressionProviderOptions>(o => o.Level = CompressionLevel.Fastest);

builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IRaceService, RaceService>();

builder.Services.AddHttpClient<PolymarketClient>();
builder.Services.AddHttpClient<WikipediaPollingClient>();
builder.Services.AddHttpClient<WikipediaGenericBallotClient>();
builder.Services.AddHttpClient<WikipediaCandidateClient>();

builder.Services.AddScoped<IPredictionMarketSource, PolymarketClient>();
builder.Services.AddScoped<WikipediaPollingClient>();
builder.Services.AddScoped<IPollingSource>(sp => sp.GetRequiredService<WikipediaPollingClient>());
builder.Services.AddScoped<IFundamentalsSource, PartisanLeanProvider>();
builder.Services.AddScoped<WikipediaGenericBallotClient>();
builder.Services.AddScoped<IGenericBallotSource>(sp => sp.GetRequiredService<WikipediaGenericBallotClient>());
builder.Services.AddScoped<CandidateRefreshService>();

builder.Services.AddSingleton<WeightCalculator>();
builder.Services.AddSingleton<MonteCarloSimulator>();
builder.Services.AddScoped<IForecastingOrchestrator, ForecastingOrchestrator>();

builder.Services.AddHostedService<DataRefreshService>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.AddFixedWindowLimiter("api", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100; // 100 requests per window
        limiterOptions.Window = TimeSpan.FromMinutes(1); // 1 minute window
        limiterOptions.QueueLimit = 10;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactApp", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5050", "http://localhost:5173" };

        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => new Uri(origin).Host == "localhost")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

var app = builder.Build();

// Apply migrations (baselining any legacy EnsureCreated database)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ForecastDbContext>();
    ForecastDbInitializer.Initialize(dbContext);

    // Re-apply the last scraped nominees to the in-memory races, so a restart doesn't fall back
    // to the compile-time candidate data until the next daily refresh.
    var candidateRefresh = scope.ServiceProvider.GetRequiredService<CandidateRefreshService>();
    await candidateRefresh.ApplyStoredOverridesAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Election Forecaster API v1");
    });
    app.UseHttpsRedirection();
}

// Compress responses before anything writes to the body
app.UseResponseCompression();

app.UseCors("ReactApp");

app.UseRateLimiter();

app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");

// Liveness plus data freshness: one URL to confirm the daily pipeline is actually keeping up
// (latest snapshot/poll/ballot dates), for a weekly glance or an uptime monitor. Always 200 as
// long as the process is up — staleness is reported, not fatal, so a source hiccup can't put
// the service in a restart loop.
app.MapGet("/health", async (ForecastDbContext db) =>
{
    var body = new Dictionary<string, object?> { ["status"] = "healthy" };
    try
    {
        var latestSnapshot = await db.ForecastHistory.MaxAsync(f => (DateTime?)f.Date);
        body["latestSnapshot"] = latestSnapshot?.ToString("yyyy-MM-dd");
        body["snapshotRaces"] = latestSnapshot == null
            ? 0
            : await db.ForecastHistory.CountAsync(f => f.Date == latestSnapshot);
        body["latestPoll"] = (await db.Polls.MaxAsync(p => (DateTime?)p.Date))?.ToString("yyyy-MM-dd");
        body["latestGenericBallot"] = (await db.GenericBallot.MaxAsync(g => (DateTime?)g.Date))?.ToString("yyyy-MM-dd");
    }
    catch (Exception ex)
    {
        body["dataError"] = ex.GetType().Name;
    }
    return Results.Ok(body);
});

app.Run();
