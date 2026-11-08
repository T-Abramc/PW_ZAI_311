using MediatR;

namespace PW_ZAI_311.Controllers.Domain.IOT.Command.Sensor
{
    public record DeleteSensorCommand(Guid Id) : IRequest;
}
