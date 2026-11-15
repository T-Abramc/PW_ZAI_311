using MediatR;

namespace PW_ZAI_311.Controllers.Domain.IOT.Command.Sensor
{
    public record DeleteSensorCommand2(Guid Id) : IRequest;
}
