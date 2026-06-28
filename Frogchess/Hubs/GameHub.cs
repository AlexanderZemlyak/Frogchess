using Frogchess.Services;
using Microsoft.AspNetCore.SignalR;

namespace Frogchess.Hubs
{
    /// <summary>
    /// Хаб, отвечающий за WebSocket соединение с клиентами игры Frog Chess.
    /// Уведомляет пользователей о событиях подключения игроков, ходов и т. д.
    /// </summary>
    public class GameHub : Hub
    {
        private readonly IGameService gameService;

        public GameHub(IGameService gameService)
        {
            this.gameService = gameService;
        }

        /// <summary>
        /// Подключиться к игре по WebSocket. Запоминает связь connectionId и playerId.
        /// </summary>
        public async Task SubscribeToGame(string gameId, string playerId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, gameId);

            gameService.RegisterConnection(gameId, playerId, Context.ConnectionId);

            await Clients.Caller.SendAsync("Subscribed", new { gameId });
        }

        /// <summary>
        /// Запускает таймер после отключения игрока, в течение которого он может вернуться в игру
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await gameService.HandleDisconnect(Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
    }
}
