using System.ComponentModel.DataAnnotations.Schema;

namespace QuizApi.Models
{
    public class Question
        {

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
            public string Title { get; set; }
            public string ImageUrl { get; set; }
            public string Text { get; set; }
            public string Type { get; set; }
            public List<string> Options { get; set; } = new List<string>();
            public string CorrectAnswer { get; set; }
            public int QuizId { get; internal set; }
        }
    }
