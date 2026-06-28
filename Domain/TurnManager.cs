using ErrorOr;

namespace Frogchess.Domain
{
    public struct GameState
    {
        public int[,] board;
        public int nextPlayer;
        public bool gameFinished;
        public int winner;
    }

    /// <summary>
    /// Класс, отвечающий за правила игры Frog Chess. Позволяет делать ходы на доске.
    /// </summary>
    public sealed class TurnManager
    {
        /// <summary>
        /// Индекс текущего игрока
        /// </summary>
        public int CurrentPlayer { get; private set; }
        /// <summary>
        /// Сколько всего ходов уже произведено
        /// </summary>
        public int MovesMade { get; private set; }
        private bool isFirstMoveForPlayer;

        private readonly int playersCount;
        private readonly FrogBoard board;

        public TurnManager(int playersCount, FrogBoard board)
        {
            this.playersCount = playersCount;
            this.CurrentPlayer = 0;
            this.MovesMade = 0;
            this.isFirstMoveForPlayer = true;
            this.board = board;
        }

        /// <summary>
        /// Переход к следующему игроку
        /// </summary>
        private void NextPlayer()
        {
            if (isFirstMoveForPlayer && CurrentPlayer == playersCount - 1)
                isFirstMoveForPlayer = false;

            CurrentPlayer = (CurrentPlayer + 1) % playersCount;

            MovesMade++;
        }

        /// <summary>
        /// Сделать ход с возможностью цепочечных прыжков, а также удаления одной лягушки на первом ходу
        /// </summary>
        public ErrorOr<GameState> ProcessPlayerMove(int playerIndex, (int x, int y)[] movePositions, bool removeFrog, (int x, int y)? frogToRemove)
        {
            if (playerIndex != CurrentPlayer)
                return Error.Unexpected("TurnManager.InvalidPlayerIndex", "Invalid player index passed.");

            if (removeFrog && frogToRemove.HasValue)
            {
                var (x, y) = frogToRemove.Value;

                // Удаление только на первом ходу
                if (board.IsValidPosition(x, y) && isFirstMoveForPlayer)
                    board.RemoveFrog(x, y);
                else
                    return Error.Unexpected("TurnManager.FrogRemovalIsNotPossible", "Frog removal is not possible.");
            }

            if (movePositions.Length > 0)
            {
                if (movePositions.Length == 1 ||
                !board.IsValidPosition(movePositions[0].x, movePositions[0].y) ||
                // попытка хода не своей лягушкой
                playerIndex + 1 != board.GetFrog(movePositions[0].x, movePositions[0].y) ||
                !board.MakeMove(movePositions))
                {
                    return Error.Unexpected("TurnManager.InvalidMovePositions", "Invalid move positions passed.");
                }

                // проверка лягушек в болоте
                board.RemoveSwampFrogs();
            }

            int lastMovePlayer = CurrentPlayer;

            NextPlayer();

            int playersSkipped = 0;

            // у следующего игрока нет ходов
            for (int i = 0; i < playersCount; i++)
            {
                if (!board.AnyFrogOfTypeHasMoves(CurrentPlayer + 1))
                {
                    NextPlayer();
                    playersSkipped++;
                }
                else break;
            }

            return new GameState() {
                board = board.GetBoardData(),
                nextPlayer = CurrentPlayer,
                gameFinished = (playersSkipped == playersCount),
                winner = lastMovePlayer
            };
        }

    }
}
