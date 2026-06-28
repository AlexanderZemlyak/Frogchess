const boardState = {
    selectedFrog: null,
    currentValidMoves: [],
    jumpSequence: [],
    capturedFrogs: [],
    isMyTurn: false,
    isFirstMove: true,
    frogToRemove: null,
    hasRemovedFrog: false
};

function setupBoardInteraction(scene) {
    scene.input.on('pointerdown', (pointer) => {
        if (!boardState.isMyTurn) return;

        const CELL = 65;
        const TOTAL = 8;
        const BOARD_PX = TOTAL * CELL;
        const OX = (520 - BOARD_PX) / 2;
        const OY = (520 - BOARD_PX) / 2;

        const row = Math.floor((pointer.y - OY) / CELL);
        const col = Math.floor((pointer.x - OX) / CELL);

        if (row < 0 || row > 7 || col < 0 || col > 7) {
            clearSelection();
            return;
        }

        handleCellClick(row, col);
    });
}

async function handleCellClick(row, col) {

    const clickedFrog = getFrogAt(row, col);

    // Удаление лягушки
    if (boardState.isFirstMove && !boardState.hasRemovedFrog) {
        if (clickedFrog) {
            selectFrogToRemove(row, col);
        }
        return;
    }

    // Проверяем, кликнули ли по валидному ходу
    const isValidMove = boardState.currentValidMoves.some(m => m.row === row && m.col === col);

    if (isValidMove && boardState.selectedFrog) {

        const from = boardState.selectedFrog;

        await animateJump(from, { row: row, col: col });

        const captureRow = from.row + (row - from.row) / 2;
        const captureCol = from.col + (col - from.col) / 2;
        removeFrogFromBoard(captureRow, captureCol);
        moveFrogOnBoard(from.row, from.col, row, col);

        boardState.selectedFrog = { row: row, col: col };

        // Добавляем прыжок в последовательность
        await addJumpToSequence(from, row, col);

        return;
    }

    // Кликнули по своей лягушке — выбираем её (только если ещё нет последовательности)
    if (clickedFrog && clickedFrog.color === clientState.color && boardState.jumpSequence.length <= 1) {
        selectFrog(row, col);
        return;
    }
}

function selectFrogToRemove(row, col) {
    if (boardState.frogToRemove) {
        const prevKey = `${boardState.frogToRemove.col}_${boardState.frogToRemove.row}`;
        const prevSprite = window._frogSprites?.[prevKey];
        if (prevSprite) prevSprite.clearTint();
    }

    boardState.frogToRemove = { row, col };

    const frogKey = `${col}_${row}`;
    const sprite = window._frogSprites?.[frogKey];
    if (sprite) {
        sprite.setTint(0xff0000);
    }

    const btn = document.getElementById('passBtn');
    if (btn) {
        btn.textContent = '🗑 Remove & Continue';
        btn.classList.add('border-red-500', 'text-red-700', 'hover:bg-red-50');
        btn.onclick = confirmFrogRemoval;
    }
}

function confirmFrogRemoval() {
    if (!boardState.frogToRemove) return;

    boardState.hasRemovedFrog = true;

    removeFrogFromBoard(boardState.frogToRemove.row, boardState.frogToRemove.col);

    const btn = document.getElementById('passBtn');
    if (btn) {
        btn.textContent = '✅ Finish move';
        btn.classList.remove('border-red-500', 'text-red-700', 'hover:bg-red-50');
        btn.onclick = submitMove;
    }

    msgEl.textContent = '✅ Pick a frog to move';
}

function selectFrog(row, col) {
    clearSelection();

    boardState.selectedFrog = { row, col };
    boardState.jumpSequence = []
    boardState.jumpSequence.push(boardState.selectedFrog);

    // Подсвечиваем выбранную лягушку
    const frogKey = `${col}_${row}`;
    const sprite = window._frogSprites?.[frogKey];
    if (sprite) {
        sprite.setTint(0xffff00);
    }

    // Вычисляем только прямые прыжки
    calculateSingleJumps(row, col);
    highlightValidMoves();
}

