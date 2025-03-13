using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QuizApi.Hubs
{
    public class GameHub : Hub
    {
        private static readonly Dictionary<string, List<string>> lobbyUsers = new Dictionary<string, List<string>>();
        private static readonly Dictionary<string, Dictionary<string, string>> lobbyAnswers = new Dictionary<string, Dictionary<string, string>>();
        // Nový slovník pro uložení času, který zbýval při odeslání odpovědi hráče
        private static readonly Dictionary<string, Dictionary<string, int>> lobbyAnswerTimes = new Dictionary<string, Dictionary<string, int>>();
        // Slovník pro uložení počátečního času otázky pro daný lobby
        private static readonly Dictionary<string, DateTime> lobbyQuestionStartTimes = new Dictionary<string, DateTime>();

        private static readonly Dictionary<string, string> connectionIdToUser = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> userAvatars = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> lobbyOwners = new Dictionary<string, string>();
        private static readonly Dictionary<string, Dictionary<string, int>> lobbyScores = new Dictionary<string, Dictionary<string, int>>();

        public async Task JoinLobby(string lobbyPin, string nickname, string avatarUrl)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyPin);

            if (!lobbyUsers.ContainsKey(lobbyPin))
            {
                lobbyUsers[lobbyPin] = new List<string>();
            }

            // Zajištění, že nedojde k duplicitním uživatelům
            var existingConnection = connectionIdToUser.FirstOrDefault(kvp => kvp.Value == nickname).Key;
            if (!string.IsNullOrEmpty(existingConnection))
            {
                connectionIdToUser.Remove(existingConnection);
                lobbyUsers[lobbyPin].Remove(nickname);
            }

            connectionIdToUser[Context.ConnectionId] = nickname;
            lobbyUsers[lobbyPin].Add(nickname);
            userAvatars[nickname] = avatarUrl;

            // Přiřazení vlastníka lobby, pokud je to první uživatel
            if (!lobbyOwners.ContainsKey(lobbyPin))
            {
                lobbyOwners[lobbyPin] = Context.ConnectionId;
                Console.WriteLine($"Lobby owner assigned: {nickname} for lobby {lobbyPin}");
            }

            await UpdateLobbyUsers(lobbyPin);
        }

        public async Task LeaveLobby(string lobbyPin, string nickname)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, lobbyPin);

            if (lobbyUsers.ContainsKey(lobbyPin))
            {
                lobbyUsers[lobbyPin].Remove(nickname);
                connectionIdToUser.Remove(Context.ConnectionId);

                // Pokud vlastník lobby odešel, přidělí se nový vlastník
                if (lobbyOwners.ContainsKey(lobbyPin) && lobbyOwners[lobbyPin] == Context.ConnectionId)
                {
                    lobbyOwners[lobbyPin] = lobbyUsers[lobbyPin].FirstOrDefault();
                }

                await UpdateLobbyUsers(lobbyPin);
            }
        }

        public async Task StartGame(string lobbyPin)
        {
            if (!lobbyOwners.TryGetValue(lobbyPin, out var ownerConnectionId) || ownerConnectionId != Context.ConnectionId)
            {
                await Clients.Caller.SendAsync("ErrorMessage", "Only the lobby owner can start the game.");
                return;
            }

            if (!lobbyAnswers.ContainsKey(lobbyPin))
            {
                lobbyAnswers[lobbyPin] = new Dictionary<string, string>();
            }

            // Inicializace slovníku pro časy odpovědí
            if (!lobbyAnswerTimes.ContainsKey(lobbyPin))
            {
                lobbyAnswerTimes[lobbyPin] = new Dictionary<string, int>();
            }

            // Notifikace pro ostatní hráče
            await Clients.OthersInGroup(lobbyPin).SendAsync("NavigateToGamePage", lobbyPin);

            // Notifikace pro vlastníka
            await Clients.Caller.SendAsync("NavigateToOwnerGamePage", lobbyPin);
        }

        public async Task SendQuestion(string lobbyPin, string question, string imageUrl, List<string> options, string correctAnswer)
        {
            if (!lobbyUsers.ContainsKey(lobbyPin))
            {
                throw new InvalidOperationException("Lobby does not exist.");
            }

            if (!lobbyOwners.TryGetValue(lobbyPin, out var ownerConnectionId) || ownerConnectionId != Context.ConnectionId)
            {
                throw new UnauthorizedAccessException("Only the lobby owner can send questions.");
            }

            // Inicializace nebo vyčištění odpovědí a časů odpovědí
            if (!lobbyAnswers.ContainsKey(lobbyPin))
            {
                lobbyAnswers[lobbyPin] = new Dictionary<string, string>();
            }
            else
            {
                lobbyAnswers[lobbyPin].Clear();
            }

            if (!lobbyAnswerTimes.ContainsKey(lobbyPin))
            {
                lobbyAnswerTimes[lobbyPin] = new Dictionary<string, int>();
            }
            else
            {
                lobbyAnswerTimes[lobbyPin].Clear();
            }

            // Zaznamenáme čas, kdy byla otázka odeslána
            lobbyQuestionStartTimes[lobbyPin] = DateTime.UtcNow;

            // Rozeslání otázky všem hráčům
            await Clients.Group(lobbyPin).SendAsync("ReceiveQuestion", question, imageUrl, options, correctAnswer);

            // Odpočet
            for (int i = 20; i >= 0; i--)
            {
                await Clients.Group(lobbyPin).SendAsync("UpdateTimer", i);
                await Task.Delay(1000);
            }

            // Inicializace skóre pro lobby, pokud ještě neexistuje
            if (!lobbyScores.ContainsKey(lobbyPin))
            {
                lobbyScores[lobbyPin] = new Dictionary<string, int>();
            }

            // Vyhodnocení odpovědí a přičítání bodů podle rychlosti
            foreach (var kvp in lobbyAnswers[lobbyPin])
            {
                // Kontrola, zda je odpověď správná
                if (kvp.Value == correctAnswer)
                {
                    // Získání bodů na základě času, který zbýval (výchozí maximální čas je 20 sekund)
                    int points = 0;
                    if (lobbyAnswerTimes[lobbyPin].TryGetValue(kvp.Key, out int timeLeft))
                    {
                        points = timeLeft;
                    }

                    if (!lobbyScores[lobbyPin].ContainsKey(kvp.Key))
                    {
                        lobbyScores[lobbyPin][kvp.Key] = 0;
                    }
                    lobbyScores[lobbyPin][kvp.Key] += points;
                }
            }

            // Rozeslání správné odpovědi a výsledků
            await Clients.Group(lobbyPin).SendAsync("ReceiveCorrectAnswer", correctAnswer);

            var answers = lobbyAnswers[lobbyPin].Select(a => new PlayerAnswer { Nickname = a.Key, Answer = a.Value }).ToList();
            await Clients.Group(lobbyPin).SendAsync("UpdatePlayerAnswers", answers);

            // Odeslání skóre klientům
            var scores = lobbyScores[lobbyPin];
            await Clients.Group(lobbyPin).SendAsync("UpdatePlayerScores", scores);
        }

        public Dictionary<string, int> GetPlayerScores(string lobbyPin)
        {
            if (lobbyScores.ContainsKey(lobbyPin))
            {
                return lobbyScores[lobbyPin];
            }
            return new Dictionary<string, int>();
        }

        public async Task SubmitAnswer(string lobbyPin, string nickname, string answer)
        {
            if (!lobbyAnswers.ContainsKey(lobbyPin))
            {
                return; 
            }

            if (!string.IsNullOrEmpty(answer))
            {
                lobbyAnswers[lobbyPin][nickname] = answer;
                Console.WriteLine($"{nickname} submitted an answer: {answer}");

                if (lobbyQuestionStartTimes.TryGetValue(lobbyPin, out DateTime startTime))
                {
                    int elapsedSeconds = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                    int remainingTime = Math.Max(20 - elapsedSeconds, 0);

                    if (!lobbyAnswerTimes.ContainsKey(lobbyPin))
                    {
                        lobbyAnswerTimes[lobbyPin] = new Dictionary<string, int>();
                    }
                    lobbyAnswerTimes[lobbyPin][nickname] = remainingTime;
                }
            }

            var answers = lobbyAnswers[lobbyPin].Select(a => new PlayerAnswer
            {
                Nickname = a.Key,
                Answer = a.Value
            }).ToList();

            await Clients.Group(lobbyPin).SendAsync("UpdatePlayerAnswers", answers);
        }

        public async Task SendMessage(string lobbyPin, string message)
        {
            await Clients.Group(lobbyPin).SendAsync("ReceiveMessage", message);
        }

        public async Task EndQuiz(string lobbyPin)
        {
            await Clients.Group(lobbyPin).SendAsync("ShowFinalScoreboard");
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            string disconnectedUser = null;
            string lobbyPin = null;

            foreach (var lobby in lobbyUsers.Keys.ToList())
            {
                if (connectionIdToUser.TryGetValue(Context.ConnectionId, out disconnectedUser))
                {
                    lobbyPin = lobby;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(disconnectedUser) && !string.IsNullOrEmpty(lobbyPin))
            {
                lobbyUsers[lobbyPin].Remove(disconnectedUser);
                connectionIdToUser.Remove(Context.ConnectionId);

                if (lobbyOwners.ContainsKey(lobbyPin) && lobbyOwners[lobbyPin] == Context.ConnectionId)
                {
                    lobbyOwners[lobbyPin] = lobbyUsers[lobbyPin].FirstOrDefault();
                }

                await UpdateLobbyUsers(lobbyPin);
            }

            await base.OnDisconnectedAsync(exception);
        }

        private async Task UpdateLobbyUsers(string lobbyPin)
        {
            if (lobbyUsers.ContainsKey(lobbyPin))
            {
                var users = lobbyUsers[lobbyPin];
                var avatars = users.ToDictionary(u => u, u => userAvatars[u]);

                await Clients.Group(lobbyPin).SendAsync("UpdateUserList", users, avatars);
            }
        }

        public Task<bool> CheckLobbyOwnership(string lobbyPin)
        {
            var isOwner = lobbyOwners.TryGetValue(lobbyPin, out var ownerConnectionId) && ownerConnectionId == Context.ConnectionId;
            Console.WriteLine($"CheckLobbyOwnership called for lobbyPin: {lobbyPin}, isOwner: {isOwner}");
            return Task.FromResult(isOwner);
        }

        public class PlayerAnswer
        {
            public string Nickname { get; set; }
            public string Answer { get; set; }
        }
    }
}
