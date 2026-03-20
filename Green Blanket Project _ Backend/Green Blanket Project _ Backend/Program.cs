using System.Xml.Schema;
using GB.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:8080")
              .AllowAnyHeader()
              .AllowAnyMethod();
              
    });
});

builder.Services.AddScoped<WaterQualityService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("FrontendPolicy");

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();