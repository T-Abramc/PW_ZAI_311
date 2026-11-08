using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;

namespace PW_ZAI_311.Controllers.Domain
{
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
            var  measurementSerie = await _context.Set<MeasurementSerie>().FindAsync(new object[] { request.MeasurementSerieId }, cancellationToken);

            var sensor = new Sensor(request.Name, measurementSerie);

            _context.Sensors.Add(sensor);
            await _context.SaveChangesAsync(cancellationToken);

            return sensor.Id;
        }
    }
}
