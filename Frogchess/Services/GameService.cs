using ErrorOr;
using Frogchess.Hubs;
using Frogchess.Models.Dtos;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using Frogchess.Domain;

namespace Frogchess.Services
{

    /// <summary>
    /// Данные об игроке
    /// </summary>
    internal class Player
    {
        public required Guid Id { get; set; }
        public required string Name { get; set; }
        public required int Order { get; set; }
        public required Color Color { get; set; }
        public bool IsConnected { get; set; } = true;
    }

    /// <summary>
    /// Данные об одной активной игре
    /// </summary>
    internal class Game
    {
        private const int PLAYERS_COUNT = 2;
        private const int BOARD_WIDTH = 8;
        private const int BOARD_HEIGHT = 8;

        public Guid Id { get; set; }

        /// <summary>
        /// Игроки данной игры
        /// </summary>
        public List<Player> Players { get; } = [];

        private readonly FrogBoard board;
        private readonly TurnManager turnManager;

        public Game() 
        {
            board = new FrogBoard(BOARD_WIDTH, BOARD_HEIGHT, PLAYERS_COUNT);
            // Расстановка лягушек заранее
            board.PositionFrogs();
            turnManager = new TurnManager(PLAYERS_COUNT, board);
        }

        /// <summary>
        /// Обработать ход игрока и подготовить данные о результатах хода для отправки на клиент
        /// </summary>
        public ErrorOr<MoveResult> ProcessPlayerMove(Guid playerId, Position[] movePositions, bool removeFrog, Position? frogToRemove)
        {
            var players = Players;

            var player = players.Find(p => p.Id == playerId);

            if (player == null)
                return Error.NotFound(description: "No player with such ID");

            int playerIndex = player.Order;

            var gameState = turnManager.ProcessPlayerMove(
                playerIndex, 
                movePositions.Select(pos => (pos.X, pos.Y)).ToArray(), 
                removeFrog, 
                frogToRemove != null ? (frogToRemove.Value.X, frogToRemove.Value.Y) : null
                );

            return gameState.Match<ErrorOr<MoveResult>>(
                success => new MoveResult()
                {
                    MovePositions = movePositions,
                    FrogWasRemoved = removeFrog,
                    RemovedFrog = frogToRemove,
                    NextPlayerId = players.Find(p => p.Order == success.nextPlayer)!.Id.ToString(),
                    GameFinished = success.gameFinished,
                    WinnerId = success.gameFinished ? players.Find(p => p.Order == success.winner)!.Id.ToString() : null
                },
                errors =>

                errors[0].Type switch
                {
                    ErrorType.Unexpected => Error.Unexpected(description: errors[0].Description),
                    _ => Error.Unexpected(description: errors[0].Description)
                }
            );
        }

        /// <summary>
        /// Id игрока, который ходит следующим
        /// </summary>
        /// <returns></returns>
        public Guid GetCurrentPlayer()
        {
            return Players[turnManager.CurrentPlayer].Id;
        }

        /// <summary>
        /// Будет ли следующий ход игрока первым для него
        /// </summary>
        public bool IsCurrentMoveFirstForPlayer(Player player)
        {
            return player.Order >= turnManager.MovesMade;
        }

        /// <summary>
        /// Возвращает представление доски в виде массива массивов
        /// </summary>
        /// <returns></returns>
        public int[][] GetBoardData()
        {
            var board2D = board.GetBoardData();

            return Enumerable.Range(0, board2D.GetLength(0))
                   .Select(i => Enumerable.Range(0, board2D.GetLength(1))
                   .Select(j => board2D[i, j])
                   .ToArray())
                   .ToArray();
        }

        /// <summary>
        /// Добавить игрока в игру (id для него генерируется автоматически)
        /// Первый игрок получает красный цвет лягушек, второй - зеленый.
        /// </summary>
        public Player AddPlayer(string playerName)
        {
            Color color = Players.Count == 0 ? Color.Red : Color.Green;

            Player newPlayer = new() { Id = Guid.NewGuid(), Name = playerName, Color = color, Order = Players.Count };
            Players.Add(newPlayer);
            return newPlayer;
        }
    }

    /// <summary>
    /// Данные об игре и игроке для конкретного WebSocket соединения
    /// </summary>
    internal class PlayerConnection
    {
        public required Guid GameId { get; set; }
        public required Guid PlayerId { get; set; }
    }

