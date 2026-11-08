using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;
using System.Drawing;

namespace PW_ZAI_311.Controllers.Domain
{
    public record UpdateMeasurementSerieCommand(Guid Id, string Name, float MaxValue, float MinValue, KnownColor? Color, string IconInfo) : IRequest;
    public class UpdateMeasurementSerieCommandHandler : IRequestHandler<UpdateMeasurementSerieCommand>
    {
        private readonly AppDbContext _context;
        public UpdateMeasurementSerieCommandHandler(AppDbContext context) => _context = context;

        public async Task Handle(UpdateMeasurementSerieCommand request, CancellationToken cancellationToken)
        {
            var measurementSerie = await _context.Set<MeasurementSerie>().FindAsync(new object[] { request.Id }, cancellationToken);

            measurementSerie.Edit(request.Name, request.MaxValue, request.MinValue, request.Color.Value, request.IconInfo);
            await _context.SaveChangesAsync(cancellationToken);
            
        }
    }
}