function calculateSingleJumps(row, col) {
    boardState.currentValidMoves = [];

    const directions = [
        { dr: -2, dc: 0 },
        { dr: 2, dc: 0 },
        { dr: 0, dc: -2 },
        { dr: 0, dc: 2 },
        { dr: 2, dc: 2 },
        { dr: 2, dc: -2 },
        { dr: -2, dc: 2 },
        { dr: -2, dc: -2 }
    ];

    for (const dir of directions) {
        const jumpRow = row + dir.dr;
        const jumpCol = col + dir.dc;
        const middleRow = row + dir.dr / 2;
        const middleCol = col + dir.dc / 2;

        if (!isInBoard(jumpRow, jumpCol)) continue;

        const middleFrog = getFrogAt(middleRow, middleCol);
        const targetEmpty = !getFrogAt(jumpRow, jumpCol);

        if (middleFrog && targetEmpty) {
            boardState.currentValidMoves.push({
                row: jumpRow,
                col: jumpCol
            });
        }
    }
}

async function addJumpToSequence(from, row, col) {
    boardState.jumpSequence.push({ row, col });

    const middleRow = from.row + (row - from.row) / 2;
    const middleCol = from.col + (col - from.col) / 2;
    boardState.capturedFrogs.push({ row: middleRow, col: middleCol });

    // Пересчитываем доступные прыжки с новой позиции
    clearHighlights();
    calculateSingleJumps(row, col);

    if (boardState.currentValidMoves.length > 0) {
        highlightValidMoves();
        highlightFrog(row, col);
    } else {
        // доступных прыжков больше нет - отправляем результат
        boardState.currentValidMoves = [];
        await submitMove();
    }
}

function highlightValidMoves() {

    for (const move of boardState.currentValidMoves) {
        highlightCell(move.row, move.col, 0xffaa00, 0.4);
    }
}

function highlightCell(row, col, color, alpha) {
    const CELL = 65;
    const TOTAL = 8;
    const BOARD_PX = TOTAL * CELL;
    const OX = (520 - BOARD_PX) / 2;
    const OY = (520 - BOARD_PX) / 2;

    const x = OX + (col) * CELL + CELL / 2;
    const y = OY + (row) * CELL + CELL / 2;

    const scene = getScene();
    const highlight = scene.add.circle(x, y, CELL / 2.5, color, alpha);
    highlight.setDepth(1);

    if (!window._highlights) window._highlights = [];
    window._highlights.push(highlight);
}

function highlightFrog(row, col) {
    const frogKey = `${col}_${row}`;
    const sprite = window._frogSprites?.[frogKey];
    if (sprite) {
        sprite.setTint(0xffff00);
    }
}

function clearHighlights() {
    if (boardState.selectedFrog) {
        const frogKey = `${boardState.selectedFrog.col}_${boardState.selectedFrog.row}`;
        const sprite = window._frogSprites?.[frogKey];
        if (sprite) sprite.clearTint();
    }

    if (boardState.frogToRemove && !boardState.hasRemovedFrog) {
        const frogKey = `${boardState.frogToRemove.col}_${boardState.frogToRemove.row}`;
        const sprite = window._frogSprites?.[frogKey];
        if (sprite) sprite.clearTint();
    }

    if (window._highlights) {
        window._highlights.forEach(h => h.destroy());
        window._highlights = [];
    }
}

function clearSelection() {
    clearHighlights();
    boardState.selectedFrog = null;
    boardState.currentValidMoves = [];
    boardState.jumpSequence = [];
    boardState.capturedFrogs = [];
}

async function submitMove() {
    if (!boardState.isMyTurn) return;

    // Для пропуска хода - пустой массив
    const movePositions = [];

    for (const jump of boardState.jumpSequence) {
        movePositions.push({ x: jump.row, y: jump.col });
    }

    const removeFrog = boardState.isFirstMove && boardState.hasRemovedFrog;
    const frogToRemove = removeFrog ? { x: boardState.frogToRemove.row, y: boardState.frogToRemove.col } : null;

    try {
        const result = await makeMove(movePositions, removeFrog, frogToRemove);

        // Очищаем болото
        removeSwampFrogs();

        clearSelection();

        if (boardState.isFirstMove) {
            boardState.isFirstMove = false;
        }

        // Противник пропустил ход - показываем уведомление
        if (!result.gameFinished && result.nextPlayerId === clientState.playerId) {
            showPassNotification(clientState.opponentName, false);
        }

        updateStatusAfterMove(result);

    } catch (err) {
        console.error("Move error:", err);
        clearSelection();
    }
}

function updateStatusAfterMove(moveResult) {

    // Уведомление об окончании игры
    if (moveResult.gameFinished) {

        const isWinner = moveResult.winnerId === clientState.playerId
        const winnerName = isWinner ? clientState.playerName : clientState.opponentName;

        showGameOverNotification(winnerName, isWinner);
        return;
    }

    const isMyMove = clientState.playerId === moveResult.nextPlayerId;

    updateStatusForMove(isMyMove);
}

