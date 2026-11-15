using MediatR;
using Microsoft.IdentityModel.Tokens;
using PW_ZAI_311.configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.EntityFrameworkCore;


namespace PW_ZAI_311.Controllers
{
    public record LoginCommand2(string Username, string Password) : IRequest<string?>;

    public class LoginCommandHandler : IRequestHandler<LoginCommand2, string?>
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;

        public LoginCommandHandler(AppDbContext db, IConfiguration config)
        {
            _db = db;
            _config = config;
        }

        public async Task<string?> Handle(LoginCommand2 request, CancellationToken cancellationToken)
        {
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Username == request.Username, cancellationToken);
            var passHush = BCrypt.Net.BCrypt.HashPassword(request.Password);
            if (user == null || !BCrypt.Net.BCrypt.Verify(passHush, user.PasswordHash))
            {
                return null;
            }

            var key = Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "TwojBardzoTajnyKluczKtoryMaMinimum16Znakow!");
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role)
                }),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
