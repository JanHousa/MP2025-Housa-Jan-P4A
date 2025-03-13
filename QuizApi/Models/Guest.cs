namespace QuizApi.Models
{
    public class Guest
    {
        public int Id { get; set; }
        public string Nickname { get; set; }
        public int LobbyId { get; set; } 
        public Lobby Lobby { get; set; }
    }
}
