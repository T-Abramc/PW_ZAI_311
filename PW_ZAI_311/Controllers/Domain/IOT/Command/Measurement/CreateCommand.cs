using MediatR;
using Microsoft.EntityFrameworkCore;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;
using System;
using System.Text.RegularExpressions;

namespace PW_ZAI_311.Controllers.Domain
{
    public record CreateMeasurementCommand(Guid? SensorId, float Value, DateTime DateTime, Guid GroupId, Guid MeasurementSerieId) : IRequest<Guid>;
    public class CreateMeasurementCommandHandler : IRequestHandler<CreateMeasurementCommand, Guid>
    {
        private readonly AppDbContext _context;
        public CreateMeasurementCommandHandler(AppDbContext context) => _context = context;

        public async Task<Guid> Handle(CreateMeasurementCommand request, CancellationToken cancellationToken)
        {
            var sensor = await _context.Set<Sensor>().FindAsync(new object[] { request.SensorId }, cancellationToken);

            var measurement = new Measurement(sensor,
                                              request.Value,
                                              request.DateTime,
                                              request.GroupId,
                                              sensor?.MeasurementSerie);

            _context.Set<Measurement>().Add(measurement);
            await _context.SaveChangesAsync(cancellationToken);

            return measurement.Id;
        }
    }
}
