using Frogchess.Domain;
using ErrorOr;

namespace Frogchess.UnitTests.Domain
{
    public class TurnManagerTests
    {
        private readonly FrogBoard board;
        private readonly TurnManager turnManager;

        private const int BOARD_WIDTH = 8;
        private const int BOARD_HEIGHT = 8;
        private const int PLAYERS_COUNT = 2;

        public TurnManagerTests()
        {
            board = new FrogBoard(BOARD_WIDTH, BOARD_HEIGHT, PLAYERS_COUNT);
            turnManager = new TurnManager(PLAYERS_COUNT, board);
        }

        private ErrorOr<GameState> ProcessMove(int playerIndex, (int, int)[] movePositions)
        {
            return turnManager.ProcessPlayerMove(playerIndex, movePositions, false, (-1, -1));
        }

        private ErrorOr<GameState> RemoveFrog(int playerIndex, (int, int) frogToRemove)
        {
            return turnManager.ProcessPlayerMove(playerIndex, [], true, frogToRemove);
        }

        [Fact]
        public void ProcessPlayerMove_WrongPlayerIndex()
        {
            // Первый ход должен быть за первым игроком
            var moveResult = ProcessMove(1, []);

            Assert.True(moveResult.IsError);
            Assert.Equal("TurnManager.InvalidPlayerIndex", moveResult.Errors[0].Code);

            // повторный ход первого игрока, когда у второго не пропуск хода
            board.SetFrog(1, 1, 1);
            board.SetFrog(1, 2, 2);
            board.SetFrog(2, 3, 1);
            board.SetFrog(1, 4, 2);
            moveResult = ProcessMove(0, [(1, 1), (1, 3)]);

            Assert.True(moveResult.IsSuccess);
            Assert.Equal(1, moveResult.Value.nextPlayer);
            Assert.False(moveResult.Value.gameFinished);

            moveResult = ProcessMove(0, [(2, 3), (0, 3)]);

            Assert.True(moveResult.IsError);
            Assert.Equal("TurnManager.InvalidPlayerIndex", moveResult.Errors[0].Code);
        }

        [Fact]
        public void ProcessPlayerMove_FrogRemovalIsNotPossible()
        {
            // удаление лягушки с пустой клетки
            var moveResult = RemoveFrog(0, (1, 1));

            Assert.True(moveResult.IsError);
            Assert.Equal("TurnManager.FrogRemovalIsNotPossible", moveResult.Errors[0].Code);

            board.SetFrog(1, 1, 1);
            board.SetFrog(1, 2, 2);
            board.SetFrog(2, 3, 1);

            moveResult = ProcessMove(0, [(1, 1), (1, 3)]);

            Assert.True(moveResult.IsSuccess);
            Assert.Equal(0, moveResult.Value.nextPlayer);

            // удаление не в свой ход
            moveResult = RemoveFrog(1, (1, 3));

            Assert.True(moveResult.IsError);
            Assert.Equal("TurnManager.InvalidPlayerIndex", moveResult.Errors[0].Code);

            // удаление не в первый ход
            moveResult = RemoveFrog(0, (2, 3));

            Assert.True(moveResult.IsError);
            Assert.Equal("TurnManager.FrogRemovalIsNotPossible", moveResult.Errors[0].Code);
        }

        [Fact]
        public void ProcessPlayerMove_InvalidPositionsPassed()
        {
            board.SetFrog(1, 1, 1);
            board.SetFrog(1, 2, 2);
            board.SetFrog(2, 3, 1);

            // неправильный ход
            var moveResult = ProcessMove(0, [(2, 3), (0, 3)]);

            Assert.True(moveResult.IsError);
            Assert.Equal("TurnManager.InvalidMovePositions", moveResult.Errors[0].Code);
        }

        [Fact]
        public void ProcessPlayerMove_SwampFrogsRemoved()
        {
            board.SetFrog(1, 3, 2);
            board.SetFrog(2, 3, 1);

            var moveResult = ProcessMove(0, [(2, 3), (0, 3)]);

            Assert.True(moveResult.IsSuccess);
            Assert.True(board.IsEmptyCell(0, 3));
        }

        [Fact]
        public void ProcessPlayerMove_NextPlayerIsCorrect()
        {
            board.SetFrog(1, 1, 1);
            board.SetFrog(1, 2, 2);
            board.SetFrog(2, 3, 2);
            board.SetFrog(1, 4, 1);

            var moveResult = ProcessMove(0, [(1, 1), (1, 3)]);

            Assert.True(moveResult.IsSuccess);
            Assert.Equal(1, moveResult.Value.nextPlayer);

            moveResult = ProcessMove(1, [(2, 3), (0, 3)]);

            Assert.True(moveResult.IsSuccess);
            Assert.Equal(0, moveResult.Value.nextPlayer);

            board.SetFrog(1, 1, 1);
            board.SetFrog(1, 2, 1);
            board.SetFrog(0, 3, 2);
            board.SetFrog(0, 4, 1);

            // после этого пропуск хода у второго игрока
            moveResult = ProcessMove(0, [(0, 4), (0, 2)]);

            Assert.True(moveResult.IsSuccess);
            Assert.False(moveResult.Value.gameFinished);
            Assert.Equal(0, moveResult.Value.nextPlayer);
        }

        [Fact]
        public void ProcessPlayerMove_GameFinishedAndWinnerAreCorrect()
        {
            board.SetFrog(1, 4, 1);
            board.SetFrog(1, 1, 1);
            board.SetFrog(1, 2, 2);

            var moveResult = turnManager.ProcessPlayerMove(0, [(1, 1), (1, 3)], true, (1, 4));

            Assert.True(moveResult.IsSuccess);
            Assert.True(moveResult.Value.gameFinished);
            Assert.Equal(0, moveResult.Value.winner);
        }

        [Fact]
        public void ProcessPlayerMove_PassingMoveIsPossible()
        {
            board.SetFrog(1, 4, 1);
            board.SetFrog(2, 4, 2);
            board.SetFrog(1, 1, 1);
            board.SetFrog(1, 2, 2);

            var moveResult = ProcessMove(0, []);

            Assert.True(moveResult.IsSuccess);
            Assert.Equal(1, moveResult.Value.nextPlayer);
        }
    }
}
