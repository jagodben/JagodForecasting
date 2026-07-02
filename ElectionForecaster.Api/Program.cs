using System.Threading.RateLimiting;
using ElectionForecaster.Core.Interfaces;
using ElectionForecaster.Infrastructure.Data;
using ElectionForecaster.Infrastructure.DataSources.Approval;
using ElectionForecaster.Infrastructure.DataSources.Fundamentals;
using ElectionForecaster.Infrastructure.DataSources.Interfaces;
using ElectionForecaster.Infrastructure.DataSources.Polling;
using ElectionForecaster.Infrastructure.DataSources.PredictionMarkets;
using ElectionForecaster.Infrastructure.Forecasting;
using ElectionForecaster.Infrastructure.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
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

// Configure SQLite database
builder.Services.AddDbContext<ForecastDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("ForecastDb")));

// Register core services
builder.Services.AddSingleton<IStateService, StateService>();
builder.Services.AddSingleton<IRaceService, RaceService>();
builder.Services.AddSingleton<IDistrictService, DistrictService>();

// Register HttpClient for data sources
builder.Services.AddHttpClient<PolymarketClient>();
builder.Services.AddHttpClient<WikipediaPollingClient>();
builder.Services.AddHttpClient<ApprovalAggregator>();

// Register data sources
builder.Services.AddScoped<IPredictionMarketSource, PolymarketClient>();
builder.Services.AddScoped<WikipediaPollingClient>();
builder.Services.AddScoped<IPollingSource>(sp => sp.GetRequiredService<WikipediaPollingClient>());
builder.Services.AddScoped<IFundamentalsSource, CookPVIProvider>();
builder.Services.AddScoped<IApprovalSource, ApprovalAggregator>();

// Register forecasting components
builder.Services.AddSingleton<WeightCalculator>();
builder.Services.AddSingleton<MonteCarloSimulator>();
builder.Services.AddScoped<IForecastingOrchestrator, ForecastingOrchestrator>();

// Register background service for data refresh
builder.Services.AddHostedService<DataRefreshService>();

// Configure rate limiting
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

// Configure CORS for React frontend
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

// Ensure database is created
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ForecastDbContext>();
    dbContext.Database.EnsureCreated();
}

// Configure the HTTP request pipeline

// Only enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Election Forecaster API v1");
    });
    app.UseHttpsRedirection();
}

app.UseCors("ReactApp");

// Rate limiting
app.UseRateLimiter();

app.UseAuthorization();
app.MapControllers().RequireRateLimiting("api");

// Health check endpoint (no auth required)
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
