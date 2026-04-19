using GB.Application.Interfaces;
using GB.Application.Services;
using GB.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using Renci.SshNet; // The SSH Tunnel Library
using Scalar.AspNetCore;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. SSH TUNNEL (The Bridge to Harties DB)
// ==========================================
if (builder.Environment.IsDevelopment())
{
    var sshHost = builder.Configuration["SshConfiguration:Host"];
    var sshPortStr = builder.Configuration["SshConfiguration:Port"];
    var sshPort = string.IsNullOrEmpty(sshPortStr) ? 22 : int.Parse(sshPortStr);
    var sshUser = builder.Configuration["SshConfiguration:Username"];
    var sshPassword = builder.Configuration["SshConfiguration:Password"];

    if (!string.IsNullOrEmpty(sshHost) && !string.IsNullOrEmpty(sshUser) && !sshUser.Contains("YOUR_USERNAME"))
    {
        try
        {
            var sshClient = new SshClient(sshHost, sshPort, sshUser, sshPassword);
            sshClient.Connect();

            var port = new ForwardedPortLocal("127.0.0.1", 54320, "127.0.0.1", 5432);
            sshClient.AddForwardedPort(port);
            port.Start();

            Thread.Sleep(2000);

            Console.WriteLine(">>> SSH Tunnel Connected Successfully! <<<");
            Console.WriteLine(">>> Port Forwarding Active on 127.0.0.1:54320 <<<");

            // THE FIX: Save the client into the app's services so the Garbage Collector doesn't kill it!
            builder.Services.AddSingleton(sshClient);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"!!! SSH Tunnel Failed to Start: {ex.Message} !!!");
        }
    }
    else
    {
        Console.WriteLine(">>> SSH Tunnel Skipped: Placeholders detected in appsettings.Development.json <<<");
    }
}

// Grab the Database connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ==========================================
// 2. SERVICES (Dependency Injection)
// ==========================================

// A. Entity Framework
builder.Services.AddDbContext<GreenBlanketDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());

// B. Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "Harties-Remote-DB");

// C. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://greenblanket.crabdance.com")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// D. Application Services
builder.Services.AddScoped<WaterQualityService>();
builder.Services.AddScoped<IAuthService, AuthService>();

var baseUrl = builder.Configuration["ApiBaseUrl"];

builder.Services.AddHttpClient<ChatbotService>(client =>
{
    client.BaseAddress = new Uri(baseUrl ?? "https://localhost:5050");
}); // Kept your chatbot!

// E. API Tools
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ==========================================
// 3. BUILD THE APP
// ==========================================
var app = builder.Build();

// ==========================================
// 4. MIDDLEWARE PIPELINE
// ==========================================
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // Kept your Scalar UI!

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/api/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var isDev = app.Environment.IsDevelopment();
        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds + "ms",
            info = isDev ? report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                error = e.Value.Exception?.Message ?? "None"
            }) : null
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        }));
    }
});

Console.WriteLine($"Base URL: {baseUrl}");

app.UseCors("FrontendPolicy");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

app.Run();