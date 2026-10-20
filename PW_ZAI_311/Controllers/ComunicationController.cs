using Microsoft.AspNetCore.Mvc;
using MediatR;
using PW_ZAI_311.Controllers.Domain;

namespace PW_ZAI_311.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ComunicationController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<ComunicationController> _logger;
        private readonly IMediator _mediator;
        public ComunicationController(ILogger<ComunicationController> logger,
                                      IMediator mediator)
        {
            _logger = logger;
            _mediator =  mediator;  
        }

        [HttpGet(Name = "GetWeatherForecast")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
            .ToArray();
        }


        [HttpGet("TestEndpoint")]
        public Task<Guid> Get2()
        {
            return _mediator.Send(new CreateUserCommand("JanKowalski", "jan@example.com"));
        }

    }
}
