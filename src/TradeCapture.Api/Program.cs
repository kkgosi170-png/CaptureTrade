using System.Text.Json;
using System.Text.Json.Serialization;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.Description;
using TradeCapture.Api.Data;
using TradeCapture.Api.Services;
using TradeCapture.Api.Soap;
using TradeCapture.Api.Soap.Contracts;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TradeCapture")
    ?? throw new InvalidOperationException("Missing connection string 'TradeCapture'.");

builder.Services.AddSingleton(new SqlConnectionFactory(connectionString));
builder.Services.AddScoped<ITradeRepository, TradeRepository>();
builder.Services.AddScoped<TradeIngestionService>();
builder.Services.AddScoped<ReportingService>();

// --- CoreWCF: host the SOAP currency rate service (server side) ------------------------
builder.Services.AddServiceModelServices();
builder.Services.AddServiceModelMetadata();
builder.Services.AddTransient<CurrencyRateSoapService>(); // resolved by CoreWCF per call
// ---------------------------------------------------------------------------------------

// --- Enrichment seam: choose how trades are enriched (config-driven) -------------------
// "Soap" (default) calls the CoreWCF endpoint via a WCF client; "Stub" reads the table directly.
var provider = builder.Configuration["Enrichment:Provider"] ?? "Soap";
if (provider.Equals("Stub", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<ICurrencyRateService, StubCurrencyRateService>();
}
else
{
    var soapUrl = builder.Configuration["Soap:CurrencyServiceUrl"]
        ?? throw new InvalidOperationException("Missing 'Soap:CurrencyServiceUrl'.");

    // One ChannelFactory is shared (thread-safe); a lightweight channel is created per call.
    builder.Services.AddSingleton(_ => new System.ServiceModel.ChannelFactory<ICurrencyRateSoapClient>(
        new System.ServiceModel.BasicHttpBinding(),
        new System.ServiceModel.EndpointAddress(soapUrl)));
    builder.Services.AddScoped<ICurrencyRateService, WcfCurrencyRateService>();
}
// ---------------------------------------------------------------------------------------

builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Match the snake_case shapes shown in the assignment (external_id, trade_time, total_qty, ...).
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Turnkey demo setup: create DB + apply idempotent schema/seed on startup.
await DbInitializer.InitializeAsync(app.Configuration, app.Environment,
    app.Services.GetRequiredService<ILogger<Program>>());

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Map the SOAP endpoint and expose WSDL (browse to /soap/currency?wsdl).
app.UseServiceModel(serviceBuilder =>
{
    serviceBuilder.AddService<CurrencyRateSoapService>(options =>
        options.DebugBehavior.IncludeExceptionDetailInFaults = app.Environment.IsDevelopment());

    serviceBuilder.AddServiceEndpoint<CurrencyRateSoapService, ICurrencyRateSoapContract>(
        new CoreWCF.BasicHttpBinding(), "/soap/currency");
});
app.Services.GetRequiredService<ServiceMetadataBehavior>().HttpGetEnabled = true;

app.MapControllers();

app.Run();

// Exposed so integration tests can host the app via WebApplicationFactory.
public partial class Program { }
