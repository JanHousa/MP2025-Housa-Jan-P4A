using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizApi.Data;
using QuizApi.Models;
using System.Diagnostics;

namespace QuizApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LobbyController : ControllerBase
    {
        private readonly QuizAppDbContext _context;
        private readonly ILogger<LobbyController> _logger;
        public LobbyController(QuizAppDbContext context, ILogger<LobbyController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Vytvoření nového lobby
        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateLobby([FromBody] CreateLobbyModel createLobbyModel)
        {
            string pin;
            bool pinExists;

            // Získání ID přihlášeného uživatele (majitel)
            var userId = User.Claims.First(c => c.Type == "id").Value;
            var owner = await _context.Users.FirstOrDefaultAsync(u => u.Id == int.Parse(userId));
            if (owner == null) return Unauthorized("User not found");

            // Opakovaně generuj PIN, dokud nenajdeme unikátní
            do
            {
                pin = new Random().Next(100000, 999999).ToString();
                pinExists = await _context.Lobbies.AnyAsync(l => l.Pin == pin);
            }
            while (pinExists);

            var lobby = new Lobby
            {
                QuizId = createLobbyModel.QuizId,
                Pin = pin,
                OwnerId = owner.Id
            };

            _context.Lobbies.Add(lobby);
            await _context.SaveChangesAsync();

            return Ok(lobby);
        }

        public class CreateLobbyModel
        {
            public int QuizId { get; set; }
        }

        // Připojení do lobby pomocí PINu (bez zadání přezdívky)
        [HttpPost("join-by-pin")]
        public async Task<IActionResult> JoinLobbyByPin([FromBody] JoinLobbyRequest request)
        {
            if (string.IsNullOrEmpty(request.Pin))
            {
                return BadRequest("The pin field is required.");
            }

            var lobby = await _context.Lobbies
                .Include(l => l.Players)
                .Include(l => l.Guests)
                .FirstOrDefaultAsync(l => l.Pin == request.Pin);

            if (lobby == null)
                return NotFound("Lobby not found");

            return Ok(new { LobbyId = lobby.Id });
        }

        public class JoinLobbyRequest
        {
            public string Pin { get; set; }
        }


        // Nastavení přezdívky v lobby (po připojení)
        [HttpPost("set-nickname")]
        public async Task<IActionResult> SetNickname([FromBody] SetNicknameRequest request)
        {
            var lobby = await _context.Lobbies
                .Include(l => l.Guests)
                .FirstOrDefaultAsync(l => l.Id == request.LobbyId);

            if (lobby == null)
                return NotFound("Lobby not found");

            if (lobby.Guests.Any(g => g.Nickname == request.Nickname))
            {
                return BadRequest("Nickname already in use in this lobby. Please choose another.");
            }

            var guest = new Guest
            {
                Nickname = request.Nickname,
                LobbyId = lobby.Id
            };

            _context.Guests.Add(guest);
            await _context.SaveChangesAsync();

            return Ok(new { GuestId = guest.Id, Nickname = guest.Nickname });
        }

        public class SetNicknameRequest
        {
            public int LobbyId { get; set; }
            public string Nickname { get; set; }
        }

        // Zobrazení všech hráčů v lobby na základě lobby PIN
        [HttpGet("{lobbyPin}/players")]
        public async Task<IActionResult> GetLobbyPlayers(string lobbyPin)
        {
            // Vyhledání lobby podle PINu
            var lobby = await _context.Lobbies
                .Include(l => l.Players)
                .Include(l => l.Guests)
                .FirstOrDefaultAsync(l => l.Pin == lobbyPin);

            if (lobby == null)
                return NotFound("Lobby not found");

            return Ok(new
            {
                Players = lobby.Players.Select(p => new { p.Id, p.Username }),
                Guests = lobby.Guests.Select(g => new { g.Id, g.Nickname })
            });
        }

        // Odpojení hráče nebo hosta z lobby (pouze vlastník)
        [Authorize]
        [HttpPost("{lobbyId}/remove")]
        public async Task<IActionResult> RemovePlayer(int lobbyId, [FromBody] RemovePlayerRequest request)
        {
            var lobby = await _context.Lobbies.Include(l => l.Players).Include(l => l.Guests).FirstOrDefaultAsync(l => l.Id == lobbyId);
            if (lobby == null) return NotFound("Lobby not found");

            // Kontrola, zda je aktuální uživatel vlastníkem lobby
            var userId = User.Claims.First(c => c.Type == "id").Value;
            if (lobby.OwnerId != int.Parse(userId))
                return Unauthorized("Only the owner can remove players.");

            // Odstranění hráče podle jeho typu
            if (request.IsGuest)
            {
                var guest = lobby.Guests.FirstOrDefault(g => g.Id == request.PlayerId);
                if (guest == null) return NotFound("Guest not found");
                _context.Guests.Remove(guest);
            }
            else
            {
                var player = lobby.Players.FirstOrDefault(p => p.Id == request.PlayerId);
                if (player == null) return NotFound("Player not found");
                lobby.Players.Remove(player);
            }

            await _context.SaveChangesAsync();
            return Ok("Player removed successfully.");
        }

        // Začátek hry z lobby
        [HttpPost("{lobbyId}/start")]
        public async Task<IActionResult> StartGame(int lobbyId)
        {
            var lobby = await _context.Lobbies.Include(l => l.Players).Include(l => l.Guests).FirstOrDefaultAsync(l => l.Id == lobbyId);
            if (lobby == null) return NotFound("Lobby not found");

            return Ok(new { message = "Game started", quizId = lobby.QuizId });
        }

        // Ověření, zda je aktuální uživatel vlastníkem lobby
        [Authorize]
        [HttpGet("{lobbyPin}/isOwner")]
        public async Task<IActionResult> IsOwner(string lobbyPin)
        {
            try
            {
                _logger.LogInformation($"Checking owner status for lobby with PIN: {lobbyPin}");

                // Získání lobby podle PINu
                var lobby = await _context.Lobbies.FirstOrDefaultAsync(l => l.Pin == lobbyPin);
                if (lobby == null)
                {
                    _logger.LogWarning("Lobby not found.");
                    return NotFound("Lobby not found");
                }

                // Logování všech claimů v tokenu
                _logger.LogInformation("User claims:");
                var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();
                foreach (var claim in claims)
                {
                    _logger.LogInformation($"Claim: {claim.Type} - {claim.Value}");
                }

                // Získání a kontrola User ID z claimů
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
                if (userIdClaim == null)
                {
                    _logger.LogWarning("User ID not found in token.");
                    return Unauthorized("User ID not found in token.");
                }

                // Parsování User ID
                if (!int.TryParse(userIdClaim.Value, out var userId))
                {
                    _logger.LogWarning("Failed to parse User ID from token.");
                    return Unauthorized("Invalid User ID in token.");
                }

                _logger.LogInformation($"User ID from token: {userId}");

                // Kontrola, zda je aktuální uživatel vlastníkem lobby
                bool isOwner = lobby.OwnerId == userId;
                _logger.LogInformation($"Is owner: {isOwner}");

                return Ok(isOwner);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in IsOwner endpoint");
                return StatusCode(500, "An internal server error occurred.");
            }
        }
        
        [HttpGet("{lobbyPin}/quizId")]
        public async Task<IActionResult> GetQuizId(string lobbyPin)
        {
            // Fetch the lobby based on the provided PIN
            var lobby = await _context.Lobbies.FirstOrDefaultAsync(l => l.Pin == lobbyPin);

            if (lobby == null)
            {
                return NotFound("Lobby not found");
            }

            return Ok(lobby.QuizId); // Return the QuizId associated with the lobby
        }
         
        // Model pro odstranění hráče
        public class RemovePlayerRequest
        {
            public int PlayerId { get; set; } // ID hráče nebo hosta
            public bool IsGuest { get; set; } // Určuje, zda je hráč hostem
        }
    }
}
