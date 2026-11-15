using MediatR;
using MongoDB.Driver.Linq;
using PW_ZAI_311.configuration;

namespace PW_ZAI_311.Controllers
{
    public record ChangePasswordCommand2(string Username, string CurrentPassword, string NewPassword) : IRequest<bool>;

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand2, bool>
    {
        private readonly AppDbContext _db;

        public ChangePasswordCommandHandler(AppDbContext db)
        {
            _db = db;
        }

        public async Task<bool> Handle(ChangePasswordCommand2 request, CancellationToken cancellationToken)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);

            // Sprawdzenie starego hasła (Wymaganie F8, T5.1)
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            {
                return false;
            }

            // Hashowanie nowego hasła przez bcrypt (Wymaganie T5.1)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _db.SaveChangesAsync(cancellationToken);

            return true;
        }
    }
}
