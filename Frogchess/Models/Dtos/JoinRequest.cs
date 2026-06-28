namespace Frogchess.Models.Dtos
{
    public struct JoinRequest
    {
        public string PlayerName { get; set; }
        public string? GameId { get; set; }
    }
}
