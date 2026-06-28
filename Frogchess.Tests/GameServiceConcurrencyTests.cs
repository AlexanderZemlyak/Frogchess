using ErrorOr;
using Frogchess.Hubs;
using Frogchess.Models.Dtos;
using Frogchess.Services;
using Microsoft.AspNetCore.SignalR;
using Moq;

namespace Frogchess.Tests
{
    public class GameServiceConcurrencyTests
    {
        private readonly GameService gameService;
        private readonly Mock<IHubContext<GameHub>> hubContextMock;

        private Mock<IHubContext<GameHub>> CreateMockHubContext()
        {
            var mockClients = new Mock<IHubClients>();
            var mockClientProxy = new Mock<IClientProxy>();
            var mockHubContext = new Mock<IHubContext<GameHub>>();

            mockClientProxy
                .Setup(p => p.SendCoreAsync(
                    It.IsAny<string>(),
                    It.IsAny<object[]>(),
                    It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            mockClients
                .Setup(c => c.Group(It.IsAny<string>()))
                .Returns(mockClientProxy.Object);

            mockClients
                .Setup(c => c.GroupExcept(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>()))
                .Returns(mockClientProxy.Object);

            mockClients
                .Setup(c => c.All)
                .Returns(mockClientProxy.Object);

            mockHubContext
                .Setup(h => h.Clients)
                .Returns(mockClients.Object);

            return mockHubContext;
        }

        public GameServiceConcurrencyTests()
        {
            hubContextMock = CreateMockHubContext();
            gameService = new GameService(hubContextMock.Object);
        }

        private void CheckInvariants(Game game)
        {
            Assert.Equal(2, game.Players.Count(p => p.IsConnected));

            var ids = game.Players.Select(p => p.Id).Distinct().Count();
            var names = game.Players.Select(p => p.Name).Distinct().Count();
            Assert.Equal(game.Players.Count, ids);
            Assert.Equal(game.Players.Count, names);

            var colors = game.Players.Select(p => p.Color).Distinct().Count();
            Assert.Equal(game.Players.Count, colors);

            Assert.Contains(game.Players, p => p.Order == 0);
            Assert.Contains(game.Players, p => p.Order == 1);

            var board = game.GetBoardData();
            var frogCount = board.SelectMany(r => r).Count(v => v != 0);
            Assert.True(frogCount <= 36);
        }

        [Fact]
        public async Task ParallelSimilarMoves_MaxTwoSucceed()
        {
            var joinResult1 = await gameService.JoinOrCreateGameAsync(null, "Player1");
            var joinResult2 = await gameService.JoinOrCreateGameAsync(
                joinResult1.Value.GameId.ToString(), "Player2");

            var gameId = joinResult1.Value.GameId.ToString();
            var player1Id = joinResult1.Value.PlayerId.ToString();
            var player2Id = joinResult2.Value.PlayerId.ToString();

            gameService.RegisterConnection(gameId, player1Id, "connection1");
            gameService.RegisterConnection(gameId, player2Id, "connection2");

            // 10 параллельных ходов от обоих игроков
            var tasks = new List<Task<ErrorOr<MoveResult>>>();

            for (int i = 0; i < 5; i++)
            {
                tasks.Add(gameService.ProcessMoveAsync(gameId, new MoveRequest
                {
                    PlayerId = player1Id,
                    MovePositions = [],
                    RemoveFrog = true,
                    FrogToRemove = new Position(1, 1)
                }));

                tasks.Add(gameService.ProcessMoveAsync(gameId, new MoveRequest
                {
                    PlayerId = player2Id,
                    MovePositions = [],
                    RemoveFrog = true,
                    FrogToRemove = new Position(2, 2)
                }));
            }

            var results = await Task.WhenAll(tasks);

            // Либо один, либо два успешных хода
            var successCount = results.Count(r => !r.IsError);
            Assert.True(successCount >= 1 && successCount <= 2);

            var game = gameService.games[joinResult1.Value.GameId];

            Assert.Equal(0, game.GetBoardData()[1][1]);

            Assert.Equal("Player1", game.Players[0].Name);
            Assert.Equal(0, game.Players[0].Order);
            Assert.Equal(player1Id, game.Players[0].Id.ToString());
            
            Assert.Equal("Player2", game.Players[1].Name);
            Assert.Equal(1, game.Players[1].Order);
            Assert.Equal(player2Id, game.Players[1].Id.ToString());
            
            Assert.False(game.IsCurrentMoveFirstForPlayer(game.Players[0]));

            CheckInvariants(game);
        }

        [Fact]
        public async Task ParallelJoins_SameGame_OnlyTwoPlayersAccepted()
        {
            var game = await gameService.JoinOrCreateGameAsync(null, "Creator");
            var gameId = game.Value.GameId.ToString();

            // 10 игроков пытаются одновременно присоединиться
            var joinTasks = new List<Task<ErrorOr<JoinResult>>>();
            for (int i = 0; i < 10; i++)
            {
                var playerName = $"Player{i}";
                joinTasks.Add(gameService.JoinOrCreateGameAsync(gameId, playerName));
            }

            var results = await Task.WhenAll(joinTasks);

            // Только 1 успешное присоединение
            var successCount = results.Count(r => !r.IsError);
            Assert.Equal(1, successCount);

            var errors = results.Where(r => r.IsError)
                .Select(r => r.FirstError.Description)
                .ToList();
            Assert.All(errors, e => Assert.Contains("game is full", e.ToLower()));

            CheckInvariants(gameService.games[game.Value.GameId]);
        }
    }
}
