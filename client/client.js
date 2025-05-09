const net = require('net');
const readline = require('readline');

const SERVER_HOST = '127.0.0.1'; 
const SERVER_PORT = 7777;       
let PLAYER_NAME = `NodePlayer${Math.floor(Math.random() * 1000)}`; 

let client = null;
let myPlayerId = null;
let myColor = null;
let incomingDataBuffer = ''; 

let gameState = {
    players: {},
    planets: {},
    fleets: []
};

const MessageType = {
    ConnectRequest: 0,
    ConnectResponse: 1,
    GameStart: 2,
    SendUnits: 3,
    PlanetUpdate: 4,
    FleetLaunched: 5,
    FleetArrived: 6,
    GameOver: 7,
    Error: 8,
    PlayerDisconnected: 9
};

const MessageTypeName = Object.fromEntries(Object.entries(MessageType).map(([key, value]) => [value, key]));


const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
});

function log(message) {
    readline.cursorTo(process.stdout, 0);
    process.stdout.write(`[LOG] ${message}\n`);
    rl.prompt(true);
}

function connectToServer(host, port, playerName) {
    if (client && client.connecting) {
        log("Вже відбувається підключення...");
        return;
    }
    if (client && !client.destroyed) {
        log("Клієнт вже підключено або намагається підключитися. Спочатку відключіться.");
        return;
    }

    log(`Підключення до ${host}:${port} як ${playerName}...`);
    client = new net.Socket();
    PLAYER_NAME = playerName;

    client.connect(port, host, () => {
        log("Підключено до сервера!");
        sendMessage(MessageType.ConnectRequest, { playerName: PLAYER_NAME });
        rl.prompt(true);
    });

    client.on('data', (data) => {
        incomingDataBuffer += data.toString('utf8');
        processBuffer();
    });

    client.on('end', () => {
        log("Відключено від сервера (сервер закрив з'єднання).");
        resetClientState();
    });

    client.on('close', (hadError) => {
        if (hadError) {
            log("З'єднання закрито через помилку передачі.");
        } else {
            log("З'єднання закрито.");
        }
        resetClientState();
        rl.prompt(true);
    });

    client.on('error', (err) => {
        log(`Помилка з'єднання: ${err.message}`);
        if (client) client.destroy(); 
        resetClientState();
    });
}

function resetClientState() {
    client = null;
    myPlayerId = null;
    myColor = null;
    incomingDataBuffer = '';
    gameState = { players: {}, planets: {}, fleets: [] };
    log("Стан клієнта скинуто.");
}


function processBuffer() {
    let newlineIndex;
    while ((newlineIndex = incomingDataBuffer.indexOf('\n')) >= 0) {
        let jsonMessage = incomingDataBuffer.substring(0, newlineIndex);
        incomingDataBuffer = incomingDataBuffer.substring(newlineIndex + 1);

        jsonMessage = jsonMessage.trim();

        if (jsonMessage) {
            try {
                const baseMessage = JSON.parse(jsonMessage);
                const payload = baseMessage.Payload ? JSON.parse(baseMessage.Payload) : null;
                log(`Parsed: ${MessageTypeName[baseMessage.Type] || 'UnknownType'} - ${JSON.stringify(payload).substring(0, 100)}...`);
                handleServerMessage(baseMessage.Type, payload);
            } catch (error) {
                log(`Помилка обробки JSON: ${error.message}. Отримано для парсингу: [${jsonMessage}]`);
            }
        }
    }
}


function sendMessage(type, payloadObject) {
    if (client && !client.destroyed && client.writable) {
        const message = {
            Type: type,
            Payload: JSON.stringify(payloadObject)
        };
        const jsonMessage = JSON.stringify(message) + '\n';
        client.write(jsonMessage);
        log(`Sent: ${MessageTypeName[type] || 'UnknownType'} - ${JSON.stringify(payloadObject).substring(0,100)}...`);
    } else {
        log("Неможливо відправити: клієнт не підключено або сокет не готовий до запису.");
    }
}

function handleServerMessage(messageType, payload) {
    switch (messageType) {
        case MessageType.ConnectResponse:
            handleConnectResponse(payload);
            break;
        case MessageType.GameStart:
            handleGameStart(payload);
            break;
        case MessageType.PlanetUpdate:
            handlePlanetUpdate(payload);
            break;
        case MessageType.FleetLaunched:
            handleFleetLaunched(payload);
            break;
        case MessageType.GameOver:
            handleGameOver(payload);
            break;
        case MessageType.PlayerDisconnected:
            handlePlayerDisconnected(payload);
            break;
        case MessageType.Error:
            handleError(payload);
            break;
        default:
            log(`Отримано невідомий тип повідомлення: ${messageType}`);
    }
}

function handleConnectResponse(payload) {
    if (payload.status === "success") {
        myPlayerId = payload.playerId;
        log(`Успішно підключено! Мій Player ID: ${myPlayerId}`);
    } else {
        log(`Не вдалося підключитися: ${payload.message}`);
        if (client) client.end();
    }
}

