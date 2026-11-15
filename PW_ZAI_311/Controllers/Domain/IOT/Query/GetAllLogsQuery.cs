using MediatR;
using Microsoft.EntityFrameworkCore;
using PW_ZAI_311.configuration;
using PW_ZAI_311.Entieties;

namespace PW_ZAI_311.Controllers.Domain.IOT.Query
{
    public record LogDto(
    Guid Id,
    uint HttpValue,
    bool IsError,
    string ActionInfo,
    string EntityInfo,
    string ErrorCode,
    DateTime CreationDataTime);

    public record GetAllLogsQuery2() : IRequest<List<LogDto>>;
    public class GetAllLogsQueryHandler : IRequestHandler<GetAllLogsQuery2, List<LogDto>>
    {
        private readonly AppDbContext _context;
        public GetAllLogsQueryHandler(AppDbContext context) => _context = context;

        public async Task<List<LogDto>> Handle(GetAllLogsQuery2 request, CancellationToken cancellationToken)
        {
            var logs = await _context.Set<Log>().ToListAsync(cancellationToken);

            return logs.Select(log => new LogDto(
                               log.Id,
                               log.HttpValue,
                               log.IsError,
                               log.ActionInfo,
                               log.EntityInfo,
                               log.ErrorCode,
                               log.CreationDataTime
            )).ToList();
        }
    }
}
