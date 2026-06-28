const clientState = {
    connection: null,
    gameId: null,
    playerName: null,
    playerOrder: null,
    opponentName: null,
    playerId: null,
    color: null,
    board: null
};

async function connectToServer() {
    clientState.connection = new signalR.HubConnectionBuilder()
        .withUrl("/gamehub")
        .withAutomaticReconnect()
        .build();

    clientState.connection.on("Subscribed", (data) => {
        console.log("Subscribed to game", data.gameId);
    });

    clientState.connection.on("OpponentJoined", (data) => {
        console.log("Opponent joined:", data.opponentName, data.color);
        clientState.opponentName = data.opponentName;
        boardState.isMyTurn = !data.opponent;
        updateStatus({
            state: 'your-turn',
            turn: '🐸 YOUR TURN',
            players: [
                { name: clientState.playerName, active: true, color: clientState.color },
                { name: data.opponentName, active: false, color: matchColor(data.color) }
            ],
            msg: '🗑 Click a frog to remove'
        });
    });

    clientState.connection.on("OpponentMoved", async (data) => {
        console.log("Opponent moved:", data);

        const moveResult = data.moveResult;

        // Противник пропустил ход - показываем уведомление
        if (!moveResult.gameFinished && moveResult.movePositions.length === 0 && !moveResult.frogWasRemoved) {
            showPassNotification(clientState.opponentName, false);
        }

        // Мы пропускаем ход - уведомление
        if (moveResult.nextPlayerId !== clientState.playerId) {
            showPassNotification(clientState.playerName, true);
        }

        updateStatusAfterMove(moveResult);

        // обновляем все согласно ходу противника
        const movePositions = moveResult.movePositions.map(p => ({ row: p.x, col: p.y }));

        if (moveResult.frogWasRemoved)
            removeFrogFromBoard(moveResult.removedFrog.x, moveResult.removedFrog.y);

        await applyMove(movePositions, calculateCaptures(movePositions));
    });

    clientState.connection.on("OpponentDisconnected", async () => {
        console.log("Opponent disconnected");

        showOpponentDisconnected(clientState.opponentName);
    });

    clientState.connection.on("OpponentReconnected", async () => {
        console.log("Opponent reconnected");

        showOpponentReconnected(clientState.opponentName);
    });

    clientState.connection.on("OpponentReconnectTimeout", async () => {
        console.log("Timeout on opponent reconnection");

        showGameOverNotification(clientState.playerName, true);
    });

    await clientState.connection.start();
    console.log("SignalR connected");
}

async function joinGame(playerName, gameId = null) {
    const response = await fetch("/api/games/join", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ playerName, gameId })
    });

    if (!response.ok) {
        const err = await response.json();
        throw new Error(err.error[0].description || "Failed to join game");
    }

    const data = await response.json();
    clientState.gameId = data.gameId;
    clientState.playerId = data.playerId;
    clientState.playerName = playerName;
    clientState.opponentName = data.opponent;
    clientState.playerOrder = data.order;
        
    clientState.color = matchColor(data.color);
    clientState.board = data.board;

    boardState.isFirstMove = data.isFirstMove;
    boardState.isMyTurn = data.isPlayerMove;

    await clientState.connection.invoke("SubscribeToGame", data.gameId, data.playerId);

    const cells = boardArrayToCells(data.board);
    if (window.setBoardState) {
        window.setBoardState(cells);
    }

    return data;
}

async function makeMove(movePositions, removeFrog = false, frogToRemove = null) {
    const response = await fetch(`/api/games/${clientState.gameId}/move`, {
        method: "PATCH",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
            playerId: clientState.playerId,
            movePositions,
            removeFrog,
            frogToRemove
        })
    });

    if (!response.ok) {
        const err = await response.json();
        throw new Error(err.error[0].description || "Failed to move");
    }

    return await response.json();
}

function matchColor(colorIndex) {
    return colorIndex === 0 ? 'red' : 'green';
}

function boardArrayToCells(boardArray) {
    const cells = [];
    for (let row = 0; row < boardArray.length; row++) {
        for (let col = 0; col < boardArray[row].length; col++) {
            const value = boardArray[row][col];
            if (value !== 0) {
                cells.push({
                    col: col,
                    row: row,
                    color: value === 1 ? 'red' : 'green'
                });
            }
        }
    }
    return cells;
}