function updateStatusForMove(isMyMove) {

    const players = [
        { name: clientState.playerName, color: clientState.color },
        { name: clientState.opponentName, color: clientState.color === 'red' ? 'green' : 'red' }
    ];

    if (isMyMove) {
        players[0].active = true;
        players[1].active = false;
    }
    else {
        players[0].active = false;
        players[1].active = true;
    }

    if (clientState.playerOrder !== 0)
        [players[0], players[1]] = [players[1], players[0]]

    if (isMyMove) {
        boardState.isMyTurn = true;

        updateStatus({
            state: 'your-turn',
            turn: '🐸 YOUR TURN',
            players: players,
            msg: boardState.isFirstMove ? '🗑 Click a frog to remove' : '✅ Pick a frog to move'
        });
    }
    else {
        boardState.isMyTurn = false;

        updateStatus({
            state: 'their-turn',
            turn: '⏳ THEIR TURN',
            players: players,
            msg: `⏳ ${clientState.opponentName} is thinking…`
        });
    }
}

function showPassNotification(playerName, isMyPass) {
    if (isMyPass) {
        showNotification({
            emoji: '⏭',
            title: 'Turn Passed (No available moves)',
            autoHide: true,
            duration: 2500
        });
    } else {
        showNotification({
            emoji: '⏭',
            title: `${playerName} passed their turn.`,
            autoHide: true,
            duration: 3000
        });
    }
}

function showGameOverNotification(winnerName, isWinner) {
    showNotification({
        emoji: isWinner ? '🏆' : '😢',
        title: isWinner ? 'Victory!' : 'Defeat!',
        message: isWinner
            ? `Congratulations! You won the game!`
            : `${winnerName} won the game. Better luck next time!`,
        autoHide: false,
        onClose: returnToLobby
    });
}

function showOpponentDisconnected(playerName) {
    showNotification({
        emoji: '🔌',
        title: `${playerName} has disconnected`,
        message: `Waiting 30 seconds for reconnection...`,
        autoHide: true,
        duration: 5000
    });
}

function showOpponentReconnected(playerName) {
    showNotification({
        emoji: '😀',
        title: `${playerName} has reconnected`,
        autoHide: true,
        duration: 2000
    })
}

let notificationQueue = [];
let isNotificationShowing = false;

function showNotification({ emoji = '⏭', title = 'Pass!', message = '', autoHide = true, duration = 3000, onClose = null } = {}) {
    // Добавляем в очередь
    notificationQueue.push({ emoji, title, message, autoHide, duration, onClose });

    // Если сейчас не показывается — запускаем
    if (!isNotificationShowing) {
        processNotificationQueue();
    }
}

function processNotificationQueue() {
    if (notificationQueue.length === 0) {
        isNotificationShowing = false;
        return;
    }

    isNotificationShowing = true;
    const { emoji, title, message, autoHide, duration, onClose } = notificationQueue.shift();

    const toast = document.getElementById('notificationToast');
    const inner = document.getElementById('toastInner');
    const emojiEl = document.getElementById('toastEmoji');
    const titleEl = document.getElementById('toastTitle');
    const messageEl = document.getElementById('toastMessage');
    const dismissBtn = document.getElementById('toastDismiss');

    emojiEl.textContent = emoji;
    titleEl.textContent = title;
    messageEl.textContent = message;

    toast.classList.remove('hidden');
    dismissBtn.style.display = 'block';

    requestAnimationFrame(() => {
        inner.classList.add('toast-show');
    });

    // Закрытие
    const closeAndNext = () => {
        inner.classList.remove('toast-show');
        setTimeout(() => {
            toast.classList.add('hidden');
            if (onClose) onClose();
            processNotificationQueue(); // Показываем следующее
        }, 300);
    };

    dismissBtn.onclick = closeAndNext;

    if (autoHide) {
        setTimeout(closeAndNext, duration);
    }
}

function returnToLobby() {
    clearSelection();
    window.setBoardState([]);

    boardState.selectedFrog = null;
    boardState.currentValidMoves = [];
    boardState.capturedFrogs = [];
    boardState.jumpSequence = [];
    boardState.isFirstMove = true;
    boardState.hasRemovedFrog = false;
    boardState.frogToRemove = null;
    boardState.isMyTurn = false;
    

    document.getElementById('joinModalBackdrop').style.display = 'flex';
    document.getElementById('playerNameInput').value = clientState.playerName;
    document.getElementById('gameIdInput').value = '';

    updateStatus({
        state: 'waiting',
        turn: '🐸 LOBBY',
        players: [{ name: clientState.playerName, active: false, color: 'red' }],
        msg: '🌿 Enter a name & join game'
    });

    clientState.gameId = null;
    clientState.playerId = null;
    clientState.playerName = null;
    clientState.playerOrder = null;
    clientState.color = null;
    clientState.opponentName = null;
    clientState.board = null;
}

