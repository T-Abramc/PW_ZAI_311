using MediatR;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Controllers.Domains;
using PW_ZAI_311.Entieties;
using System.Drawing;

namespace PW_ZAI_311.Controllers.Domain
{
 
    public class BaseOutput
    {
        public Guid Id { get; set; }
        public BaseOutput() { }
        public BaseOutput(Guid id):this() 
        { Id = id; }
    }
    public class ColorOutput 
    {
        public byte A { get; set; }
        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }
        public string Name { get; set; }
        public bool IsSystemColor { get; set; }

        public ColorOutput() { }

        public ColorOutput(Color color) : this()
        {
            A = color.A;
            R = color.R;
            G = color.G;
            B = color.B;
            Name = color.Name;
            IsSystemColor = color.IsSystemColor;
            //Color.FromArgb(A, R, G, B);
        }

        public ColorOutput(KnownColor knownColor) : this(Color.FromKnownColor(knownColor))
        {
            //Color.FromArgb(A, R, G, B);
        }
    }
    //

    public class MeasurementSerieOutput : BaseOutput
    {
        public string Name { get; set; }
        public float MaxValue { get; set; }
        public float MinValue { get; set; }
        public string Color { get; set; }
        public string IconInfo { get; set; }

        public MeasurementSerieOutput(Guid id) : base(id) { }

        public MeasurementSerieOutput(Guid id, string name, float maxValue, float minValue, string color, string iconInfo) : this(id)
        {
            Name = name;
            MaxValue = maxValue;
            MinValue = minValue;
            Color = color;
            IconInfo = iconInfo;
        }
    }

    public record GetMeasurementSerieQuery2(Guid Id) : IRequest<MeasurementSerieOutput>;

    public class GetMeasurementSerieQueryHandler : IRequestHandler<GetMeasurementSerieQuery2, MeasurementSerieOutput>
    {
        private readonly AppDbContext _context;
        public GetMeasurementSerieQueryHandler(AppDbContext context) => _context = context;

        public async Task<MeasurementSerieOutput> Handle(GetMeasurementSerieQuery2 request, CancellationToken cancellationToken)
        {
            var measurementSerie = await _context.Set<MeasurementSerie>().FindAsync(new object[] { request.Id }, cancellationToken);
            if (measurementSerie == null) return null;

            return new MeasurementSerieOutput(
                measurementSerie.Id,
                measurementSerie.Name,
                measurementSerie.MaxValue,
                measurementSerie.MinValue,
                measurementSerie.Color,
                measurementSerie.IconInfo);
        }
    }


}
