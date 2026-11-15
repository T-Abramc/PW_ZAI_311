using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;
using System.Drawing;

namespace PW_ZAI_311.Controllers.Domain
{
    public record UpdateMeasurementSerieCommand2(Guid Id, string Name, float MaxValue, float MinValue, string color, string IconInfo) : IRequest;
    public class UpdateMeasurementSerieCommandHandler : IRequestHandler<UpdateMeasurementSerieCommand2>
    {
        private readonly AppDbContext _context;
        public UpdateMeasurementSerieCommandHandler(AppDbContext context) => _context = context;

        public async Task Handle(UpdateMeasurementSerieCommand2 request, CancellationToken cancellationToken)
        {
            var measurementSerie = await _context.Set<MeasurementSerie>().FindAsync(new object[] { request.Id }, cancellationToken);

            measurementSerie.Edit(request.Name, request.MaxValue, request.MinValue, request.color, request.IconInfo);
            await _context.SaveChangesAsync(cancellationToken);
            
        }
    }
}
