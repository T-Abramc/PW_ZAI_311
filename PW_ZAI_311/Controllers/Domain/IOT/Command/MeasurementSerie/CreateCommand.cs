using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;
using System.Drawing;

namespace PW_ZAI_311.Controllers.Domain
{
    public record CreateMeasurementSerieCommand(string Name, float MaxValue, float MinValue, KnownColor? Color, string IconInfo) : IRequest<Guid>;
    public class CreateMeasurementSerieCommandHandler : IRequestHandler<CreateMeasurementSerieCommand, Guid>
    {
        private readonly AppDbContext _context;
        public CreateMeasurementSerieCommandHandler(AppDbContext context) => _context = context;

        public async Task<Guid> Handle(CreateMeasurementSerieCommand request, CancellationToken cancellationToken)
        {
            Color? c = null;
            if (request.Color.HasValue)
                c = Color.FromKnownColor(request.Color.Value);
                
            var measurementSerie = new MeasurementSerie
            {
                Id = Guid.NewGuid(),
                CreationDataTime = DateTime.UtcNow,
                Name = request.Name,
                MaxValue = request.MaxValue,
                MinValue = request.MinValue,
                Color = c,
                IconInfo = request.IconInfo
            };

            _context.Set<MeasurementSerie>().Add(measurementSerie);
            await _context.SaveChangesAsync(cancellationToken);

            return measurementSerie.Id;
        }
    }
}
