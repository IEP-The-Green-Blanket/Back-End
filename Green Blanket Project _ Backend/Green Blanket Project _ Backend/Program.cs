using GB.Application.Interfaces;
using GB.Application.Services;
using GB.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using Renci.SshNet; // The SSH Tunnel Library

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CONFIGURATION & SSH TUNNEL
// ==========================================

// Grab SSH details from appsettings.json
var sshHost = builder.Configuration["SshConfiguration:Host"];
var sshPortStr = builder.Configuration["SshConfiguration:Port"];
var sshPort = string.IsNullOrEmpty(sshPortStr) ? 22 : int.Parse(sshPortStr);
var sshUser = builder.Configuration["SshConfiguration:Username"];
var sshPassword = builder.Configuration["SshConfiguration:Password"];

// Build the tunnel BEFORE setting up the database
if (!string.IsNullOrEmpty(sshHost) && !string.IsNullOrEmpty(sshUser))
{
    var sshClient = new SshClient(sshHost, sshPort, sshUser, sshPassword);
    try
    {
        sshClient.Connect();
        Console.WriteLine(">>> SSH Tunnel Connected Successfully! <<<");

        // We route traffic from YOUR laptop (Port 54320) through the tunnel to THEIR localhost (Port 5432)
        var port = new ForwardedPortLocal("127.0.0.1", 54320, "127.0.0.1", 5432);
        sshClient.AddForwardedPort(port);
        port.Start();
        Console.WriteLine(">>> Port Forwarding Started on 127.0.0.1:54320 <<<");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"!!! SSH Connection Failed: {ex.Message} !!!");
    }
}

// Grab the Database connection string (This now points to the local tunnel entrance)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ==========================================
// 2. SERVICES (The Dependency Injection Container)
// ==========================================

// A. Entity Framework (The Database Bridge)
builder.Services.AddDbContext<GreenBlanketDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention());

// B. Health Checks (The "Check Engine" Light)
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "Harties-Remote-DB");

// C. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// D. Application Services
builder.Services.AddScoped<WaterQualityService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// E. Core API Requirements
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// ==========================================
// 3. BUILD THE APP
// ==========================================
var app = builder.Build();

// ==========================================
// 4. MIDDLEWARE PIPELINE (The Request Journey)
// ==========================================

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();

    // Swagger
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds + "ms",
            info = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                error = e.Value.Exception?.Message ?? "None"
            })
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
    }
});

app.UseCors("FrontendPolicy");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();