function handleGameStart(payload) {
    gameState.planets = {};
    gameState.players = {};

    if (payload.players) {
        payload.players.forEach(playerData => {
            gameState.players[playerData.playerId] = playerData;
            if (playerData.playerId === myPlayerId) {
                myColor = playerData.color;
                log(`Мій колір: ${myColor}`);
            }
        });
    }
    if (payload.map && payload.map.planets) {
        payload.map.planets.forEach(planetData => {
            gameState.planets[planetData.planetId] = planetData;
        });
    }
    log("Гра почалася! Карта завантажена.");
    displayGameState();
}

function handlePlanetUpdate(payload) {
    if (!payload || !payload.updates) {
        log("Некоректне оновлення планет.");
        return;
    }
    payload.updates.forEach(planetData => {
        gameState.planets[planetData.planetId] = planetData;
    });
    log(`Планети оновлено: ${payload.updates.map(p => p.planetId).join(', ')}`);
}

function handleFleetLaunched(payload) {
    gameState.fleets.push(payload);
    log(`Флот ${payload.fleetId} запущено від ${payload.ownerId.substring(0,6)}... з планети ${payload.fromPlanetId} до ${payload.toPlanetId} (${payload.unitCount} юнітів).`);
}

function handleGameOver(payload) {
    log(`ГРУ ЗАВЕРШЕНО! Переможець: ${payload.winnerId ? (gameState.players[payload.winnerId]?.name || payload.winnerId.substring(0,6)) : 'Нічия'}. Причина: ${payload.reason}`);
    if (client) client.end();
}

function handlePlayerDisconnected(payload) {
    if (payload && payload.playerId) {
        const playerName = gameState.players[payload.playerId]?.name || payload.playerId.substring(0,6);
        log(`Гравець ${playerName} відключився.`);
        if (gameState.players[payload.playerId]) {
            delete gameState.players[payload.playerId];
        }
    }
}

function handleError(payload) {
    log(`ПОМИЛКА ВІД СЕРВЕРА: ${payload.message}`);
}

function displayGameState() {
    log("\n--- Поточний стан гри ---");
    if (myPlayerId) {
        log(`Мій ID: ${myPlayerId.substring(0,6)}..., Колір: ${myColor || 'N/A'}`);
    }
    log("Гравці:");
    for (const pId in gameState.players) {
        const p = gameState.players[pId];
        log(`  - ${p.name} (ID: ${pId.substring(0,6)}..., Колір: ${p.color})`);
    }
    log("Планети:");
    for (const planetId in gameState.planets) {
        const p = gameState.planets[planetId];
        const ownerName = p.ownerId ? (gameState.players[p.ownerId]?.name || p.ownerId.substring(0,6)) : "Нейтральна";
        log(`  ID: ${p.planetId}, Власник: ${ownerName}, Юніти: ${p.units} (+${p.productionRate}) Розмір: ${p.size} @(${p.x},${p.y})`);
    }
    log("-------------------------\n");
}

async function processUserCommands() {
    rl.setPrompt(`${PLAYER_NAME}> `);
    rl.prompt();

    for await (const line of rl) {
        const parts = line.trim().split(' ');
        const command = parts[0].toLowerCase();

        try {
            switch (command) {
                case 'connect':
                    const name = parts[1] || PLAYER_NAME;
                    connectToServer(SERVER_HOST, SERVER_PORT, name);
                    break;
                case 'disconnect':
                    if (client) client.end();
                    else log("Клієнт не підключено.");
                    break;
                case 'send':
                    if (parts.length < 4) {
                        log("Використання: send <from_id> <to_id> <percentage>");
                    } else {
                        const fromId = parseInt(parts[1]);
                        const toId = parseInt(parts[2]);
                        const percentage = parseInt(parts[3]);
                        if (isNaN(fromId) || isNaN(toId) || isNaN(percentage) || percentage < 1 || percentage > 100) {
                            log("Некоректні параметри для 'send'.");
                        } else {
                            sendMessage(MessageType.SendUnits, {
                                fromPlanetId: fromId,
                                toPlanetId: toId,
                                percentage: percentage
                            });
                        }
                    }
                    break;
                case 'status':
                    displayGameState();
                    break;
                case 'name':
                    if (parts.length > 1) {
                        PLAYER_NAME = parts[1];
                        log(`Ім'я гравця змінено на: ${PLAYER_NAME}. Перепідключіться, щоб ім'я оновилось на сервері.`);
                        rl.setPrompt(`${PLAYER_NAME}> `);
                    } else {
                        log(`Поточне ім'я: ${PLAYER_NAME}`);
                    }
                    break;
                case 'help':
                    log("Доступні команди:");
                    log("  connect [ім'я_гравця] - підключитися до сервера");
                    log("  disconnect            - відключитися від сервера");
                    log("  send <id_звідки> <id_куди> <відсоток> - відправити юніти");
                    log("  status                - показати поточний стан гри");
                    log("  name [нове_ім'я]      - встановити/показати ім'я гравця (локально)");
                    log("  exit                  - вийти з клієнта");
                    log("  help                  - показати це повідомлення");
                    break;
                case 'exit':
                    if (client) client.end();
                    rl.close();
                    return;
                default:
                    if (command) log(`Невідома команда: ${command}. Введіть 'help' для списку команд.`);
            }
        } catch (e) {
            log(`Помилка виконання команди: ${e.message}`);
        }
        rl.prompt();
    }
}

log("Клієнт Galcon на Node.js. Введіть 'help' для списку команд.");
processUserCommands();