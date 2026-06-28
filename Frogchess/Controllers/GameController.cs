using ErrorOr;
using Frogchess.Models.Dtos;
using Frogchess.Services;
using Microsoft.AspNetCore.Mvc;

namespace Frogchess.Controllers
{
    /// <summary>
    /// Http контроллер игры Frog Chess. Поддерживает создание игры, присоединение к игре и ход в игре.
    /// </summary>
    [ApiController]
    [Route("api/games")]
    public class GameController : ControllerBase
    {
        private readonly IGameService gameService;

        public GameController(IGameService gameService)
        {
            this.gameService = gameService;
        }

        /// <summary>
        /// Создать новую игру или присоединиться к существующей.
        /// Для создания новой нужно передать request.GameId = null.
        /// </summary>
        [HttpPost("join")]
        public async Task<IActionResult> JoinGame([FromBody] JoinRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.PlayerName))
                return BadRequest(new { error = "Player name is required" });

            if (request.PlayerName.Length > 16)
                return BadRequest(new { error = "Player name max 16 characters" });

            var result = await gameService.JoinOrCreateGameAsync(
                request.GameId,
                request.PlayerName
            );

            return result.Match<IActionResult>(
                success => Ok(new
                {
                    gameId = success.GameId,
                    playerId = success.PlayerId,
                    color = success.Color,
                    order = success.Order,
                    opponent = success.Opponent,
                    board = success.Board,
                    isFirstMove = success.IsFirstMove,
                    isPlayerMove = success.IsPlayerMove
                }),
                error => BadRequest(new { error })
            );
        }

        /// <summary>
        /// Сделать ход в игре с id = gameId. Вернет информацию о следующем игроке и о том, выявлен ли победитель.
        /// </summary>
        [HttpPatch("{gameId}/move")]
        public async Task<IActionResult> MakeMove(string gameId, [FromBody] MoveRequest request)
        {
            var moveResult = await gameService.ProcessMoveAsync(gameId, request);

            return moveResult.Match<IActionResult>(
                success => Ok(new
                {
                    nextPlayerId = success.NextPlayerId,
                    gameFinished = success.GameFinished,
                    winnerId = success.WinnerId
                }),
                error => BadRequest(new { error })
            );
        }
    }
}
