using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class GameServer
{
    private TcpListener _listener;
    private List<ClientHandler> _clients = new List<ClientHandler>();
    private readonly object _clientsLock = new object();
    public GameState GameState { get; private set; } = new GameState();
    private Timer _gameLoopTimer;
    private const int MinPlayersToStart = 2;

    public readonly string[] AvailableColors = { "red", "blue", "green", "yellow", "purple", "orange" };


    public GameServer(string ipAddress, int port)
    {
        _listener = new TcpListener(IPAddress.Parse(ipAddress), port);
    }

    public async Task StartAsync()
    {
        _listener.Start();
        Console.WriteLine($"Server started on {_listener.LocalEndpoint}...");
        Console.WriteLine("Waiting for players...");

        _gameLoopTimer = new Timer(GameLoopTick, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));

        try
        {
            while (true)
            {
                TcpClient tcpClient = await _listener.AcceptTcpClientAsync();
                var clientHandler = new ClientHandler(tcpClient, this);
                lock (_clientsLock)
                {
                    _clients.Add(clientHandler);
                }
                _ = clientHandler.HandleClientAsync();
            }
        }
        catch (SocketException ex)
        {
            Console.WriteLine($"SocketException: {ex.Message}");
        }
        finally
        {
            _listener.Stop();
            _gameLoopTimer?.Dispose();
        }
    }

    public void CheckAndStartGame()
    {
        lock (_clientsLock)
        {
            if (GameState.CurrentStatus == GameStatus.WaitingForPlayers && GameState.Players.Count >= MinPlayersToStart)
            {
                Console.WriteLine("Minimum players reached. Starting game...");
                GameState.CurrentStatus = GameStatus.InProgress;

                var unownedPlanets = GameState.Planets.Values.Where(p => p.OwnerId == null).ToList();
                int playerIdx = 0;
                foreach (var player in GameState.Players.Values)
                {
                    if (unownedPlanets.Count > playerIdx)
                    {
                        var startingPlanet = GameState.Planets.Values.FirstOrDefault(p => p.PlanetId == playerIdx + 1);
                        if (startingPlanet != null)
                        {
                            startingPlanet.OwnerId = player.PlayerId;
                            startingPlanet.Units = 50;
                        }
                    }
                    playerIdx++;
                }


                var gameStartPayload = new GameStartPayload
                {
                    Map = new MapData
                    {
                        Planets = GameState.Planets.Values.Select(ClientHandler.ConvertToPlanetData).ToList()
                    },
                    Players = GameState.Players.Values.Select(p => new PlayerData { PlayerId = p.PlayerId, Name = p.Name, Color = p.Color }).ToList()
                };
                _ = Task.Run(async () => await BroadcastMessageAsync(MessageType.GameStart, gameStartPayload));
            }
        }
    }


    private void GameLoopTick(object state)
    {
        if (GameState.CurrentStatus != GameStatus.InProgress) return;

        var planetUpdates = new List<PlanetData>();
        var fleetsToRemove = new List<Fleet>();
        string winnerId = null;

        foreach (var planet in GameState.Planets.Values)
        {
            if (planet.OwnerId != null)
            {
                planet.Units += planet.ProductionRate;
                planetUpdates.Add(ClientHandler.ConvertToPlanetData(planet));
            }
        }

        foreach (var fleet in GameState.Fleets)
        {
            if (DateTime.UtcNow >= fleet.EstimatedArrivalTime)
            {
                fleetsToRemove.Add(fleet);
                Planet targetPlanet = GameState.GetPlanet(fleet.ToPlanetId);
                if (targetPlanet != null)
                {
                    Console.WriteLine($"Fleet {fleet.FleetId} arrived at planet {targetPlanet.PlanetId}");
                    if (targetPlanet.OwnerId == fleet.OwnerId)
                    {
                        targetPlanet.Units += fleet.UnitCount;
                    }
                    else
                    {
                        if (fleet.UnitCount > targetPlanet.Units)
                        {
                            targetPlanet.Units = fleet.UnitCount - targetPlanet.Units;
                            targetPlanet.OwnerId = fleet.OwnerId;
                        }
                        else
                        {
                            targetPlanet.Units -= fleet.UnitCount;
                            if (targetPlanet.Units < 0) targetPlanet.Units = 0;
                        }
                    }
                    planetUpdates.Add(ClientHandler.ConvertToPlanetData(targetPlanet));
                }
            }
        }

        foreach (var fleetToRemove in fleetsToRemove)
        {
            GameState.Fleets.Remove(fleetToRemove);
        }

        if (planetUpdates.Any())
        {
            var payload = new PlanetUpdatePayload { Updates = planetUpdates.GroupBy(p => p.PlanetId).Select(g => g.First()).ToList()};
            _ = Task.Run(async () => await BroadcastMessageAsync(MessageType.PlanetUpdate, payload));
        }

        var activePlayers = GameState.Planets.Values
                                .Where(p => p.OwnerId != null)
                                .Select(p => p.OwnerId)
                                .Union(GameState.Fleets.Select(f => f.OwnerId))
                                .Distinct()
                                .ToList();

        if (GameState.Players.Count > 0 && activePlayers.Count == 1 && GameState.CurrentStatus == GameStatus.InProgress)
        {
            winnerId = activePlayers.First();
            GameState.CurrentStatus = GameStatus.Finished;
            var gameOverPayload = new GameOverPayload { WinnerId = winnerId, Reason = $"Player {GameState.GetPlayer(winnerId)?.Name} has conquered the galaxy!" };
            Console.WriteLine($"Game Over! Winner: {winnerId}");
            _ = Task.Run(async () => await BroadcastMessageAsync(MessageType.GameOver, gameOverPayload));
            _gameLoopTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        else if (GameState.Players.Count > 0 && !activePlayers.Any() && GameState.CurrentStatus == GameStatus.InProgress)
        {
            GameState.CurrentStatus = GameStatus.Finished;
            var gameOverPayload = new GameOverPayload { WinnerId = null, Reason = "All players have been eliminated. Stalemate!" };
            Console.WriteLine($"Game Over! Stalemate!");
            _ = Task.Run(async () => await BroadcastMessageAsync(MessageType.GameOver, gameOverPayload));
            _gameLoopTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }


    public void RemoveClient(ClientHandler clientHandler)
    {
        lock (_clientsLock)
        {
            _clients.Remove(clientHandler);
            if (clientHandler.PlayerId != null)
            {
                var disconnectPayload = new PlayerDisconnectedPayload { PlayerId = clientHandler.PlayerId };
                _ = Task.Run(async () => await BroadcastMessageAsync(MessageType.PlayerDisconnected, disconnectPayload, clientHandler));
            }
        }
    }

    public async Task BroadcastMessageAsync<T>(MessageType type, T payload, ClientHandler excludeClient = null)
    {
        List<ClientHandler> clientsToSend;
        lock (_clientsLock)
        {
            clientsToSend = _clients.ToList();
        }

        foreach (var client in clientsToSend)
        {
            if (client != excludeClient && client.TcpClient.Connected && client.PlayerId != null)
            {
                await client.SendMessageAsync(type, payload);
            }
        }
    }
}