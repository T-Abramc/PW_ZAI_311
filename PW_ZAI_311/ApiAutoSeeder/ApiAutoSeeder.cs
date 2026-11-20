using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PW_ZAI_311.configuration;

namespace PW_ZAI_311
{
    /// <summary>
    /// Automatycznie wprowadza dane przykładowe F11 (3 serie po 18 pomiarów)
    /// WYŁĄCZNIE przez endpointy REST API (z użyciem prawdziwego klucza X-API-Key czujników),
    /// zgodnie z wymaganiami F11 i T11.
    /// </summary>
    public class ApiAutoSeeder : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IServer _server;
        private readonly ILogger<ApiAutoSeeder> _logger;

        public ApiAutoSeeder(IServiceProvider serviceProvider, IServer server, ILogger<ApiAutoSeeder> logger)
        {
            _serviceProvider = serviceProvider;
            _server = server;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Czekamy 3 sekundy, aż IIS Express / Kestrel w pełni wystartuje i zacznie nasłuchiwać
            await Task.Delay(3000, stoppingToken);

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // Sprawdzamy, czy w bazie są już co najmniej 3 serie (pomijając serie testowe ZAI-TEST-)
                var allSeries = await db.MeasurementSeries.ToListAsync(stoppingToken);
                int userSeriesCount = allSeries.Count(s => s.Name != null && !s.Name.StartsWith("ZAI-TEST-"));

                if (userSeriesCount >= 3)
                    return;
                

                // Pobieramy aktualny adres URL, na którym uruchomiony jest serwer (np. https://localhost:44343)
                var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
                var baseUrl = addresses?.FirstOrDefault(a => a.StartsWith("https")) ?? addresses?.FirstOrDefault() ?? "https://localhost:44343";

                _logger.LogInformation("[F11 Seeder] Rozpoczynam seedowanie danych F11 przez API pod adresem: {BaseUrl}", baseUrl);

                // Ignorujemy błędy lokalnego certyfikatu self-signed SSL na localhost
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };

                using var adminClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
                var loginRes = await adminClient.PostAsJsonAsync("/api/auth/login", new { username = "admin", password = "haslo" }, stoppingToken);

                if (!loginRes.IsSuccessStatusCode)
                {
                    _logger.LogError("[F11 Seeder] Nie udało się zalogować na konto admina przez API (Status: {Status}).", loginRes.StatusCode);
                    return;
                }

                var loginJson = await loginRes.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: stoppingToken);
                string accessToken = loginJson.GetProperty("accessToken").GetString()!;
                adminClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                var seriesToCreate = new[]
                {
                    new { Name = "Temperatura Zewnętrzna", Min = -45.0, Max = 67.0, Color = "#e63946", Icon = "thermometer", Unit = "°C", GenMin = 14.0, GenMax = 24.0, Sine = true },
                    new { Name = "Wilgotność Powietrza", Min = 0.0, Max = 100.0, Color = "#457b9d", Icon = "water_drop", Unit = "%", GenMin = 45.0, GenMax = 75.0, Sine = true },
                    new { Name = "Stężenie Pyłu PM2.5", Min = 0.0, Max = 500.0, Color = "#2a9d8f", Icon = "water_drop", Unit = "µg/m³", GenMin = 12.0, GenMax = 55.0, Sine = false }
                };

                foreach (var s in seriesToCreate)
                {
                    // 2. Tworzenie serii przez POST /api/series
                    var serieRes = await adminClient.PostAsJsonAsync("/api/series", new
                    {
                        name = s.Name,
                        minValue = s.Min,
                        maxValue = s.Max,
                        color = s.Color,
                        icon = s.Icon,
                        unit = s.Unit
                    }, stoppingToken);

                    if (!serieRes.IsSuccessStatusCode) continue;

                    var serieJson = await serieRes.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: stoppingToken);
                    var seriesId = serieJson.GetProperty("id").GetGuid();

                    // 3. Rejestracja czujnika przez POST /api/sensors i odebranie jednorazowego klucza API
                    var sensorRes = await adminClient.PostAsJsonAsync("/api/sensors", new
                    {
                        name = $"Czujnik IoT - {s.Name}",
                        seriesId = seriesId
                    }, stoppingToken);

                    if (!sensorRes.IsSuccessStatusCode) continue;

                    var sensorJson = await sensorRes.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: stoppingToken);
                    string plainApiKey = sensorJson.GetProperty("apiKey").GetString()!;

                    // 4. Wysyłanie 18 wyników pomiarów (wymagane min. 15 w F11) przez osobnego klienta używającego WYŁĄCZNIE nagłówka X-API-Key
                    using var sensorClient = new HttpClient(new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                    })
                    { BaseAddress = new Uri(baseUrl) };

                    sensorClient.DefaultRequestHeaders.Add("X-API-Key", plainApiKey);

                    var startTime = DateTime.UtcNow.AddMinutes(-18 * 15);
                    for (int i = 0; i < 18; i++)
                    {
                        double val = s.Sine
                            ? ((s.GenMin + s.GenMax) / 2.0) + ((s.GenMax - s.GenMin) / 2.0) * Math.Sin(i * 0.45)
                            : s.GenMin + Random.Shared.NextDouble() * (s.GenMax - s.GenMin);

                        var measurementPayload = new
                        {
                            value = Math.Round(val, 2),
                            timestamp = startTime.AddMinutes(i * 15).ToString("o")
                        };

                        await sensorClient.PostAsJsonAsync("/api/measurements", measurementPayload, stoppingToken);
                    }

                    _logger.LogInformation("[F11 Seeder] Utworzono serię '{Name}' i przesłano 18 pomiarów kluczem czujnika.", s.Name);
                }

                _logger.LogInformation("[F11 Seeder] Seedowanie F11 przez API zakończone sukcesem!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[F11 Seeder] Wystąpił błąd podczas automatycznego seedowania przez API.");
            }
        }
    }
}