function hideAllNotifications() {
    notificationQueue = [];
    const toast = document.getElementById('notificationToast');
    const inner = document.getElementById('toastInner');
    inner.classList.remove('toast-show');
    setTimeout(() => toast.classList.add('hidden'), 300);
    isNotificationShowing = false;
}

function calculateCaptures(movePositions) {
    const captures = [];

    for (let i = 0; i < movePositions.length - 1; i++) {
        const from = movePositions[i];
        const to = movePositions[i + 1];

        const minRow = Math.min(from.row, to.row);
        const minCol = Math.min(from.col, to.col);

        const captureRow = minRow + Math.abs(from.row - to.row) / 2;
        const captureCol = minCol + Math.abs(from.col - to.col) / 2;

        captures.push({ row: captureRow, col: captureCol });
    }

    return captures;
}

async function applyMove(movePositions, captures) {

    for (let i = 0; i < movePositions.length - 1; i++) {

        const from = movePositions[i];
        const to = movePositions[i + 1];

        await animateJump(from, to);

        removeFrogFromBoard(captures[i].row, captures[i].col);

        moveFrogOnBoard(from.row, from.col, to.row, to.col);
    }

    removeSwampFrogs();
}

function removeSwampFrogs() {

    const board = clientState.board;

    for (let col = 0; col < board[0].length; col++) {
        removeFrogFromBoard(0, col);
        removeFrogFromBoard(board.length - 1, col);
    }

    for (let row = 0; row < board.length; row++) {
        removeFrogFromBoard(row, 0);
        removeFrogFromBoard(row, board[0].length - 1);
    }
}

async function animateJump(from, to) {
    const CELL = 65;
    const TOTAL = 8;
    const BOARD_PX = TOTAL * CELL;
    const OX = (520 - BOARD_PX) / 2;
    const OY = (520 - BOARD_PX) / 2;

    const frogKey = `${from.col}_${from.row}`;
    const sprite = window._frogSprites?.[frogKey];
    if (!sprite) return;

    const startX = OX + (from.col) * CELL + CELL / 2;
    const startY = OY + (from.row) * CELL + CELL / 2;
    const endX = OX + (to.col) * CELL + CELL / 2;
    const endY = OY + (to.row) * CELL + CELL / 2;

    return new Promise(resolve => {
        const scene = getScene();
        scene.tweens.add({
            targets: sprite,
            x: endX,
            y: endY,
            duration: 300,
            ease: 'Quad.easeOut',
            onComplete: resolve
        });
    });
}

function getFrogAt(row, col) {
    const board = clientState.board;
    if (!board || !board[row] || board[row][col] === undefined) return null;

    const value = board[row][col];
    if (value === 0) return null;

    return {
        row, col,
        color: value === 1 ? 'red' : 'green'
    };
}

function isInBoard(row, col) {
    return row >= 0 && row <= 7 && col >= 0 && col <= 7;
}

function removeFrogFromBoard(row, col) {
    const frogKey = `${col}_${row}`;
    const sprite = window._frogSprites?.[frogKey];
    if (sprite) {
        // Анимация исчезновения
        const scene = getScene();
        scene.tweens.add({
            targets: sprite,
            alpha: 0,
            scale: 0.5,
            duration: 200,
            onComplete: () => sprite.destroy()
        });
    }

    // Обновляем данные доски
    if (clientState.board) {
        clientState.board[row][col] = 0;
    }
}

function moveFrogOnBoard(fromRow, fromCol, toRow, toCol) {
    const value = clientState.board[fromRow][fromCol];
    clientState.board[fromRow][fromCol] = 0;
    clientState.board[toRow][toCol] = value;

    // Обновляем спрайт
    const frogKey = `${fromCol}_${fromRow}`;
    const sprite = window._frogSprites?.[frogKey];
    if (sprite) {
        delete window._frogSprites[frogKey];
        window._frogSprites[`${toCol}_${toRow}`] = sprite;
    }
}

function getScene() {
    return window.gameScene;
}

window.setBoardState = function (cells, frogSprites) {
    window._frogSprites = frogSprites || {};
};