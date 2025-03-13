using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;
using static QuizFrontend.Pages.QuizHost;

namespace Frontend.Services
{
    public class SignalRClientService
    {
        private HubConnection _hubConnection;
        private readonly HttpClient _httpClient;
        private string _accessToken;

        public SignalRClientService(HttpClient httpClient)
        {
            _httpClient = httpClient; 
        }

        public bool IsConnected => _hubConnection?.State == HubConnectionState.Connected;

        public void SetAccessToken(string token) => _accessToken = token;

        public async Task StartConnection(string signalrUrl)
        {
            if (_hubConnection == null)
            {
                _hubConnection = new HubConnectionBuilder()
                    .WithUrl(signalrUrl, options =>
                    {
                        options.AccessTokenProvider = () => Task.FromResult(_accessToken);
                    })
                    .WithAutomaticReconnect()
                    .Build();
            }

            if (_hubConnection.State != HubConnectionState.Connected)
            {
                try
                {
                    await _hubConnection.StartAsync();
                    Console.WriteLine("SignalR connected.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SignalR connection failed: {ex.Message}");
                }
            }
        }

        private async Task EnsureConnection()
        {
            if (!IsConnected)
            {
                throw new InvalidOperationException("SignalR connection is not established.");
            }
        }

        public async Task<bool> CheckLobbyOwnershipViaApi(string lobbyPin, string token)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://localhost:5198/api/lobby/{lobbyPin}/isOwner")
                {
                    Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<bool>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error while checking ownership via API: {ex.Message}");
                return false;
            }
        }

        public void RegisterGameStarted(Action handleGameStarted) =>
            _hubConnection.On("GameStarted", handleGameStarted);

        public void RegisterReceiveMessage(Action<string> handleMessage) =>
            _hubConnection.On("ReceiveMessage", handleMessage);

        public void RegisterQuestion(Action<string, string, List<string>, string> handleQuestion) =>
            _hubConnection.On("ReceiveQuestion", handleQuestion);

        public void RegisterTimerUpdate(Action<int> handleTimerUpdate) =>
            _hubConnection.On("UpdateTimer", handleTimerUpdate);

        public void RegisterPlayerAnswers(Action<List<PlayerAnswer>> handlePlayerAnswers) =>
            _hubConnection.On("UpdatePlayerAnswers", handlePlayerAnswers);

        public void RegisterUpdatePlayerScores(Action<Dictionary<string, int>> handlePlayerScores) =>
            _hubConnection.On("UpdatePlayerScores", handlePlayerScores);

        public void RegisterUpdateUserList(Action<List<string>, Dictionary<string, string>> handleUserList) =>
            _hubConnection.On("UpdateUserList", handleUserList);

        public void RegisterNavigateToGamePage(Action<string> navigateToGamePage) =>
            _hubConnection.On("NavigateToGamePage", navigateToGamePage);

        public void RegisterCorrectAnswer(Action<string> handleCorrectAnswer) =>
            _hubConnection.On("ReceiveCorrectAnswer", handleCorrectAnswer);


        public void RegisterShowFinalScoreboard(Action handleShowFinalScoreboard) =>
            _hubConnection.On("ShowFinalScoreboard", handleShowFinalScoreboard);

        public async Task JoinLobby(string lobbyPin, string nickname, string avatarUrl)
        {
            await EnsureConnection();
            try
            {
                await _hubConnection.InvokeAsync("JoinLobby", lobbyPin, nickname, avatarUrl);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to join lobby: {ex.Message}");
            }
        }

        public async Task SendMessage(string lobbyPin, string message)
        {
            await EnsureConnection();
            try
            {
                await _hubConnection.InvokeAsync("SendMessage", lobbyPin, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send message: {ex.Message}");
            }
        }

        public async Task StartGame(string lobbyPin)
        {
            await EnsureConnection();
            try
            {
                await _hubConnection.InvokeAsync("StartGame", lobbyPin);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start game: {ex.Message}");
            }
        }

        public async Task SubmitAnswer(string lobbyPin, string nickname, string answer)
        {
            await EnsureConnection();
            try
            {
                await _hubConnection.InvokeAsync("SubmitAnswer", lobbyPin, nickname, answer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to submit answer: {ex.Message}");
            }
        }

        public async Task<int> GetQuizIdFromLobby(string lobbyPin, string token)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://localhost:5198/api/lobby/{lobbyPin}/quizId")
                {
                    Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<int>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch QuizId for Lobby: {ex.Message}");
                throw;
            }
        }

        public async Task<List<QuestionModel>> GetQuizQuestions(int quizId, string token)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://localhost:5198/api/quiz/{quizId}/questions")
                {
                    Headers = { Authorization = new AuthenticationHeaderValue("Bearer", token) }
                };

                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                return await response.Content.ReadFromJsonAsync<List<QuestionModel>>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch questions for QuizId {quizId}: {ex.Message}");
                throw;
            }
        }

        public async Task SendQuestion(string lobbyPin, string question, string imageUrl, List<string> options, string correctAnswer)
        {
            await EnsureConnection();
            try
            {
                await _hubConnection.InvokeAsync("SendQuestion", lobbyPin, question, imageUrl, options, correctAnswer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to send question: {ex.Message}");
            }
        }

        public async Task EndQuiz(string lobbyPin)
        {
            await EnsureConnection();
            try
            {
                await _hubConnection.InvokeAsync("EndQuiz", lobbyPin);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to end quiz: {ex.Message}");
            }
        }

        public async Task StopConnection()
        {
            if (_hubConnection != null)
            {
                await _hubConnection.StopAsync();
                _hubConnection = null;
            }
        }
    }

    public class PlayerAnswer
    {
        public string Nickname { get; set; }
        public string Answer { get; set; }
    }
}
