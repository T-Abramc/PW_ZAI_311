using Cqrs.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PW_ZAI_311.configuration;
using System.Windows.Input;

namespace PW_ZAI_311.Controllers.Domain
{
    public record CreateUserCommand(string esername, string email) : IRequest<Guid>;

    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
    {
        private readonly AppDbContext _dbContext;
        public CreateUserCommandHandler(
            AppDbContext dbContext
            )
        {
            _dbContext = dbContext;
        }

        public Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            var p = _dbContext.Sensors.Find();
           Guid generatedUserId = Guid.NewGuid();

            return Task.FromResult(generatedUserId);
        }
    }
}
