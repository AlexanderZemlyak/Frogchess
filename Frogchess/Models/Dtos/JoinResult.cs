namespace Frogchess.Models.Dtos
{
    public struct JoinResult
    {
        public Guid GameId { get; set; }
        public Guid PlayerId { get; set; }
        public Color Color { get; set; }
        public int Order { get; set; }
        public string? Opponent { get; set; }
        public int[][] Board { get; set; }
        public bool IsFirstMove { get; set; }
        public bool IsPlayerMove { get; set; }
    }
}
