using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Controllers.Domain;

namespace PW_ZAI_311.Controllers.Domains
{
    public record GetMeasurementQuery2(Guid Id) : IRequest<MeasurementOutput>;

    public class MeasurementOutput : BaseOutput
    {
        public Guid? SensorId { get; set; }
        public float Value { get; set; }
        public DateTime DateTime { get; set; }
        public Guid GroupId { get; set; }
        public Guid MeasurementSerieId { get; set; }

        public MeasurementOutput(Guid id) : base(id) { }

        public MeasurementOutput(Guid id, Guid? sensorId, float value, DateTime dateTime, Guid groupId, Guid measurementSerieId) : this(id)
        {
            SensorId = sensorId;
            Value = value;
            DateTime = dateTime;
            GroupId = groupId;
            MeasurementSerieId = measurementSerieId;
        }
    }

    public class GetMeasurementQueryHandler : IRequestHandler<GetMeasurementQuery2, MeasurementOutput>
    {
        private readonly AppDbContext _context;
        public GetMeasurementQueryHandler(AppDbContext context) => _context = context;

        public async Task<MeasurementOutput> Handle(GetMeasurementQuery2 request, CancellationToken cancellationToken)
        {
            var measurement = await _context.Measurements.FindAsync(new object[] { request.Id }, cancellationToken);
            if (measurement == null) return null;

            return new MeasurementOutput(
                measurement.Id,
                measurement.SensorId,
                measurement.Value,
                measurement.DateTime,
                measurement.GroupId,
                measurement.MeasurementSerieId);
        }
    }
}
