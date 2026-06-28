using System.Text;

namespace Frogchess.Domain
{
    /// <summary>
    /// Доска для игры Frog Chess
    /// </summary>
    public sealed class FrogBoard
    {
        private readonly (int x, int y)[] moveDirections = [(-2, -2), (-2, 0), (-2, 2),
                                                        ( 0, -2), ( 0, 2),
                                                        ( 2, -2), ( 2, 0), ( 2, 2)];

        private int Width { get; set; }
        private int Height { get; set; }
        private int FrogTypesCount { get; set; }
        
        private readonly int[,] board;

        private static readonly Random rnd = new Random();

        public FrogBoard(int width, int height, int frogTypesCount)
        {
            this.Width = width;
            this.Height = height;
            this.FrogTypesCount = frogTypesCount;

            this.board = new int[height, width];
        }

        /// <summary>
        /// Принадлежат ли координаты позиции доске
        /// </summary>
        public bool IsValidPosition(int x, int y) => x >= 0 && y >= 0 && x < Height && y < Width;

        /// <summary>
        /// Пуста ли клетки доски
        /// </summary>
        public bool IsEmptyCell(int x, int y) => board[x, y] == 0;

        /// <summary>
        /// Возвращает список пустых клеток основного поля (исключая болото)
        /// </summary>
        private List<(int x, int y)> GetEmptyWhiteCells()
        {
            List<(int, int)> positions = [];

            for (int i = 1; i < Height - 1; i++)
            {
                for (int j = 1; j < Width - 1; j++)
                {
                    if (IsEmptyCell(i, j))
                        positions.Add((i, j));
                }
            }

            return positions;
        }

        /// <summary>
        /// Случайным образом размещает лягушек на доске. Используется тасование Фишера.
        /// </summary>
        public void PositionFrogs()
        {

            var allPositions = GetEmptyWhiteCells();

            int frogsPerType = allPositions.Count / FrogTypesCount;
            
            var types = new List<int>();
            for (int t = 1; t <= FrogTypesCount; t++)
                for (int i = 0; i < frogsPerType; i++)
                    types.Add(t);

            // тасуем типы
            for (int i = types.Count - 1; i > 0; i--)
            {
                int j = rnd.Next(0, i + 1);
                (types[i], types[j]) = (types[j], types[i]);
            }

            for (int i = 0; i < allPositions.Count; i++)
            {
                var (x, y) = allPositions[i];
                board[x, y] = types[i];
            }
        }

        /// <summary>
        /// Возможен ли ход с (x1, y1) на (x2, y2)
        /// </summary>
        public bool IsSingleMoveValid(int x1, int y1, int x2, int y2)
        {
            if (!IsValidPosition(x1, y1) || !IsValidPosition(x2, y2))
                return false;

            if (IsEmptyCell(x1, y1))
                return false;

            if (!IsEmptyCell(x2, y2))
                return false;

            int deltaX = Math.Abs(x1 - x2);
            int deltaY = Math.Abs(y1 - y2);

            int[] validDelta = [0, 2];

            if (!validDelta.Contains(deltaX) || !validDelta.Contains(deltaY))
                return false;

            if (deltaX == 0 && deltaY == 0)
                return false;

            (int victimX, int victimY) = (x1 + (x2 - x1) / 2, y1 + (y2 - y1) / 2);

            if (IsEmptyCell(victimX, victimY))
                return false;

            return true;
        }

        /// <summary>
        /// Сделать ход с (x1, y1) на (x2, y2)
        /// </summary>
        private bool MakeSingleMove(int x1, int y1, int x2, int y2)
        {
            if (!IsSingleMoveValid(x1, y1, x2, y2))
                return false;

            int frogType = GetFrog(x1, y1);
            RemoveFrog(x1, y1);
            
            (int victimX, int victimY) = (x1 + (x2 - x1) / 2, y1 + (y2 - y1) / 2);
            RemoveFrog(victimX, victimY);

            SetFrog(x2, y2, frogType);

            return true;
        }

        /// <summary>
        /// Возвращает матрицу, соответствующую размещению лягушек на доске
        /// </summary>
        public int[,] GetBoardData() => board;

        /// <summary>
        /// Задать лягушку для клетки (x, y)
        /// </summary>
        public void SetFrog(int x, int y, int frogType)
        {
            board[x, y] = frogType;
        }

        /// <summary>
        /// Получить лягушку на клетке (x, y)
        /// </summary>
        public int GetFrog(int x, int y)
        {
            return board[x, y];
        }

        /// <summary>
        /// Удалить лягушку с клетки (x, y)
        /// </summary>
        public void RemoveFrog(int x, int y)
        {
            board[x, y] = 0;
        }

        /// <summary>
        /// Сделать цепочечный ход из прыжков, заданных массивом позиций
        /// </summary>
        public bool MakeMove(params (int x, int y)[] movePositions)
        {
            if (movePositions.Length < 2)
                return false;

            for (int i = 0; i < movePositions.Length - 1; i++)
            {
                (int x1, int y1) = movePositions[i];
                (int x2, int y2) = movePositions[i + 1];

                if (!MakeSingleMove(x1, y1, x2, y2))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Имеет ли допустимые ходы лягушка на (x, y)
        /// </summary>
        public bool HasMoves(int x, int y)
        {
            if (IsEmptyCell(x, y))
                return false;

            foreach (var moveDirection in moveDirections)
            {
                if (IsSingleMoveValid(x, y, x + moveDirection.x, y + moveDirection.y))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Имеет ли допустимые ходы любая лягушка переданного типа
        /// </summary>
        public bool AnyFrogOfTypeHasMoves(int frogType)
        {
            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    if (GetFrog(i, j) == frogType && HasMoves(i, j))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Очистить лягушек, находящихся на клетках болота
        /// </summary>
        public void RemoveSwampFrogs()
        {
            var removeOperation = (int x, int y) =>
            {
                if (!IsEmptyCell(x, y))
                    RemoveFrog(x, y);
            };

            for (int y = 0; y < Width; y++)
            {
                removeOperation(0, y);
                removeOperation(Height - 1, y);
            }

            for (int x = 0; x < Height; x++)
            {
                removeOperation(x, 0);
                removeOperation(x, Width - 1);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < Height; i++)
            {
                for (int j = 0; j < Width; j++)
                {
                    sb.Append(board[i, j] + " ");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
