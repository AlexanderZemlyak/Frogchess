namespace Frogchess.Models.Dtos
{
    public struct MoveRequest
    {
        public string PlayerId { get; set; }
        public Position[] MovePositions { get; set; }
        public bool RemoveFrog { get; set; }
        public Position? FrogToRemove { get; set; }
    }
}
