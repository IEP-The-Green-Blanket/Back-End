using GB.Infrastructure; // Make sure this points to where your DbContext is!
using GB.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GB.Infrastructure.Services; // Notice the namespace change here

public class ProceduralSensorAgent : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(10);
    private readonly Random _rng = new();

    // Stateful Environment Memory
    private double _weatherIntensity = 0;
    private int _daysSinceLastMajorSpill = 0;

    public ProceduralSensorAgent(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Kick off the manual terminal listener in the background (fire-and-forget)
        _ = Task.Run(() => ListenForConsoleCommands(stoppingToken), stoppingToken);

        // 2. Start the standard 10-minute automated loop
        using PeriodicTimer timer = new(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await GenerateProceduralEntry(forceSpill: false);
        }
    }

    private async Task ListenForConsoleCommands(CancellationToken stoppingToken)
    {
        await Task.Delay(2000, stoppingToken);

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("\n=======================================================");
        Console.WriteLine(" 🛠️  AGENT MANUAL OVERRIDE ACTIVE");
        Console.WriteLine(" Press [T] to force a standard sensor reading.");
        Console.WriteLine(" Press [S] to force a severe sewage spill anomaly.");
        Console.WriteLine("=======================================================\n");
        Console.ResetColor();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(intercept: true).Key;

                    if (key == ConsoleKey.T)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n[Manual Override] ⚡ Forcing standard reading...");
                        Console.ResetColor();
                        await GenerateProceduralEntry(forceSpill: false);
                    }
                    else if (key == ConsoleKey.S)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("\n[Manual Override] ⚠️ INJECTING SEWAGE SPILL ANOMALY...");
                        Console.ResetColor();
                        _weatherIntensity = 1.0;
                        await GenerateProceduralEntry(forceSpill: true);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                break;
            }

            await Task.Delay(100, stoppingToken);
        }
    }

    private async Task GenerateProceduralEntry(bool forceSpill)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GreenBlanketDbContext>();

        DateTime now = DateTime.UtcNow;
        DateTime sastTime = now.AddHours(2);

        int dayOfYear = sastTime.DayOfYear;
        double hour = sastTime.Hour + (sastTime.Minute / 60.0);
        double totalDays = (sastTime - new DateTime(2020, 1, 1)).TotalDays;

        double climateShift = (Math.Sin(totalDays / 365.25 / 3.1) + Math.Cos(totalDays / 365.25 / 1.7)) / 2.0;
        double season = Math.Cos((dayOfYear - 15) * 2 * Math.PI / 365);

        double rainProbability = season > 0 ? 0.08 : 0.01;
        if (_rng.NextDouble() < rainProbability)
        {
            _weatherIntensity += (_rng.NextDouble() * 0.4) - 0.2;
            _weatherIntensity = Math.Clamp(_weatherIntensity, 0, 1.0);
        }
        else
        {
            _weatherIntensity = Math.Max(0, _weatherIntensity - 0.01);
        }

        // --- ANOMALY INJECTION ---
        bool isSewageSpill = forceSpill;
        _daysSinceLastMajorSpill++;

        if (!isSewageSpill && _rng.NextDouble() < 0.001 + (_weatherIntensity * 0.008))
        {
            isSewageSpill = true;
        }

        if (isSewageSpill)
        {
            _daysSinceLastMajorSpill = 0;
        }

        double diurnalPh = Math.Sin((hour - 8) * 2 * Math.PI / 24);
        double phBase = 7.5 + (climateShift * 0.3);
        double phSwingMulti = 0.15 + (Math.Max(0, season) * 0.5);
        double finalPh = phBase + (diurnalPh * phSwingMulti) + GenerateMicroJitter(0.05);

        double nitrateBase = 1.2 + (climateShift * 0.4) + (season * 0.3);
        double finalNitrates = Math.Max(0, nitrateBase + (_weatherIntensity * 3.0) + (isSewageSpill ? 5.0 : 0) + GenerateMicroJitter(0.1));

        double sulfateBase = 80.0 + (climateShift * 15.0);
        double finalSulfate = Math.Max(20, sulfateBase + (_weatherIntensity * 20.0) + GenerateMicroJitter(2.0));

        double phosphateBase = 0.1 + (climateShift * 0.05);
        double finalPhosphates = Math.Max(0, phosphateBase + (isSewageSpill ? 0.9 : 0) + GenerateMicroJitter(0.02));

        double finalAmmonia = Math.Max(0, 0.05 + (isSewageSpill ? 1.8 : 0) + GenerateMicroJitter(0.01));

        double evaporationConcentration = (season * 5.0);
        double rainDilutionMulti = 1.0 - (_weatherIntensity * 0.4);

        double ecBase = 60.0 - (climateShift * 5.0) + evaporationConcentration;
        double finalEc = Math.Max(10, (ecBase * rainDilutionMulti) + GenerateMicroJitter(1.5));

        double sodiumBase = 45.0 + evaporationConcentration;
        double finalSodium = Math.Max(5, (sodiumBase * rainDilutionMulti) + GenerateMicroJitter(1.0));

        double chlorideBase = 52.0 + evaporationConcentration;
        double finalChloride = Math.Max(5, (chlorideBase * rainDilutionMulti) + GenerateMicroJitter(1.0));

        double calciumBase = 42.0 + (climateShift * 3.0);
        double finalCalcium = Math.Max(10, (calciumBase * rainDilutionMulti) + GenerateMicroJitter(0.5));

        double magnesiumBase = 15.0 + (climateShift * 2.0);
        double finalMagnesium = Math.Max(2, (magnesiumBase * rainDilutionMulti) + GenerateMicroJitter(0.3));

        double alkalinityBase = 105.0 + (climateShift * 5.0);
        double finalAlkalinity = Math.Max(30, alkalinityBase - (_weatherIntensity * 15.0) + GenerateMicroJitter(0.8));

        var reading = new WaterReading
        {
            DateTime = now,
            PhLevel = (double)Math.Round(finalPh, 2),
            ElectricalConductivity = (double)Math.Round(finalEc, 1),
            Nitrates = (double)Math.Round(finalNitrates, 3),
            Phosphates = (double)Math.Round(finalPhosphates, 4),
            Ammonia = (double)Math.Round(finalAmmonia, 3),
            Calcium = (double)Math.Round(finalCalcium, 1),
            Magnesium = (double)Math.Round(finalMagnesium, 1),
            Sodium = (double)Math.Round(finalSodium, 1),
            Chloride = (double)Math.Round(finalChloride, 1),
            Sulfate = (double)Math.Round(finalSulfate, 1),
            TotalAlkalinity = (double)Math.Round(finalAlkalinity, 1)
        };

        context.WaterReadings.Add(reading);
        await context.SaveChangesAsync();

        if (now.Minute % 30 == 0 || isSewageSpill || forceSpill)
        {
            string status = isSewageSpill ? "⚠️ SEWAGE SPILL DETECTED" :
                            _weatherIntensity > 0.6 ? "🌧️ HEAVY RAIN" :
                            _weatherIntensity > 0.2 ? "⛅ OVERCAST" : "☀️ CLEAR";
            Console.WriteLine($"[Eco-Engine] {sastTime:HH:mm:ss} | pH: {reading.PhLevel:F1} | EC: {reading.ElectricalConductivity:F0} | NO3: {reading.Nitrates:F2} | {status}");
        }
    }

    private double GenerateMicroJitter(double scale)
    {
        return (_rng.NextDouble() * scale * 2) - scale;
    }
}