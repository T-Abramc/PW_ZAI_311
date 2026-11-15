using MediatR;
using PW_ZAI_311.Controllers.Domain.IOT.NewCommand.PW_ZAI_311.Application;

namespace PW_ZAI_311.Controllers
{
    public record LogoutCommand(string Token) : IRequest<Unit>;

    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
    {
        public Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            TokenBlacklist.Revoke(request.Token);
            return Task.FromResult(Unit.Value);
        }
    }
}
