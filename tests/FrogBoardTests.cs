using Frogchess.Domain;
using MathNet.Numerics.Distributions;

namespace Frogchess.UnitTests.Domain
{
    public class FrogBoardTests
    {
        private readonly FrogBoard frogBoard;

        private const int WIDTH = 8;
        private const int HEIGHT = 8;
        private const int FROGTYPES = 2;

        public FrogBoardTests()
        {
            frogBoard = new FrogBoard(WIDTH, HEIGHT, FROGTYPES);
        }

        [Fact]
        public void PositionFrogs_OccupiesAllWhiteCells()
        {
            frogBoard.PositionFrogs();

            // заняты все белые клетки
            for (int x = 1; x < HEIGHT - 1; x++)
            {
                for (int y = 1; y < WIDTH - 1; y++)
                {
                    Assert.False(frogBoard.IsEmptyCell(x, y));
                }
            }

            // болото свободно
            for (int y = 0; y < WIDTH; y++)
            {
                Assert.True(frogBoard.IsEmptyCell(0, y));
                Assert.True(frogBoard.IsEmptyCell(HEIGHT - 1, y));
            }

            for (int x = 0; x < HEIGHT; x++)
            {
                Assert.True(frogBoard.IsEmptyCell(x, 0));
                Assert.True(frogBoard.IsEmptyCell(x, WIDTH - 1));
            }
        }

        [Fact]
        public void PositionFrogs_EqualFrogTypes()
        {
            frogBoard.PositionFrogs();

            int frogTypeAmount = (WIDTH - 2) * (HEIGHT - 2) / FROGTYPES;

            for (int i = 1; i <= FROGTYPES; i++)
            {
                int count = 0;

                for (int x = 1; x < HEIGHT - 1; x++)
                {
                    for (int y = 1; y < WIDTH - 1; y++)
                    {
                        if (frogBoard.GetFrog(x, y) == i)
                            count++;
                    }
                }

                Assert.Equal(frogTypeAmount, count);
            }
        }

        [Fact]
        // проверка статистической значимости различий расстановок для случая двух типов лягушек
        public void PositionFrogs_StatisticalSignificanceOfDifferences()
        {
            Assert.Equal(2, FROGTYPES);

            int significantCount = 0;
            int totalTests = 100;

            for (int test = 0; test < totalTests; test++)
            {
                var frogBoard1 = new FrogBoard(WIDTH, HEIGHT, FROGTYPES);
                var frogBoard2 = new FrogBoard(WIDTH, HEIGHT, FROGTYPES);
                frogBoard1.PositionFrogs();
                frogBoard2.PositionFrogs();

                int a = 0, b = 0, c = 0, d = 0;

                for (int x = 1; x < HEIGHT - 1; x++)
                {
                    for (int y = 1; y < WIDTH - 1; y++)
                    {
                        int val1 = frogBoard1.GetFrog(x, y);
                        int val2 = frogBoard2.GetFrog(x, y);

                        if (val1 == 1 && val2 == 1) a++;
                        else if (val1 == 1 && val2 == 2) b++;
                        else if (val1 == 2 && val2 == 1) c++;
                        else if (val1 == 2 && val2 == 2) d++;
                    }
                }

                int totalCells = (WIDTH - 2) * (HEIGHT - 2);

                int n1 = a + b; // кол-во 1-го типа на 1-ой доске
                int n2 = c + d; // кол-во 2-го типа на 1-ой доске
                int m1 = a + c; // кол-во 1-го типа на 2-ой доске
                int m2 = b + d; // кол-во 2-го типа на 2-ой доске
                int N = a + b + c + d;
                Assert.Equal(N, totalCells);

                double chiSquare = 0;

                // ожидаемые частоты событий при независимости расстановок
                double E_a = (n1 * m1) / (double)N;
                double E_b = (n1 * m2) / (double)N;
                double E_c = (n2 * m1) / (double)N;
                double E_d = (n2 * m2) / (double)N;

                if (E_a > 0) chiSquare += Math.Pow(a - E_a, 2) / E_a;
                if (E_b > 0) chiSquare += Math.Pow(b - E_b, 2) / E_b;
                if (E_c > 0) chiSquare += Math.Pow(c - E_c, 2) / E_c;
                if (E_d > 0) chiSquare += Math.Pow(d - E_d, 2) / E_d;

                int df = 1;

                double pValue = 1 - ChiSquared.CDF(df, chiSquare);

                // маленькое - слишком много похожих или различающихся
                if (pValue < 0.05) significantCount++;
            }

            // тест на равномерное распределение p-value
            double expected = totalTests * 0.05;
            double stdDev = Math.Sqrt(totalTests * 0.05 * 0.95);
            double zScore = (significantCount - expected) / stdDev;

            Assert.True(Math.Abs(zScore) < 3.0,
                $"z-score = {zScore:F2}, значимых различий: {significantCount}/{totalTests}");
        }

