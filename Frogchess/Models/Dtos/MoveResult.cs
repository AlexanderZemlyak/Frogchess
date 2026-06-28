namespace Frogchess.Models.Dtos
{
    public struct MoveResult
    {
        public Position[] MovePositions { get; set; }
        public bool FrogWasRemoved { get; set; }
        public Position? RemovedFrog { get; set; }
        public string NextPlayerId { get; set; }
        public bool GameFinished { get; set; }
        public string? WinnerId { get; set; }
    }

}
