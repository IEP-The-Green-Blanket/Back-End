using GB.Application.Interfaces;
using GB.Application.Services;
using Scalar.AspNetCore; // Ensure this is here after installing the package

var builder = WebApplication.CreateBuilder(args);

// --- 1. SERVICE CONFIGURATION (The "Builder" Phase) ---

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddScoped<ChatbotService>();
builder.Services.AddScoped<WaterQualityService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi(); // This generates the "blueprints" (JSON)

// Registers your login logic
builder.Services.AddScoped<IAuthService, AuthService>();

// --- 2. BUILD THE APP ---
var app = builder.Build();

// --- 3. MIDDLEWARE PIPELINE (The "App" Phase) ---

if (app.Environment.IsDevelopment())
{
    // This provides the raw JSON data
    app.MapOpenApi();

    // This creates the actual WEBSITE at /scalar/v1 using that data
    app.MapScalarApiReference();
}

app.UseCors("FrontendPolicy");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();