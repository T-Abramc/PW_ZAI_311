using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;
using System.Drawing;

namespace PW_ZAI_311.Controllers.Domain
{
    public record CreateMeasurementSerieCommand2(string Name, float MaxValue, float MinValue, string Color, string IconInfo) : IRequest<Guid>;
    public class CreateMeasurementSerieCommandHandler : IRequestHandler<CreateMeasurementSerieCommand2, Guid>
    {
        private readonly AppDbContext _context;
        public CreateMeasurementSerieCommandHandler(AppDbContext context) => _context = context;

        public async Task<Guid> Handle(CreateMeasurementSerieCommand2 request, CancellationToken cancellationToken)
        {

                
            var measurementSerie = new MeasurementSerie
            {
                Id = Guid.NewGuid(),
                CreationDataTime = DateTime.UtcNow,
                Name = request.Name,
                MaxValue = request.MaxValue,
                MinValue = request.MinValue,
                Color = request.Color,
                IconInfo = request.IconInfo
            };

            _context.Set<MeasurementSerie>().Add(measurementSerie);
            await _context.SaveChangesAsync(cancellationToken);

            return measurementSerie.Id;
        }
    }
}