    /// <summary>
    /// Сервис реализует логику подключения игроков к игре, создания новых игр,
    /// регистрации WebSocket соединений, отправки уведомлений о событиях игры,
    /// обрабатывет ходы игроков, управляет жизненным циклом игр
    /// </summary>
    public class GameService : IGameService
    {
        internal readonly ConcurrentDictionary<Guid, Game> games = new();
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> gameLocks = new();
        private readonly ConcurrentDictionary<string, PlayerConnection> connections = new();
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> disconnectTimers = new();

        private readonly IHubContext<GameHub> hubContext;

        public GameService(IHubContext<GameHub> hubContext)
        {
            this.hubContext = hubContext;
        }

        /// <summary>
        /// Зарегистрировать WebSocket соединение для игрока
        /// </summary>
        public void RegisterConnection(string gameId, string playerId, string connectionId)
        {
            if (!Guid.TryParse(gameId, out var gameGuid))
                return;

            if (!Guid.TryParse(playerId, out var playerGuid))
                return;

            connections[connectionId] = new PlayerConnection
            {
                GameId = gameGuid,
                PlayerId = playerGuid
            };

            // Обновляем статус игрока в игре
            if (games.TryGetValue(gameGuid, out var game))
            {
                var player = game.Players.FirstOrDefault(p => p.Id == playerGuid);
                if (player != null)
                {
                    player.IsConnected = true;
                }
            }
        }

