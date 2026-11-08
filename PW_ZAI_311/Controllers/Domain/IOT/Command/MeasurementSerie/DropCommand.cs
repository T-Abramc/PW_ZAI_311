using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;

namespace PW_ZAI_311.Controllers
{
    public record DeleteMeasurementSerieCommand(Guid Id) : IRequest;
    public class DeleteMeasurementSerieCommandHandler : IRequestHandler<DeleteMeasurementSerieCommand>
    {
        private readonly AppDbContext _context;
        public DeleteMeasurementSerieCommandHandler(AppDbContext context) => _context = context;

        public async Task Handle(DeleteMeasurementSerieCommand request, CancellationToken cancellationToken)
        {
            var measurementSerie = await _context.Set<MeasurementSerie>().FindAsync(new object[] { request.Id }, cancellationToken);
            if (measurementSerie != null)
            {
                _context.Set<MeasurementSerie>().Remove(measurementSerie);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
