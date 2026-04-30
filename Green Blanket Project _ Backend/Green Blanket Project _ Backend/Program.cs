using GB.Application.Interfaces;
using GB.Application.Services;
using GB.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Renci.SshNet;
using Scalar.AspNetCore;
using GB.Infrastructure.Services;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// 1. SSH TUNNEL (Dev Only)
if (builder.Environment.IsDevelopment())
{
    var sshHost = builder.Configuration["SshConfiguration:Host"];
    var sshPortStr = builder.Configuration["SshConfiguration:Port"];
    var sshPort = string.IsNullOrEmpty(sshPortStr) ? 22 : int.Parse(sshPortStr);
    var sshUser = builder.Configuration["SshConfiguration:Username"];
    var sshPassword = builder.Configuration["SshConfiguration:Password"];

    if (!string.IsNullOrEmpty(sshHost) && !string.IsNullOrEmpty(sshUser))
    {
        try
        {
            var sshClient = new SshClient(sshHost, sshPort, sshUser, sshPassword);
            sshClient.Connect();
            var port = new ForwardedPortLocal("127.0.0.1", 54320, "127.0.0.1", 5432);
            sshClient.AddForwardedPort(port);
            port.Start();
            builder.Services.AddSingleton(sshClient);
            Console.WriteLine(">>> SSH Tunnel Connected Successfully! <<<");
        }
        catch (Exception ex) { Console.WriteLine($"!!! SSH Tunnel Failed: {ex.Message} !!!"); }
    }
}

// DB Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// 2. SERVICES
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddMemoryCache();

builder.Services.AddDbContext<GreenBlanketDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(60);
        npgsqlOptions.EnableRetryOnFailure(3);
    })
    .UseSnakeCaseNamingConvention());

builder.Services.AddHealthChecks().AddNpgSql(connectionString);

builder.Services.AddCors(options => {
    options.AddPolicy("FrontendPolicy", policy => {
        policy.WithOrigins("http://localhost:3000", "https://greenblanket.crabdance.com")
              .AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddScoped<WaterQualityService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHostedService<ProceduralSensorAgent>();

var baseUrl = builder.Configuration["ApiBaseUrl"];
builder.Services.AddHttpClient<ChatbotService>(client => {
    client.BaseAddress = new Uri(baseUrl ?? "https://localhost:5050");
});

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 4. MIDDLEWARE
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FrontendPolicy");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseAuthorization();
app.MapControllers();

app.Run();