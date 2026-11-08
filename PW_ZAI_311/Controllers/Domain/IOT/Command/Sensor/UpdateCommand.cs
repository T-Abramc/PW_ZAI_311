using MediatR;

namespace PW_ZAI_311.Controllers.Domain
{
    public record UpdateSensorCommand(Guid Id, string Nuame) : IRequest;


}
