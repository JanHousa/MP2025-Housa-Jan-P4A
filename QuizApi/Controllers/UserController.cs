using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using QuizApi.Data;
using QuizApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Jose;


namespace QuizApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly QuizAppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserController> _logger;

        public UserController(QuizAppDbContext context, IConfiguration configuration, ILogger<UserController> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        // Registrace nového uživatele
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterUserRequest request)
        {
            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
            {
                return BadRequest("Username is already taken.");
            }

            var user = new User
            {
                Username = request.Username,
                Role = "User" // Výchozí role
            };

            using (var hmac = new HMACSHA512())
            {
                // Uložení Saltu a Hashe hesla
                user.PasswordSalt = Convert.ToBase64String(hmac.Key);
                user.PasswordHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(request.Password)));
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            _logger.LogInformation("User {Username} registered successfully.", user.Username);

            return Ok("User registered successfully.");
        }

        // Přihlášení uživatele a generování JWT tokenu
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginUserRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null)
            {
                _logger.LogWarning("Login failed for username {Username}: User not found.", request.Username);
                return Unauthorized("Invalid username or password.");
            }

            // Ověření hesla
            using (var hmac = new HMACSHA512(Convert.FromBase64String(user.PasswordSalt)))
            {
                var computedHash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(request.Password)));
                if (computedHash != user.PasswordHash)
                {
                    _logger.LogWarning("Login failed for username {Username}: Invalid password.", request.Username);
                    return Unauthorized("Invalid username or password.");
                }
            }

            // Generování JWT tokenu
            var token = GenerateJwtToken(user);

            _logger.LogInformation("Token generated successfully for user {Username}.", user.Username);

            return Ok(new { token, message = "Login successful" });
        }

        // Metoda pro generování JWT tokenu
        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("id", user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role)
        }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }


        public class RegisterUserRequest
        {
            public string Username { get; set; }
            public string Password { get; set; } 
        }

        public class LoginUserRequest
        {
            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
