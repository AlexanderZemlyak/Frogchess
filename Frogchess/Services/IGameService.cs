using ErrorOr;
using Frogchess.Models.Dtos;

namespace Frogchess.Services
{
    public interface IGameService
    {
        void RegisterConnection(string gameId, string playerId, string connectionId);
        Task HandleDisconnect(string connectionId);
        Task<ErrorOr<JoinResult>> JoinOrCreateGameAsync(string? gameId, string playerName);
        Task<ErrorOr<MoveResult>> ProcessMoveAsync(string gameId, MoveRequest moveRequest);
    }
}