        /// <summary>
        /// Зарегистрировать разрыв соединения с игроком, оповестить оппонента и запустить таймер 
        /// для предоставления возможности вернуться в игру. После таймаута удаляет игру.
        /// </summary>
        public async Task HandleDisconnect(string connectionId)
        {
            if (connections.TryRemove(connectionId, out var connection))
            {
                if (games.TryGetValue(connection.GameId, out var game))
                {
                    var player = game.Players.FirstOrDefault(p => p.Id == connection.PlayerId);
                    if (player != null)
                    {
                        player.IsConnected = false;

                        await hubContext.Clients
                            .GroupExcept(game.Id.ToString(), connectionId)
                            .SendAsync("OpponentDisconnected");

                        var cts = new CancellationTokenSource();
                        disconnectTimers[connection.PlayerId] = cts;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(30), cts.Token);

                                // Таймер истёк — игрок не вернулся
                                if (games.TryGetValue(connection.GameId, out var currentGame))
                                {
                                    var disconnectedPlayer = currentGame.Players
                                        .FirstOrDefault(p => p.Id == connection.PlayerId);

                                    // Принудительно завершаем игру
                                    if (disconnectedPlayer != null && !disconnectedPlayer.IsConnected)
                                    {
                                        await hubContext.Clients
                                            .Group(connection.GameId.ToString())
                                            .SendAsync("OpponentReconnectTimeout");

                                        await RemoveGame(connection.GameId);
                                    }
                                }
                            }
                            catch (TaskCanceledException)
                            {
                                // Таймер отменён — игрок переподключился
                            }
                            finally
                            {
                                disconnectTimers.TryRemove(connection.PlayerId, out _);
                            }
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Остановить таймер дисконнекта для игрока
        /// </summary>
        private void CancelDisconnectTimer(Guid playerId)
        {
            if (disconnectTimers.TryRemove(playerId, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
            }
        }

        /// <summary>
        /// Если gameId == null, то создает новую активную игру.
        /// Иначе добавляет игрока к существующей игре, если это возможно.
        /// Поддерживает переподключение после отсутствия.
        /// Потокобезопасность обеспечивается.
        /// </summary>
        public async Task<ErrorOr<JoinResult>> JoinOrCreateGameAsync(string? gameId, string playerName)
        {
            // Создать новую игру
            if (string.IsNullOrWhiteSpace(gameId))
            {
                var game = new Game
                {
                    Id = Guid.NewGuid()
                };

                var player = game.AddPlayer(playerName);
                games[game.Id] = game; 
                gameLocks[game.Id] = new SemaphoreSlim(1, 1);

                return new JoinResult
                {
                    GameId = game.Id,
                    PlayerId = player.Id,
                    Color = player.Color,
                    Order = 0,
                    Opponent = null,
                    Board = game.GetBoardData(),
                    IsFirstMove = true,
                    IsPlayerMove = true
                };
            }

            // Присоединиться к существующей игре
            if (!Guid.TryParse(gameId, out var guid))
                return Error.Validation(description: "Invalid Game ID format");

            if (!games.TryGetValue(guid, out var existingGame))
                return Error.NotFound(description: "Game not found");

            var gameLock = gameLocks[guid];

            await gameLock.WaitAsync();

            try
            {
                var isReconnect = false;

                if (existingGame.Players.Count == 2)
                {
                    var p = existingGame.Players.Find(pl => pl.Name == playerName && !pl.IsConnected);

                    if (p != null)
                    {
                        p.IsConnected = true;
                        CancelDisconnectTimer(p.Id);
                        isReconnect = true;
                    }
                    else
                        return Error.Failure(description: "Game is full");
                }

                // Игрок переподключается
                if (isReconnect)
                {
                    var player2 = existingGame.Players.Find(pl => pl.Name == playerName)!;

                    await hubContext.Clients
                        .Group(existingGame.Id.ToString())
                        .SendAsync("OpponentReconnected");

                    return new JoinResult
                    {
                        GameId = existingGame.Id,
                        PlayerId = player2.Id,
                        Color = player2.Color,
                        Order = existingGame.Players[0] == player2 ? 0 : 1,
                        Opponent = existingGame.Players.First(pl => pl != player2).Name,
                        Board = existingGame.GetBoardData(),
                        IsFirstMove = existingGame.IsCurrentMoveFirstForPlayer(player2),
                        IsPlayerMove = existingGame.GetCurrentPlayer() == player2.Id
                    };
                }
                else
                {
                    if (existingGame.Players[0].Name == playerName)
                        return Error.Conflict(description: "This name is already taken.");

                    var player2 = existingGame.AddPlayer(playerName);

                    // Уведомить первого игрока, что противник подключился
                    await hubContext.Clients
                        .Group(existingGame.Id.ToString()) // в группе пока только первый игрок
                        .SendAsync("OpponentJoined", new
                        {
                            opponentName = player2.Name,
                            color = player2.Color
                        });

                    return new JoinResult
                    {
                        GameId = existingGame.Id,
                        PlayerId = player2.Id,
                        Color = player2.Color,
                        Order = 1,
                        Opponent = existingGame.Players.First().Name,
                        Board = existingGame.GetBoardData(),
                        IsFirstMove = true,
                        IsPlayerMove = false
                    };
                }
            }
            finally
            {
                gameLock.Release();
            }
        }

        /// <summary>
        /// Обработать ход игрока, уведомить пользователей о ходе и завершить игру при необходимости.
        /// Потокобезопасность обеспечивается.
        /// </summary>
        public async Task<ErrorOr<MoveResult>> ProcessMoveAsync(string gameId, MoveRequest moveRequest)
        {
            if (!Guid.TryParse(gameId, out var gameGuid))
                return Error.Validation(description: "Invalid Game ID format");

            if (!Guid.TryParse(moveRequest.PlayerId, out var playerGuid))
                return Error.Validation(description: "Invalid Player ID format");

            if (!games.TryGetValue(gameGuid, out var game))
                return Error.NotFound(description: "No active games with such ID");

            var gameLock = gameLocks[gameGuid];

            // Здесь race condition не будет, но это нужно согласно требованиям к тестам
            await gameLock.WaitAsync();

            ErrorOr<MoveResult> moveResult;
            var finishGame = false;
            try
            {
                moveResult = game.ProcessPlayerMove(playerGuid, moveRequest.MovePositions, moveRequest.RemoveFrog, moveRequest.FrogToRemove);

                // Уведомляем всех о результате хода
                if (moveResult.IsSuccess)
                {
                    await hubContext.Clients
                     .GroupExcept(game.Id.ToString(), connections.First(c => c.Value.PlayerId == playerGuid).Key)
                     .SendAsync("OpponentMoved", new
                     {
                         playerId = moveRequest.PlayerId,
                         moveResult = moveResult.Value
                     });

                    if (moveResult.Value.GameFinished)
                    {
                        finishGame = true;
                    }
                }
            }
            finally
            {
                gameLock.Release();
            }

            // Конец игры - освобождение ресурсов
            if (finishGame)
                await RemoveGame(gameGuid);

            return moveResult;
        }

        /// <summary>
        /// Удалить данные об активной игре и отсоединить игроков, относящихся к ней
        /// </summary>
        private async Task RemoveGame(Guid gameGuid)
        {
            games.TryRemove(gameGuid, out _);
            if (gameLocks.TryRemove(gameGuid, out var lockToDispose))
            {
                lockToDispose.Dispose();
            }

            var connectionsToRemove = connections.Where(c => c.Value.GameId == gameGuid).Select(kv => kv.Key).ToArray();

            foreach (var connectionId in connectionsToRemove)
            {
                await hubContext.Groups.RemoveFromGroupAsync(connectionId, gameGuid.ToString());
                connections.TryRemove(connectionId, out var _);
            }
        }
    }
}
