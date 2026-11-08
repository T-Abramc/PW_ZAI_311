using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace PW_ZAI_311.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ComunicationAuthController : ControllerBase
    {
        // 1. Dostępne dla każdego (nie wymaga tokena)
        [HttpGet("public")]
        public IActionResult GetPublicData()
        {
            return Ok("To jest publiczny endpoint, każdy ma dostęp.");
        }

        // 2. Dostępne dla KAŻDEGO zalogowanego użytkownika z ważnym tokenem
        [Authorize]
        [HttpGet("secured")]
        public IActionResult GetSecuredData()
        {
            var username = User.Identity?.Name;
            return Ok($"Jesteś zalogowany jako: {username}. Masz dostęp do tego endpointu.");
        }
    }
}
