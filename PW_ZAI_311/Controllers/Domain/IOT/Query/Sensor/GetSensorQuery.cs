using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;

namespace PW_ZAI_311.Controllers.Domain
{
    public record GetSensorQuery2(Guid Id) : IRequest<SensorOutput>;

    public class SensorOutput: BaseOutput
    {
        public string Nuame { get; set; }
        public SensorOutput(Guid id) :base(id) { }
        public SensorOutput(Guid id, string nuame) :this(id) 
        {
            Nuame = nuame;
        }
    }

    public class GetSensorQueryHandler : IRequestHandler<GetSensorQuery2, SensorOutput>
    {
        private readonly AppDbContext _context;

        public GetSensorQueryHandler(AppDbContext context) 
        { 
            _context = context; 
        }

        public async Task<SensorOutput> Handle(GetSensorQuery2 request, CancellationToken cancellationToken)
        {
            var sensor = await _context.Sensors.FindAsync(new object[]{ request.Id }, cancellationToken);
            if (sensor == null) return null;

            return new SensorOutput(sensor.Id, sensor.Nuame);
        }
    }
}
