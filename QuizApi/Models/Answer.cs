namespace QuizApi.Models
{
    public class Answer
    {
        public int Id { get; set; }
        public int QuizId { get; set; }
        public int QuestionId { get; set; }
        public string UserAnswer { get; set; }
        public bool IsCorrect { get; set; }
        public int Points { get; set; }
        public string UserId { get; set; } 
    }

}
