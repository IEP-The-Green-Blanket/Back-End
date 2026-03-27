using GB.Application.Interfaces;
using GB.Application.Services;
using GB.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;


var builder = WebApplication.CreateBuilder(args);

// Grab the Database connection string
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
builder.Services.AddHttpClient<ChatbotService>();
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
}

app.UseCors("FrontendPolicy");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();