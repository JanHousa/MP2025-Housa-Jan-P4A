using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuizApi.Data;
using QuizApi.Models;

namespace QuizApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuizController : ControllerBase
    {
        private readonly QuizAppDbContext _context;
        private readonly ILogger<QuizController> _logger;

        public QuizController(QuizAppDbContext context, ILogger<QuizController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Vytvoření nového kvízu - chráněno [Authorize]
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateQuiz([FromBody] Quiz quiz)
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
            {
                _logger.LogWarning("User ID not found in token.");
                return Unauthorized("User ID not found in token.");
            }

            if (!int.TryParse(userIdClaim.Value, out var userId))
            {
                _logger.LogWarning("Invalid User ID in token: {TokenValue}", userIdClaim.Value);
                return Unauthorized("Invalid User ID in token.");
            }

            quiz.OwnerId = userId; // Nastavení vlastníka kvízu

            // Pokud quiz obsahuje otázky, vynulujte jejich ID, aby databáze mohla vygenerovat nová
            if (quiz.Questions != null)
            {
                foreach (var question in quiz.Questions)
                {
                    question.Id = 0;
                }
            }

            try
            {
                _logger.LogInformation("Creating quiz for user {UserId} with title '{QuizTitle}'.", userId, quiz.Title);
                _context.Quizzes.Add(quiz);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Quiz created successfully with ID {QuizId}.", quiz.Id);
                return Ok(quiz);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while creating the quiz.");
                return StatusCode(500, "An error occurred while creating the quiz.");
            }
        }



        [HttpGet("{quizId}/questions")]
        public async Task<IActionResult> GetQuizQuestions(int quizId)
        {
            // Retrieve questions for the specified QuizId
            var questions = await _context.Questions
                .Where(q => q.QuizId == quizId)
                .ToListAsync();

            if (!questions.Any())
            {
                return NotFound("No questions found for the specified quiz.");
            }

            return Ok(questions);
        }

        // Získání všech kvízů (veřejné)
        [HttpGet]
        public async Task<IActionResult> GetQuizzes()
        {
            var quizzes = await _context.Quizzes.Include(q => q.Questions).ToListAsync();
            return Ok(quizzes);
        }

        // Získání kvízu podle ID (veřejné)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetQuiz(int id)
        {
            var quiz = await _context.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == id);
            if (quiz == null) return NotFound("Quiz not found");

            return Ok(quiz);
        }

        [Authorize]
        [HttpPost("{quizId}/add-question")]
        public async Task<IActionResult> AddQuestion(int quizId, [FromBody] Question question)
        {
            if (question == null)
            {
                return BadRequest("The question field is required.");
            }

            var quiz = await _context.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null)
            {
                return NotFound("Quiz not found");
            }

            // Add question to the quiz
            quiz.Questions.Add(question);
            await _context.SaveChangesAsync();

            return Ok(question); // Return the saved question with its assigned Id
        }


        // Odeslání odpovědi na otázku v kvízu - chráněno [Authorize]
        [Authorize]
        [HttpPost("{quizId}/question/{questionId}/answer")]
        public async Task<IActionResult> SubmitAnswer(int quizId, int questionId, [FromBody] Answer answer)
        {
            var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId);
            if (question == null) return NotFound("Question not found");

            // Ověření správnosti odpovědi
            answer.IsCorrect = question.CorrectAnswer == answer.UserAnswer;

            // Výpočet bodů (např. body za správnost)
            answer.Points = answer.IsCorrect ? 10 : 0;

            // Nastavení QuizId a QuestionId v odpovědi
            answer.QuizId = quizId;
            answer.QuestionId = questionId;

            // Přidání odpovědi do databáze
            _context.Answers.Add(answer);
            await _context.SaveChangesAsync();

            return Ok(answer);
        }

        // Aktualizace kvízu
        [Authorize]
        [HttpPut("{quizId}")]
        public async Task<IActionResult> UpdateQuiz(int quizId, [FromBody] Quiz updatedQuiz)
        {
            var quiz = await _context.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null) return NotFound("Quiz not found");

            quiz.Title = updatedQuiz.Title;
            quiz.Description = updatedQuiz.Description;

            await _context.SaveChangesAsync();
            return Ok(quiz);
        }

        // Aktualizace otázky - chráněno [Authorize]
        [Authorize]
        [HttpPut("{quizId}/update-question/{questionId}")]
        public async Task<IActionResult> UpdateQuestion(int quizId, int questionId, [FromBody] Question updatedQuestion)
        {
            var quiz = await _context.Quizzes.Include(q => q.Questions).FirstOrDefaultAsync(q => q.Id == quizId);
            if (quiz == null) return NotFound("Quiz not found");

            var question = quiz.Questions.FirstOrDefault(q => q.Id == questionId);
            if (question == null) return NotFound("Question not found");

            // Aktualizace vlastností otázky
            question.Title = updatedQuestion.Title;
            question.Text = updatedQuestion.Text;
            question.ImageUrl = updatedQuestion.ImageUrl;
            question.Type = updatedQuestion.Type;
            question.CorrectAnswer = updatedQuestion.CorrectAnswer;

            // Zajištění, že možnosti odpovědí budou aktualizovány
            question.Options = updatedQuestion.Options;

            await _context.SaveChangesAsync();
            return Ok(question);
        }

        [Authorize]
        [HttpGet("user-quizzes")]
        public async Task<IActionResult> GetUserQuizzes()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == "id");
            if (userIdClaim == null)
            {
                return Unauthorized("User ID not found in token.");
            }

            if (!int.TryParse(userIdClaim.Value, out var userId))
            {
                return Unauthorized("Invalid User ID in token.");
            }

            var quizzes = await _context.Quizzes
                .Where(q => q.OwnerId == userId) // Filtr na základě vlastníka
                .Select(q => new { q.Id, q.Title })
                .ToListAsync();

            return Ok(quizzes);
        }

    }
}
