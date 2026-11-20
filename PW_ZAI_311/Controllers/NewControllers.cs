using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Drawing;
using System.Linq;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PW_ZAI_311.Entieties;
using PW_ZAI_311.Controllers.Domain.IOT.NewCommand.PW_ZAI_311.Application;

namespace PW_ZAI_311.Controllers
{
    #region SystemController (/api/health, /api/logs, /api/colors)

    [ApiController]
    [Route("api")]
    [Produces("application/json")]
    public class SystemController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SystemController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [AllowAnonymous]
        [HttpGet("health")]
        public IActionResult GetHealth()
        {
            return Ok(new { status = "ok" });
        }

        [Authorize]
        [HttpGet("Logs")]
        public async Task<IActionResult> GetAll()
        {
            var query = new GetAllLogsQuery();
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("Colors")]
        public Task<List<ColorOutput>> GetKnownColors()
        {
            var knownColors = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>();
            var result = knownColors.Select(kc => new ColorOutput(kc))
                                    .Where(x => x.A == byte.MaxValue)
                                    .OrderBy(x => x.B)
                                    .ToList();
            return Task.FromResult(result);
        }
    }

    #endregion

    #region AuthController (/api/auth)

    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AuthController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [Consumes("application/json")]
        public async Task<IActionResult> Login([FromBody] LoginRequestDto input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Username) || string.IsNullOrWhiteSpace(input.Password))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "Username and password are required.");
            }

            var response = await _mediator.Send(new LoginCommand(input.Username, input.Password));
            if (response == null)
            {
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Invalid username or password.");
            }

            // Ustawienie bezpiecznego ciasteczka sesyjnego (pod wymagania bezpieczeństwa T5.3 / A4)
            Response.Cookies.Append("jwt_session", response.AccessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddSeconds(3600)
            });

            return Ok(response);
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            string token = string.Empty;
            var authHeader = Request.Headers.Authorization.ToString();

            if (!string.IsNullOrWhiteSpace(authHeader) && authHeader.StartsWith("Bearer "))
            {
                // Jeśli przyszedł z nagłówka, obcinamy "Bearer "
                token = authHeader.Substring("Bearer ".Length).Trim();
            }
            else if (Request.Cookies.TryGetValue("jwt_session", out var cookieToken))
            {
                // Jeśli przyszedł z ciasteczka, bierzemy jak leci
                token = cookieToken;
            }

            if (!string.IsNullOrEmpty(token))
            {
                await _mediator.Send(new LogoutCommand(token));
            }
            Response.Cookies.Delete("jwt_session");
            return NoContent();
        }

        [Authorize]
        [HttpPut("password")]
        [Consumes("application/json")]
        public async Task<IActionResult> ChangePassword([FromBody] PasswordChangeDto input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.CurrentPassword) || string.IsNullOrWhiteSpace(input.NewPassword))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "currentPassword and newPassword are required.");
            }

            if (input.NewPassword.Length < 8)
            {
                return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unprocessable Entity", detail: "New password must be at least 8 characters long.");
            }

            var username = User.Identity?.Name ?? "admin";
            var status = await _mediator.Send(new ChangePasswordCommand(username, input.CurrentPassword, input.NewPassword));

            return status switch
            {
                PasswordChangeStatus.InvalidCurrentPassword => Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "Current password is incorrect."),
                PasswordChangeStatus.TooShort => Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unprocessable Entity", detail: "New password must be at least 8 characters long."),
                PasswordChangeStatus.UserNotFound => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "User not found."),
                _ => (IActionResult)NoContent()
            };
        }
    }

    #endregion

    #region SeriesController (/api/series)

    [ApiController]
    [Route("api/series")]
    [Produces("application/json")]
    public class SeriesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private static readonly Regex HexColorRegex = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

        public SeriesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAllSeries()
        {
            var result = await _mediator.Send(new GetAllMeasurementSeriesQuery());
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("{seriesId:guid}")]
        public async Task<IActionResult> GetMeasurementSerie(Guid seriesId)
        {
            var query = new GetMeasurementSerieQuery(seriesId);
            var result = await _mediator.Send(query);

            if (result == null)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: $"Series {seriesId} not found.");
            }

            return Ok(result);
        }

        [Authorize]
        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> CreateMeasurementSerie([FromBody] CreateMeasurementSerieInput input)
        {
            var validationProblem = ValidateSeriesInput(input);
            if (validationProblem != null) return validationProblem;

            try
            {
                var command = new CreateMeasurementSerieCommand(
                    input.Name,
                    input.MaxValue,
                    input.MinValue,
                    input.Color,
                    input.IconInfo,
                    input.Unit);

                var id = await _mediator.Send(command);

                var createdSeries = new SeriesDto
                {
                    Id = id,
                    Name = input.Name,
                    MinValue = input.MinValue,
                    MaxValue = input.MaxValue,
                    Color = input.Color,
                    Icon = input.IconInfo,
                    Unit = input.Unit
                };

                return Created($"/api/series/{id}", createdSeries);
            }
            catch (ApiStatusException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Title, detail: ex.Message);
            }
        }

        /// <summary>
        /// Edycja Serii 
        /// </summary>
        [Authorize]
        [HttpPut("{seriesId:guid}")]
        [Consumes("application/json")]
        public async Task<IActionResult> UpdateMeasurementSerie(Guid seriesId, [FromBody] UpdateMeasurementSerieInput input)
        {
            var validationProblem = ValidateSeriesInput(input);
            if (validationProblem != null) return validationProblem;

            var command = new UpdateMeasurementSerieCommand(
                seriesId,
                input.Name,
                input.MaxValue,
                input.MinValue,
                input.Color,
                input.IconInfo,
                input.Unit);

            var status = await _mediator.Send(command);

            return status switch
            {
                UpdateSerieResult.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: $"Series {seriesId} not found."),
                UpdateSerieResult.InvalidRange => Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unprocessable Entity", detail: "minValue must be strictly less than maxValue."),
                UpdateSerieResult.ConflictWithData => Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: "New range [minValue, maxValue] excludes existing measurements in this series."),
                _ => Ok(new SeriesDto
                {
                    Id = seriesId,
                    Name = input.Name,
                    MinValue = input.MinValue,
                    MaxValue = input.MaxValue,
                    Color = input.Color,
                    Icon = input.IconInfo,
                    Unit = input.Unit
                })
            };
        }

        [Authorize]
        [HttpDelete("{seriesId:guid}")]
        public async Task<IActionResult> DeleteMeasurementSerie(Guid seriesId)
        {
            var deleted = await _mediator.Send(new DeleteMeasurementSerieCommand(seriesId));
            if (!deleted)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: $"Series {seriesId} not found.");
            }

            return NoContent();
        }

        private IActionResult? ValidateSeriesInput(SeriesBaseInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100 || string.IsNullOrWhiteSpace(input.Color))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "Invalid series fields.");
            }

            if (!HexColorRegex.IsMatch(input.Color))
            {
                return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unprocessable Entity", detail: "Color must match ^#[0-9A-Fa-f]{6}$.");
            }

            if (input.Unit != null && input.Unit.Length > 20)
            {
                return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unprocessable Entity", detail: "Unit max length is 20.");
            }

            if (input.MinValue >= input.MaxValue)
            {
                return Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: "Unprocessable Entity", detail: "minValue must be strictly less than maxValue.");
            }

            return null;
        }
    }

    #endregion

    #region SensorsController (/api/sensors)

    [Authorize]
    [ApiController]
    [Route("api/sensors")]
    [Produces("application/json")]
    public class SensorsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public SensorsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllSensors()
        {
            var result = await _mediator.Send(new GetAllSensorsQuery());
            return Ok(result);
        }

        [HttpGet("{sensorId:guid}")]
        public async Task<IActionResult> GetSensor(Guid sensorId)
        {
            var query = new GetSensorQuery(sensorId);
            var result = await _mediator.Send(query);

            if (result == null)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: $"Sensor {sensorId} not found.");
            }

            return Ok(result);
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> CreateSensor([FromBody] CreateSensorInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 100 || input.MeasurementSerieId == Guid.Empty)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "Valid name and seriesId are required.");
            }

            try
            {
                var command = new CreateSensorCommand(input.Name, input.MeasurementSerieId);
                var id = await _mediator.Send(command);

                string generatedPlainApiKey = SensorKeyRegistry.TakePlainKey(id)!;

                var createdSensor = new SensorCreatedDto
                {
                    Id = id,
                    Name = input.Name,
                    SeriesId = input.MeasurementSerieId,
                    CreatedAt = DateTime.UtcNow,
                    LastMeasurementAt = null,
                    ApiKey = generatedPlainApiKey
                };

                return Created($"/api/sensors/{id}", createdSensor);
            }
            catch (ApiStatusException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Title, detail: ex.Message);
            }
        }

        [HttpDelete("{sensorId:guid}")]
        public async Task<IActionResult> DeleteSensor(Guid sensorId)
        {
            var deleted = await _mediator.Send(new DeleteSensorCommand(sensorId));
            if (!deleted)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: $"Sensor {sensorId} not found.");
            }

            return NoContent();
        }
    }

    #endregion

    #region MeasurementsController (/api/measurements)

    [ApiController]
    [Route("api/measurements")]
    [Produces("application/json")]
    public class MeasurementsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public MeasurementsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetMeasurements(
            [FromQuery] string? series = null,
            [FromQuery] DateTimeOffset? from = null,
            [FromQuery] DateTimeOffset? to = null,
            [FromQuery] string sort = "timestamp",
            [FromQuery] int limit = 1000)
        {
            if (sort != "timestamp" && sort != "-timestamp")
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "sort must be 'timestamp' or '-timestamp'.");
            }

            if (limit < 1 || limit > 10000)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "limit must be between 1 and 10000.");
            }

            if (from.HasValue && to.HasValue && from.Value > to.Value)
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "'from' cannot be later than 'to'.");
            }

            var seriesIds = new List<Guid>();
            if (!string.IsNullOrWhiteSpace(series))
            {
                foreach (var part in series.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (!Guid.TryParse(part, out var parsedGuid))
                    {
                        return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: $"Invalid series id: {part}");
                    }
                    seriesIds.Add(parsedGuid);
                }
            }

            var query = new GetMeasurementsQuery(seriesIds, from, to, sort, limit);
            var result = await _mediator.Send(query);

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpGet("{measurementId:guid}")]
        public async Task<IActionResult> GetMeasurement(Guid measurementId)
        {
            var query = new GetMeasurementQuery(measurementId);
            var result = await _mediator.Send(query);

            if (result == null)
            {
                return Problem(statusCode: StatusCodes.Status404NotFound, title: "Not Found", detail: $"Measurement {measurementId} not found.");
            }

            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPost]
        [Consumes("application/json")]
        public async Task<IActionResult> CreateMeasurement(
            [FromHeader(Name = "X-API-Key")] string? apiKey,
            [FromBody] CreateMeasurementInput input)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized", detail: "Missing X-API-Key header.");
            }

            if (input == null || !input.Value.HasValue || float.IsNaN(input.Value.Value) || float.IsInfinity(input.Value.Value))
            {
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: "Field 'value' is required and must be a valid number.");
            }

            var measurementTime = input.DateTime?.UtcDateTime ?? DateTime.UtcNow;

            try
            {
                var command = new CreateMeasurementCommand(
                    input.SensorId,
                    input.Value.Value,
                    measurementTime,
                    input.GroupId,
                    input.MeasurementSerieId,
                    apiKey);

                var id = await _mediator.Send(command);
                var savedEntity = await _mediator.Send(new GetMeasurementQuery(id));

                return Created($"/api/measurements/{id}", savedEntity);
            }
            catch (ApiStatusException ex)
            {
                return Problem(statusCode: ex.StatusCode, title: ex.Title, detail: ex.Message);
            }
        }
    }

    #endregion

    #region DTOs - Modele Wejściowe i Wyjściowe zgodne z zai-api-26z.yaml

    public class LoginRequestDto
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    public class LoginResponseDto
    {
        [JsonPropertyName("accessToken")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("tokenType")]
        public string TokenType { get; set; } = "Bearer";

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; } = 3600;
    }

    public class PasswordChangeDto
    {
        [JsonPropertyName("currentPassword")]
        public string CurrentPassword { get; set; } = string.Empty;

        [JsonPropertyName("newPassword")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public abstract class SeriesBaseInput
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("minValue")]
        public float MinValue { get; set; }

        [JsonPropertyName("maxValue")]
        public float MaxValue { get; set; }

        [JsonPropertyName("color")]
        public virtual string Color { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string? IconInfo { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
    }

    public class CreateMeasurementSerieInput : SeriesBaseInput
    {
    }

    public class UpdateMeasurementSerieInput : SeriesBaseInput
    {
        [JsonIgnore]
        public Guid Id { get; set; }
    }

    public class SeriesDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("minValue")]
        public float MinValue { get; set; }

        [JsonPropertyName("maxValue")]
        public float MaxValue { get; set; }

        [JsonPropertyName("color")]
        public string Color { get; set; } = string.Empty;

        [JsonPropertyName("icon")]
        public string? Icon { get; set; }

        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
    }

    public class CreateSensorInput
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("seriesId")]
        public Guid MeasurementSerieId { get; set; }
    }

    public class SensorDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("seriesId")]
        public Guid SeriesId { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("lastMeasurementAt")]
        public DateTimeOffset? LastMeasurementAt { get; set; }
    }

    public class SensorCreatedDto : SensorDto
    {
        [JsonPropertyName("apiKey")]
        public string ApiKey { get; set; } = string.Empty;
    }

    public class CreateMeasurementInput
    {
        [Required]
        [JsonPropertyName("value")]
        public float? Value { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset? DateTime { get; set; }

        [JsonIgnore]
        public Guid? SensorId { get; set; }

        [JsonIgnore]
        public Guid GroupId { get; set; }

        [JsonIgnore]
        public Guid MeasurementSerieId { get; set; }
    }

    public class MeasurementDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("seriesId")]
        public Guid SeriesId { get; set; }

        [JsonPropertyName("sensorId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Guid? SensorId { get; set; }

        [JsonPropertyName("value")]
        public float Value { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset Timestamp { get; set; }
    }

    #endregion
}