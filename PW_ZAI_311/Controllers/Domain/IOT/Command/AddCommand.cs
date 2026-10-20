using Cqrs.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Windows.Input;

namespace PW_ZAI_311.Controllers.Domain
{
    public record CreateUserCommand(string esername, string email) : IRequest<Guid>;

    public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Guid>
    {
        private readonly DbContext _dbContext;
        public CreateUserCommandHandler(DbContext dbContext) 
        {
            _dbContext = dbContext;
        }

        public Task<Guid> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            // _dbContext
            Guid generatedUserId = Guid.NewGuid();

            return Task.FromResult(generatedUserId);
        }
    }
}