        [Theory]
        // прыжок вправо
        [InlineData(1, 1, 1, 1, 2, 1, 1, 3)]
        // прыжок вверх в болото
        [InlineData(2, 1, 1, 1, 1, 2, 0, 1)]
        // прыжок по диагонали
        [InlineData(1, 1, 1, 2, 2, 2, 3, 3)]
        public void IsSingleMoveValid_ReturnTrue(int x1, int y1, int frogType1, int x2, int y2, int frogType2, int x3, int y3)
        {
            frogBoard.SetFrog(x1, y1, frogType1);
            frogBoard.SetFrog(x2, y2, frogType2);
            Assert.True(frogBoard.IsSingleMoveValid(x1, y1, x3, y3));
        }

        [Fact]
        public void IsSingleMoveValid_ReturnFalseNoFrogs()
        {
            // нет двух лягушек
            Assert.False(frogBoard.IsSingleMoveValid(0, 0, 0, 1));
            Assert.False(frogBoard.IsSingleMoveValid(0, 0, 0, 2));

            // нет одной лягушки
            frogBoard.SetFrog(1, 1, 1);
            Assert.False(frogBoard.IsSingleMoveValid(1, 1, 1, 3));
            Assert.False(frogBoard.IsSingleMoveValid(1, 2, 1, 0));
            frogBoard.SetFrog(1, 2, 1);
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, 1, 2));
        }

        [Fact]
        public void IsSingleMoveValid_ReturnFalseCellOccupied()
        {
            frogBoard.SetFrog(1, 0, 1);
            frogBoard.SetFrog(1, 1, 1);
            frogBoard.SetFrog(1, 2, 1);
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, 1, 1));
        }

        [Fact]
        public void IsSingleMoveValid_ReturnFalseWrongDistance()
        {
            frogBoard.SetFrog(1, 0, 1);
            frogBoard.SetFrog(1, 1, 1);
            // Слишком далеко
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, 1, 3));
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, 1, 4));

            // Слишком близко
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, 1, 0));
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, 0, 0));
        }

        [Fact]
        public void IsSingleMoveValid_ReturnFalseOutsideBorders()
        {
            frogBoard.SetFrog(1, 0, 1);
            frogBoard.SetFrog(1, 1, 1);
            Assert.False(frogBoard.IsSingleMoveValid(1, 1, 1, -1));

            frogBoard.SetFrog(0, 1, 1);
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, -1, 2));

            frogBoard.SetFrog(0, WIDTH - 2, 1);
            frogBoard.SetFrog(0, WIDTH - 1, 2);
            Assert.False(frogBoard.IsSingleMoveValid(0, WIDTH - 2, 0, WIDTH));
        }

        [Fact]
        public void IsSingleMoveValid_ReturnFalseWrongDirection()
        {
            frogBoard.SetFrog(1, 0, 1);
            frogBoard.SetFrog(2, 1, 1);
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, 2, 2));
            Assert.False(frogBoard.IsSingleMoveValid(1, 0, 2, 0));
        }

        [Fact]
        public void MakeMove_ReturnTrue()
        {
            // один прыжок
            frogBoard.SetFrog(1, 1, 1);
            frogBoard.SetFrog(1, 2, 1);
            
            Assert.True(frogBoard.MakeMove((1, 1), (1, 3)));
            Assert.Equal(0, frogBoard.GetFrog(1, 1));
            Assert.Equal(0, frogBoard.GetFrog(1, 2));
            Assert.Equal(1, frogBoard.GetFrog(1, 3));

            // тройной прыжок
            frogBoard.RemoveFrog(1, 3);
            frogBoard.SetFrog(1, 1, 1);
            frogBoard.SetFrog(1, 2, 1);
            frogBoard.SetFrog(2, 3, 1);
            frogBoard.SetFrog(4, 4, 1);

            Assert.True(frogBoard.MakeMove((1, 1), (1, 3), (3, 3), (5, 5)));
            Assert.Equal(0, frogBoard.GetFrog(1, 1));
            Assert.Equal(0, frogBoard.GetFrog(1, 2));
            Assert.Equal(0, frogBoard.GetFrog(2, 3));
            Assert.Equal(0, frogBoard.GetFrog(4, 4));
            Assert.Equal(1, frogBoard.GetFrog(5, 5));
        }

        [Fact]
        public void MakeMove_ReturnFalse()
        {
            // на первом прыжке
            frogBoard.SetFrog(1, 1, 1);
            frogBoard.SetFrog(1, 2, 1);
            frogBoard.SetFrog(1, 3, 1);
            Assert.False(frogBoard.MakeMove((1, 1), (1, 3)));

            // на втором прыжке
            frogBoard.RemoveFrog(1, 3);
            frogBoard.SetFrog(2, 3, 1);
            frogBoard.SetFrog(3, 3, 1);
            Assert.True(frogBoard.MakeMove((1, 1), (1, 3)));
            frogBoard.RemoveFrog(1, 3);
            frogBoard.SetFrog(1, 1, 1);
            frogBoard.SetFrog(1, 2, 1);
            Assert.False(frogBoard.MakeMove((1, 1), (1, 3), (3, 3)));
        }

        [Fact]
        public void HasMoves_ReturnTrue()
        {
            // Один доступен
            frogBoard.SetFrog(1, 1, 1);
            frogBoard.SetFrog(1, 2, 1);
            Assert.True(frogBoard.HasMoves(1, 1));

            // Два доступно
            frogBoard.SetFrog(2, 1, 1);
            Assert.True(frogBoard.HasMoves(1, 1));
        }

        [Fact]
        public void HasMoves_ReturnFalse()
        {
            frogBoard.SetFrog(1, 1, 1);
            Assert.False(frogBoard.HasMoves(1, 1));

            frogBoard.SetFrog(0, 1, 1);
            frogBoard.SetFrog(1, 3, 1);
            Assert.False(frogBoard.HasMoves(1, 1));
        }

        [Fact]
        public void RemoveSwampFrogs_RemovesAll()
        {
            frogBoard.SetFrog(0, 0, 1);
            frogBoard.SetFrog(0, 1, 1);
            frogBoard.SetFrog(HEIGHT - 1, 0, 1);
            frogBoard.SetFrog(0, WIDTH - 1, 1);
            frogBoard.SetFrog(HEIGHT - 1, WIDTH - 1, 2);

            frogBoard.RemoveSwampFrogs();

            Assert.Equal(0, frogBoard.GetFrog(0, 0));
            Assert.Equal(0, frogBoard.GetFrog(0, 1));
            Assert.Equal(0, frogBoard.GetFrog(HEIGHT - 1, 0));
            Assert.Equal(0, frogBoard.GetFrog(0, WIDTH - 1));
            Assert.Equal(0, frogBoard.GetFrog(HEIGHT - 1, WIDTH - 1));
        }
    }
}

