namespace QuizApi.Models
{
    public class Lobby
    {
        public int Id { get; set; }
        public string Pin { get; set; } 
        public List<User> Players { get; set; } = new List<User>(); 
        public List<Guest> Guests { get; set; } = new List<Guest>(); 
        public int QuizId { get; set; } 

        public int OwnerId { get; set; }
        public User Owner { get; set; } 
    }
}
