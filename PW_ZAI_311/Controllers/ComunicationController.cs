using Microsoft.AspNetCore.Mvc;
using MediatR;
using PW_ZAI_311.Controllers.Domain;
using System.Drawing;
using Microsoft.AspNetCore.Authorization;
using PW_ZAI_311.Controllers.Domain.IOT.Command.Sensor;
using PW_ZAI_311.Controllers.Domains;
using PW_ZAI_311.Controllers.Domain.IOT.Query;

namespace PW_ZAI_311.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ComunicationController : ControllerBase
    {

        private readonly ILogger<ComunicationController> _logger;
        private readonly IMediator _mediator;

        public ComunicationController(ILogger<ComunicationController> logger,
                                      IMediator mediator
            )
        {
            _logger = logger;
            _mediator =  mediator;  
        }

        //[HttpGet(Name = "GetWeatherForecast")]
        //public IEnumerable<WeatherForecast> Get()
        //{
        //    return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        //    {
        //        Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
        //        TemperatureC = Random.Shared.Next(-20, 55),
        //    })
        //    .ToArray();
        //}


        //[HttpGet("TestEndpoint")]
        //public Task<Guid> Get2()
        //{
        //    return _mediator.Send(new CreateUserCommand("JanKowalski", "jan@example.com"));
        //}


        //#region Sensor
        //[HttpPost("Sensor")]
        //public async Task<IActionResult> CreateSensor([FromBody] CreateSensorInput input)
        //{
        //    var command = new CreateSensorCommand(input.Name, input.MeasurementSerieId);
        //    var id = await _mediator.Send(command);
        //    return Ok(id);
        //}
        ////[Authorize]
        //[HttpGet("Sensor/{id}")]
        //public async Task<IActionResult> GetSensor(Guid id)
        //{
        //    var query = new GetSensorQuery(id);
        //    var result = await _mediator.Send(query);

        //    if (result == null) return NotFound();
        //    return Ok(result);
        //}

        //[HttpPut("Sensor/Edit")]
        //public async Task<IActionResult> UpdateSensor([FromBody] UpdateSensorInput input)
        //{
        //    var command = new UpdateSensorCommand(input.Id, input.Nuame);
        //    await _mediator.Send(command);
        //    return NoContent();
        //}

        //[HttpDelete("Sensor/{id}")]
        //public async Task<IActionResult> DeleteSensor(Guid id)
        //{
        //    var command = new DeleteSensorCommand(id);
        //    await _mediator.Send(command);
        //    return NoContent();
        //}
        //#endregion
        //#region Measurement 
        //[HttpPost("Measurement")]
        //public async Task<IActionResult> CreateMeasurement([FromBody] CreateMeasurementInput input)
        //{
        //    var command = new CreateMeasurementCommand(
        //        input.SensorId,
        //        input.Value,
        //        input.DateTime,
        //        input.GroupId,
        //        input.MeasurementSerieId);

        //    var id = await _mediator.Send(command);
        //    return Ok(id);
        //}

        //[HttpGet("Measurement/{id}")]
        //public async Task<IActionResult> GetMeasurement(Guid id)
        //{
        //    var query = new GetMeasurementQuery(id);
        //    var result = await _mediator.Send(query);

        //    if (result == null) return NotFound();
        //    return Ok(result);
        //}

        //#endregion

        //#region MeasurementSerie  

        //[HttpPost("MeasurementSerie")]
        //public async Task<IActionResult> CreateMeasurementSerie([FromBody] CreateMeasurementSerieInput input)
        //{
        //    var command = new CreateMeasurementSerieCommand(
        //        input.Name,
        //        input.MaxValue,
        //        input.MinValue,
        //        input.Color,
        //        input.IconInfo);

        //    var id = await _mediator.Send(command);
        //    return Ok(id);
        //}

        //[HttpGet("MeasurementSerie/{id}")]
        //public async Task<IActionResult> GetMeasurementSerie(Guid id)
        //{
        //    var query = new GetMeasurementSerieQuery(id);
        //    var result = await _mediator.Send(query);

        //    if (result == null) return NotFound();
        //    return Ok(result);
        //}

        //[HttpPut("MeasurementSerie/Edit")]
        //public async Task<IActionResult> UpdateMeasurementSerie([FromBody] UpdateMeasurementSerieInput input)
        //{
        //    var command = new UpdateMeasurementSerieCommand(
        //        input.Id,
        //        input.Name,
        //        input.MaxValue,
        //        input.MinValue,
        //        input.ThisColor,
        //        input.IconInfo);

        //    await _mediator.Send(command);
        //    return NoContent();
        //}

        //[HttpDelete("MeasurementSerie/{id}")]
        //public async Task<IActionResult> DeleteMeasurementSerie(Guid id)
        //{
        //    var command = new DeleteMeasurementSerieCommand(id);
        //    await _mediator.Send(command);
        //    return NoContent();
        //}
        //#endregion

        //#region System  
        //[HttpGet("Logs")]
        //public async Task<IActionResult> GetAll()
        //{
        //    var query = new GetAllLogsQuery();
        //    var result = await _mediator.Send(query);

        //    return Ok(result);
        //}
        //[HttpGet("Colors")]
        //public async Task<List<ColorOutput>> GetKnownColors()
        //{
        //    var knownColors = Enum.GetValues(typeof(KnownColor)).Cast<KnownColor>();
        //    return knownColors.Select(kc => new ColorOutput(kc))
        //                      .Where(x=> x.A == byte.MaxValue)
        //                      .OrderBy(x=> x.B)
        //                      .ToList();
        //}
        //#endregion
    }

}
