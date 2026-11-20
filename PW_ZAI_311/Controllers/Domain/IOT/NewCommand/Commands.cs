namespace PW_ZAI_311.Controllers.Domain.IOT.NewCommand
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IdentityModel.Tokens.Jwt;
    using System.Linq;
    using System.Security.Claims;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using global::PW_ZAI_311.configuration;
    using global::PW_ZAI_311.Entieties;
    using MediatR;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.IdentityModel.Tokens;

    namespace PW_ZAI_311.Application
    {
        #region Helpers & Exceptions (Obsługa kluczy API, czarnej listy JWT i błędów HTTP 401/409/422)

        /// <summary>
        /// Pomocniczy rejestr w pamięci pozwalający odebrać jednorazowy jawny klucz API
        /// po wywołaniu CreateSensorCommand (który zwraca samo Guid).
        /// </summary>
        public static class SensorKeyRegistry
        {
            private static readonly ConcurrentDictionary<Guid, string> _oneTimeKeys = new();

            public static void StorePlainKey(Guid sensorId, string plainApiKey)
            {
                _oneTimeKeys[sensorId] = plainApiKey;
            }

            /// <summary>
            /// Pobiera i natychmiast usuwa jawny klucz z pamięci (zgodnie z F12 i T5.5 klucz jest ujawniany tylko raz).
            /// </summary>
            public static string? TakePlainKey(Guid sensorId)
            {
                return _oneTimeKeys.TryRemove(sensorId, out var key) ? key : null;
            }
        }

        /// <summary>
        /// Czarna lista wylogowanych tokenów JWT dla POST /api/auth/logout
        /// </summary>
        public static class TokenBlacklist
        {
            private static readonly ConcurrentDictionary<string, DateTime> _revokedTokens = new();

            public static void Revoke(string token)
            {
                if (!string.IsNullOrWhiteSpace(token))
                {
                    _revokedTokens[token.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim()] = DateTime.UtcNow;
                }
            }

            public static bool IsRevoked(string? token)
            {
                if (string.IsNullOrWhiteSpace(token)) return false;
                return _revokedTokens.ContainsKey(token.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim());
            }
        }

        /// <summary>
        /// Wyjątek domenowy pozwalający przekazać dokładny kod HTTP (401, 403, 404, 409, 422) z Handlera do Kontrolera
        /// </summary>
        public class ApiStatusException : Exception
        {
            public int StatusCode { get; }
            public string Title { get; }

            public ApiStatusException(int statusCode, string title, string message) : base(message)
            {
                StatusCode = statusCode;
                Title = title;
            }
        }

        #endregion

        #region 1. SystemController Commands & Queries (/api/logs)

        public record GetAllLogsQuery() : IRequest<List<Log>>;

        public class GetAllLogsQueryHandler : IRequestHandler<GetAllLogsQuery, List<Log>>
        {
            private readonly AppDbContext _context;

            public GetAllLogsQueryHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<List<Log>> Handle(GetAllLogsQuery request, CancellationToken cancellationToken)
            {
                return await _context.Logs
                    .OrderByDescending(x => x.CreationDataTime)
                    .ToListAsync(cancellationToken);
            }
        }

        #endregion

        #region 2. AuthController Commands & Queries (/api/auth/login, /api/auth/logout, /api/auth/password)

        public record LoginCommand(string Username, string Password) : IRequest<LoginResponseDto?>;

        public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponseDto?>
        {
            private readonly AppDbContext _context;
            private readonly IConfiguration _configuration;

            public LoginCommandHandler(AppDbContext context, IConfiguration configuration)
            {
                _context = context;
                _configuration = configuration;
            }

            public async Task<LoginResponseDto?> Handle(LoginCommand request, CancellationToken cancellationToken)
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

                var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                if (user == null || !isValid)
                {
                    var errorLog = Log.CreateForError(
                        entity: new { request.Username },
                        command: new { request.Username },
                        errorObject: "InvalidCredentials",
                        errorString: "Nieudana próba logowania administratora.",
                        httpStatusCode: 401);

                    _context.Logs.Add(errorLog);
                    await _context.SaveChangesAsync(cancellationToken);

                    return null;
                }

                var keyString = _configuration["Jwt:Key"] ?? "TwojBardzoTajnyKluczKtoryMaMinimum32ZnakiDlugosci!";
                var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
                var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
                int expiresInSeconds = 3600;

                var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(ClaimTypes.Name, user.Username),
                new(ClaimTypes.Role, user.Role),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

                var token = new JwtSecurityToken(
                    claims: claims,
                    expires: DateTime.UtcNow.AddSeconds(expiresInSeconds),
                    signingCredentials: creds);

                var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

                return new LoginResponseDto
                {
                    AccessToken = tokenString,
                    TokenType = "Bearer",
                    ExpiresIn = expiresInSeconds
                };
            }
        }

        public record LogoutCommand(string? BearerToken) : IRequest<bool>;

        public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
        {
            public Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
            {
                TokenBlacklist.Revoke(request.BearerToken ?? string.Empty);
                return Task.FromResult(true);
            }
        }

        public enum PasswordChangeStatus
        {
            Success,
            UserNotFound,
            InvalidCurrentPassword, // Zwróć 403 Forbidden w kontrolerze zgodnie z zai-api-26z.yaml
            TooShort                // Zwróć 422 Unprocessable Entity w kontrolerze
        }

        public record ChangePasswordCommand(string Username, string CurrentPassword, string NewPassword) : IRequest<PasswordChangeStatus>;

        public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, PasswordChangeStatus>
        {
            private readonly AppDbContext _context;

            public ChangePasswordCommandHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<PasswordChangeStatus> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
            {
                if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
                {
                    return PasswordChangeStatus.TooShort;
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

                // Fallback na domyślne konto admin, jeśli w Claims nie przekazano nazwy
                user ??= await _context.Users.FirstOrDefaultAsync(cancellationToken);

                if (user == null)
                {
                    return PasswordChangeStatus.UserNotFound;
                }

                var isValid = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash);
                    if (!isValid)
                {
                    return PasswordChangeStatus.InvalidCurrentPassword;
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                _context.Users.Update(user);
                await _context.SaveChangesAsync(cancellationToken);

                return PasswordChangeStatus.Success;
            }
        }

        #endregion

        #region 3. SeriesController Commands & Queries (/api/series)

        public record GetAllMeasurementSeriesQuery() : IRequest<List<MeasurementSerie>>;

        public class GetAllMeasurementSeriesQueryHandler : IRequestHandler<GetAllMeasurementSeriesQuery, List<MeasurementSerie>>
        {
            private readonly AppDbContext _context;

            public GetAllMeasurementSeriesQueryHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<List<MeasurementSerie>> Handle(GetAllMeasurementSeriesQuery request, CancellationToken cancellationToken)
            {
                return await _context.MeasurementSeries
                    .OrderBy(s => s.CreationDataTime)
                    .ToListAsync(cancellationToken);
            }
        }

        public record GetMeasurementSerieQuery(Guid Id) : IRequest<MeasurementSerie?>;

        public class GetMeasurementSerieQueryHandler : IRequestHandler<GetMeasurementSerieQuery, MeasurementSerie?>
        {
            private readonly AppDbContext _context;

            public GetMeasurementSerieQueryHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<MeasurementSerie?> Handle(GetMeasurementSerieQuery request, CancellationToken cancellationToken)
            {
                return await _context.MeasurementSeries
                    .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            }
        }

        public record CreateMeasurementSerieCommand(
            string Name,
            float MaxValue,
            float MinValue,
            string Color,
            string? IconInfo,
            string? Unit = null) : IRequest<Guid>;

        public class CreateMeasurementSerieCommandHandler : IRequestHandler<CreateMeasurementSerieCommand, Guid>
        {
            private readonly AppDbContext _context;

            public CreateMeasurementSerieCommandHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<Guid> Handle(CreateMeasurementSerieCommand request, CancellationToken cancellationToken)
            {
                if (request.MinValue >= request.MaxValue)
                {
                    var log = Log.CreateForError(request, request, "MinGreaterOrEqualMax", "MinValue musi być mniejsze niż MaxValue.", 422);
                    _context.Logs.Add(log);
                    await _context.SaveChangesAsync(cancellationToken);
                    throw new ApiStatusException(422, "Unprocessable Entity", "minValue must be strictly less than maxValue.");
                }

                var serie = new MeasurementSerie(
                    request.Name,
                    request.MaxValue,
                    request.MinValue,
                    request.Color,
                    request.IconInfo,
                    request.Unit);

                _context.MeasurementSeries.Add(serie);
                await _context.SaveChangesAsync(cancellationToken);

                return serie.Id;
            }
        }

        public enum UpdateSerieResult
        {
            Success,
            NotFound,           // 404 Not Found
            InvalidRange,       // 422 Unprocessable Entity (minValue >= maxValue)
            ConflictWithData    // 409 Conflict (istniejące pomiary wypadają poza nowy zakres [minValue, maxValue])
        }

        public record UpdateMeasurementSerieCommand(
            Guid Id,
            string Name,
            float MaxValue,
            float MinValue,
            string? ThisColor,
            string? IconInfo,
            string? Unit = null) : IRequest<UpdateSerieResult>;

        public class UpdateMeasurementSerieCommandHandler : IRequestHandler<UpdateMeasurementSerieCommand, UpdateSerieResult>
        {
            private readonly AppDbContext _context;

            public UpdateMeasurementSerieCommandHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<UpdateSerieResult> Handle(UpdateMeasurementSerieCommand request, CancellationToken cancellationToken)
            {
                var serie = await _context.MeasurementSeries
                    .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

                if (serie == null)
                {
                    return UpdateSerieResult.NotFound;
                }

                if (request.MinValue >= request.MaxValue)
                {
                    var log = Log.CreateForError(serie, request, "InvalidRange", "MinValue >= MaxValue podczas edycji serii.", 422);
                    _context.Logs.Add(log);
                    await _context.SaveChangesAsync(cancellationToken);
                    return UpdateSerieResult.InvalidRange;
                }

                // Wymaganie F4 oraz zai-api-26z.yaml (PUT /api/series/{seriesId}):
                // Sprawdzenie, czy istniejące pomiary mieszczą się w nowym przedziale [MinValue, MaxValue]
                bool hasOutOfRangeMeasurements = await _context.Measurements
                    .AnyAsync(m => m.MeasurementSerieId == request.Id &&
                                  (m.Value < request.MinValue || m.Value > request.MaxValue),
                              cancellationToken);

                if (hasOutOfRangeMeasurements)
                {
                    var conflictLog = Log.CreateForError(
                        entity: serie,
                        command: request,
                        errorObject: "RangeConflict",
                        errorString: "Nowy przedział min/max wyklucza zapisane już wyniki pomiarów w tej serii.",
                        httpStatusCode: 409);

                    _context.Logs.Add(conflictLog);
                    await _context.SaveChangesAsync(cancellationToken);

                    return UpdateSerieResult.ConflictWithData;
                }

                serie.Edit(
                    request.Name,
                    request.MaxValue,
                    request.MinValue,
                    request.ThisColor ?? serie.Color,
                    request.IconInfo,
                    request.Unit);

                _context.MeasurementSeries.Update(serie);
                await _context.SaveChangesAsync(cancellationToken);

                return UpdateSerieResult.Success;
            }
        }

        public record DeleteMeasurementSerieCommand(Guid Id) : IRequest<bool>;

        public class DeleteMeasurementSerieCommandHandler : IRequestHandler<DeleteMeasurementSerieCommand, bool>
        {
            private readonly AppDbContext _context;

            public DeleteMeasurementSerieCommandHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<bool> Handle(DeleteMeasurementSerieCommand request, CancellationToken cancellationToken)
            {
                var serie = await _context.MeasurementSeries
                    .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

                if (serie == null)
                {
                    return false; // Kontroler zwróci 404 NotFound
                }

                // Kontrakt YAML: Usunięcie serii usuwa ją wraz ze wszystkimi jej wynikami i przypisanymi czujnikami
                var relatedSensors = await _context.Sensors
                    .Where(s => s.MeasurementSerieId == request.Id)
                    .ToListAsync(cancellationToken);

                var relatedMeasurements = await _context.Measurements
                    .Where(m => m.MeasurementSerieId == request.Id)
                    .ToListAsync(cancellationToken);

                if (relatedSensors.Count > 0)
                {
                    _context.Sensors.RemoveRange(relatedSensors);
                }

                if (relatedMeasurements.Count > 0)
                {
                    _context.Measurements.RemoveRange(relatedMeasurements);
                }

                _context.MeasurementSeries.Remove(serie);
                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }
        }

        #endregion

        #region 4. SensorsController Commands & Queries (/api/sensors)

        public record GetAllSensorsQuery() : IRequest<List<SensorDto>>;

        public class GetAllSensorsQueryHandler : IRequestHandler<GetAllSensorsQuery, List<SensorDto>>
        {
            private readonly AppDbContext _context;

            public GetAllSensorsQueryHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<List<SensorDto>> Handle(GetAllSensorsQuery request, CancellationToken cancellationToken)
            {
                var sensors = await _context.Sensors.ToListAsync(cancellationToken);

                // 2. Sortujemy w pamięci i od razu mapujemy na bezpieczne DTO
                return sensors
                    .OrderBy(s => s.CreationDataTime)
                    .Select(s => new SensorDto
                    {
                        Id = s.Id,
                        Name = s.Name, 
                        SeriesId = s.MeasurementSerieId,
                        CreatedAt = s.CreationDataTime,
                        LastMeasurementAt = s.LastMeasurementAt
                    })
                    .ToList();
            }
        }

        public record GetSensorQuery(Guid Id) : IRequest<Sensor?>;

        public class GetSensorQueryHandler : IRequestHandler<GetSensorQuery, Sensor?>
        {
            private readonly AppDbContext _context;

            public GetSensorQueryHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<Sensor?> Handle(GetSensorQuery request, CancellationToken cancellationToken)
            {
                return await _context.Sensors
                    .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            }
        }

        public record CreateSensorCommand(string Name, Guid? MeasurementSerieId) : IRequest<Guid>;

        public class CreateSensorCommandHandler : IRequestHandler<CreateSensorCommand, Guid>
        {
            private readonly AppDbContext _context;

            public CreateSensorCommandHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<Guid> Handle(CreateSensorCommand request, CancellationToken cancellationToken)
            {
                var measurementSerie = await _context.Set<MeasurementSerie>()
                    .FirstOrDefaultAsync(s => s.Id == request.MeasurementSerieId, cancellationToken);

                if (measurementSerie == null)
                {
                    // Kontrakt YAML: wskazanie nieistniejącej serii kończy się kodem 422
                    var errorLog = Log.CreateForError(request, request, "SeriesNotFound", "Wskazana seria nie istnieje.", 422);
                    _context.Logs.Add(errorLog);
                    await _context.SaveChangesAsync(cancellationToken);
                    throw new ApiStatusException(422, "Unprocessable Entity", "Specified seriesId does not exist.");
                }

                // Wymaganie F12 oraz T5.5: Generujemy losowy klucz API (48 znaków) i zapisujemy w bazie TYLKO jego hash SHA-256
                string plainApiKey = Sensor.GeneratePlainApiKey();
                string apiKeyHash = Sensor.HashApiKey(plainApiKey);

                var sensor = new Sensor(request.Name, measurementSerie)
                {
                    MeasurementSerieId = measurementSerie.Id,
                    ApiKeyHash = apiKeyHash
                };

                _context.Sensors.Add(sensor);
                await _context.SaveChangesAsync(cancellationToken);

                // Zapisujemy jawny klucz w rejestrze jednorazowym, skąd kontroler pobierze go przez SensorKeyRegistry.TakePlainKey(id)
                SensorKeyRegistry.StorePlainKey(sensor.Id, plainApiKey);

                return sensor.Id;
                //toto
            }
        }

        // Zachowany dla pełnej kompatybilności z Twoim wcześniejszym kodem
        public record UpdateSensorCommand(Guid Id, string Nuame) : IRequest<bool>;

        public class UpdateSensorCommandHandler : IRequestHandler<UpdateSensorCommand, bool>
        {
            private readonly AppDbContext _context;

            public UpdateSensorCommandHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<bool> Handle(UpdateSensorCommand request, CancellationToken cancellationToken)
            {
                var sensor = await _context.Sensors.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
                if (sensor == null) return false;

                sensor.Nuame = request.Nuame;
                _context.Sensors.Update(sensor);
                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }
        }

        public record DeleteSensorCommand(Guid Id) : IRequest<bool>;

        public class DeleteSensorCommandHandler : IRequestHandler<DeleteSensorCommand, bool>
        {
            private readonly AppDbContext _context;

            public DeleteSensorCommandHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<bool> Handle(DeleteSensorCommand request, CancellationToken cancellationToken)
            {
                var sensor = await _context.Sensors
                    .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);

                if (sensor == null)
                {
                    return false; // Kontroler zwróci 404 NotFound
                }

                // Wymaganie F12 oraz YAML: Usuwamy czujnik (jego klucz API natychmiast przestaje działać),
                // ale NIE usuwamy zebranych przez niego wyników pomiarów (Measurements)!
                _context.Sensors.Remove(sensor);
                await _context.SaveChangesAsync(cancellationToken);

                return true;
            }
        }

        #endregion

        #region 5. MeasurementsController Commands & Queries (/api/measurements)

        public record GetMeasurementsQuery(
            List<Guid>? SeriesIds = null,
            DateTimeOffset? From = null,
            DateTimeOffset? To = null,
            string Sort = "timestamp",
            int Limit = 1000) : IRequest<List<Measurement>>;

        public class GetMeasurementsQueryHandler : IRequestHandler<GetMeasurementsQuery, List<Measurement>>
        {
            private readonly AppDbContext _context;

            public GetMeasurementsQueryHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<List<Measurement>> Handle(GetMeasurementsQuery request, CancellationToken cancellationToken)
            {
                IQueryable<Measurement> query = _context.Measurements.AsQueryable();

                // 1. Filtrowanie po wybranych seriach (F5)
                if (request.SeriesIds != null && request.SeriesIds.Count > 0)
                {
                    query = query.Where(m => request.SeriesIds.Contains(m.MeasurementSerieId));
                }

                // 2. Domknięty przedział czasu: from <= timestamp <= to (YAML)
                if (request.From.HasValue)
                {
                    var fromUtc = request.From.Value.UtcDateTime;
                    query = query.Where(m => m.DateTime >= fromUtc);
                }

                if (request.To.HasValue)
                {
                    var toUtc = request.To.Value.UtcDateTime;
                    query = query.Where(m => m.DateTime <= toUtc);
                }

                // 3. Sortowanie rosnąco (timestamp) lub malejąco (-timestamp)
                query = request.Sort == "-timestamp"
                    ? query.OrderByDescending(m => m.DateTime)
                    : query.OrderBy(m => m.DateTime);

                // 4. Limit wyników (1..10000, domyślnie 1000)
                int safeLimit = Math.Clamp(request.Limit, 1, 10000);

                return await query.Take(safeLimit).ToListAsync(cancellationToken);
            }
        }

        public record GetMeasurementQuery(Guid Id) : IRequest<Measurement?>;

        public class GetMeasurementQueryHandler : IRequestHandler<GetMeasurementQuery, Measurement?>
        {
            private readonly AppDbContext _context;

            public GetMeasurementQueryHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<Measurement?> Handle(GetMeasurementQuery request, CancellationToken cancellationToken)
            {
                return await _context.Measurements
                    .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);
            }
        }

        /// <summary>
        /// Obsługuje zarówno wywołanie z 5 parametrami (Twój stary kod), jak i z 6 parametrem ApiKey z nagłówka X-API-Key.
        /// </summary>
        public record CreateMeasurementCommand(
            Guid? SensorId,
            float Value,
            DateTime DateTime,
            Guid GroupId,
            Guid MeasurementSerieId,
            string? ApiKey = null) : IRequest<Guid>;

        public class CreateMeasurementCommandHandler : IRequestHandler<CreateMeasurementCommand, Guid>
        {
            private readonly AppDbContext _context;

            public CreateMeasurementCommandHandler(AppDbContext context)
            {
                _context = context;
            }

            public async Task<Guid> Handle(CreateMeasurementCommand request, CancellationToken cancellationToken)
            {
                Sensor? sensor = null;

                if (!string.IsNullOrWhiteSpace(request.ApiKey))
                {
                    string keyHash = Sensor.HashApiKey(request.ApiKey.Trim());
                    sensor = await _context.Sensors
                        .FirstOrDefaultAsync(s => s.ApiKeyHash == keyHash, cancellationToken);

                    if (sensor == null)
                    {
                        var authLog = Log.CreateForError(
                            entity: new { request.Value, request.DateTime },
                            command: new { request.Value, request.DateTime },
                            errorObject: "InvalidSensorApiKey",
                            errorString: "Odrzucono pomiar: nieprawidłowy lub wyrejestrowany klucz X-API-Key czujnika.",
                            httpStatusCode: 401);

                        _context.Logs.Add(authLog);
                        await _context.SaveChangesAsync(cancellationToken);

                        throw new ApiStatusException(401, "Unauthorized", "Invalid or unregistered sensor X-API-Key.");
                    }
                }
                else if (request.SensorId.HasValue && request.SensorId.Value != Guid.Empty)
                {
                    sensor = await _context.Sensors
                        .FirstOrDefaultAsync(s => s.Id == request.SensorId.Value, cancellationToken);
                }

                // 2. Pobranie serii pomiarowej wynikającej z czujnika (Wymaganie T3)
                Guid targetSerieId = sensor?.MeasurementSerieId ?? request.MeasurementSerieId;
                var serie = await _context.MeasurementSeries
                    .FirstOrDefaultAsync(s => s.Id == targetSerieId, cancellationToken);

                if (serie == null)
                {
                    var serieLog = Log.CreateForError(sensor, request, "SeriesNotFound", "Seria przypisana do czujnika nie istnieje.", 422);
                    _context.Logs.Add(serieLog);
                    await _context.SaveChangesAsync(cancellationToken);

                    throw new ApiStatusException(422, "Unprocessable Entity", "Measurement series associated with the sensor does not exist.");
                }

                // 3. Walidacja znacznika czasu: nie później niż 5 minut od bieżącego czasu serwera (zai-api-26z.yaml)
                var utcTimestamp = request.DateTime.Kind == DateTimeKind.Unspecified
                    ? DateTime.SpecifyKind(request.DateTime, DateTimeKind.Utc)
                    : request.DateTime.ToUniversalTime();

                if (utcTimestamp > DateTime.UtcNow.AddMinutes(5))
                {
                    var timeLog = Log.CreateForError(
                        entity: serie,
                        command: new { SensorId = sensor?.Id, request.Value, Timestamp = utcTimestamp },
                        errorObject: "TimestampInFuture",
                        errorString: $"Odrzucono pomiar: czas {utcTimestamp:O} wykracza ponad 5 minut w przyszłość.",
                        httpStatusCode: 422);

                    _context.Logs.Add(timeLog);
                    await _context.SaveChangesAsync(cancellationToken);

                    throw new ApiStatusException(422, "Unprocessable Entity", "Timestamp cannot be more than 5 minutes in the future.");
                }

                // 4. Walidacja zakresu wartości [MinValue, MaxValue] serii i zapis do logu błędów (Wymaganie F4, A3)
                if (request.Value < serie.MinValue || request.Value > serie.MaxValue)
                {
                    var rangeLog = Log.CreateForError(
                        entity: serie,
                        command: new { SensorId = sensor?.Id, SerieId = serie.Id, request.Value, Timestamp = utcTimestamp },
                        errorObject: "ValueOutOfRange",
                        errorString: $"Wartość {request.Value} przekracza dozwolony zakres serii '{serie.Name}' [{serie.MinValue}..{serie.MaxValue}].",
                        httpStatusCode: 422);

                    _context.Logs.Add(rangeLog);
                    await _context.SaveChangesAsync(cancellationToken);

                    throw new ApiStatusException(
                        422,
                        "Unprocessable Entity",
                        $"Value {request.Value} is outside the allowed range [{serie.MinValue}, {serie.MaxValue}] for series '{serie.Name}'.");
                }

                // 5. Zapis poprawnego pomiaru oraz aktualizacja statystyk czujnika
                var measurement = new Measurement(
                    sensorId: sensor?.Id,
                    measurementSerieId: serie.Id,
                    value: request.Value,
                    dateTime: utcTimestamp)
                {
                    GroupId = request.GroupId == Guid.Empty ? Guid.NewGuid() : request.GroupId
                };

                if (sensor != null)
                {
                    sensor.RegisterNewMeasurement(utcTimestamp);
                    _context.Sensors.Update(sensor);
                }

                _context.Measurements.Add(measurement);
                await _context.SaveChangesAsync(cancellationToken);

                return measurement.Id;
            }
        }

        #endregion
    }
}

