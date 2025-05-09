using System;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;

public class ClientHandler
{
    public TcpClient TcpClient { get; }
    private NetworkStream _stream;
    private StreamReader _reader;
    private StreamWriter _writer;
    private GameServer _server;
    public string PlayerId { get; private set; }

    public ClientHandler(TcpClient tcpClient, GameServer server)
    {
        TcpClient = tcpClient;
        _server = server;
        _stream = tcpClient.GetStream();
        _reader = new StreamReader(_stream, Encoding.UTF8);
        _writer = new StreamWriter(_stream, Encoding.UTF8) { AutoFlush = true };
    }

    public async Task HandleClientAsync()
    {
        Console.WriteLine($"Client connected: {TcpClient.Client.RemoteEndPoint}");
        try
        {
            while (TcpClient.Connected)
            {
                string jsonMessage = await _reader.ReadLineAsync();
                if (string.IsNullOrEmpty(jsonMessage))
                {
                    break;
                }

                Console.WriteLine($"Received from {PlayerId ?? "new client"}: {jsonMessage}");
                BaseMessage baseMessage = JsonSerializer.Deserialize<BaseMessage>(jsonMessage);

                if (baseMessage != null)
                {
                    await ProcessMessageAsync(baseMessage);
                }
            }
        }
        catch (IOException ex)
        {
            Console.WriteLine($"IO Exception with client {PlayerId ?? TcpClient.Client.RemoteEndPoint?.ToString()}: {ex.Message}. Client likely disconnected.");
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"Invalid JSON received from {PlayerId ?? TcpClient.Client.RemoteEndPoint?.ToString()}: {jsonEx.Message}");
            await SendErrorAsync("Invalid JSON format.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling client {PlayerId ?? TcpClient.Client.RemoteEndPoint?.ToString()}: {ex.Message}");
        }
        finally
        {
            _server.RemoveClient(this);
            Console.WriteLine($"Client {PlayerId ?? TcpClient.Client.RemoteEndPoint?.ToString()} disconnected.");
            TcpClient.Close();
        }
    }

    private async Task ProcessMessageAsync(BaseMessage baseMessage)
    {
        if (string.IsNullOrEmpty(PlayerId) && baseMessage.Type != MessageType.ConnectRequest)
        {
            await SendErrorAsync("Client not authenticated. Please send ConnectRequest first.");
            return;
        }

        switch (baseMessage.Type)
        {
            case MessageType.ConnectRequest:
                var connectPayload = JsonSerializer.Deserialize<ConnectRequestPayload>(baseMessage.Payload ?? "{}");
                if (connectPayload != null)
                {
                    PlayerId = Guid.NewGuid().ToString();
                    var player = new Player(PlayerId, connectPayload.PlayerName);
                    _server.GameState.AddPlayer(player);

                    int playerIndex = _server.GameState.Players.Count % _server.AvailableColors.Length;
                    player.Color = _server.AvailableColors[playerIndex];

                    Console.WriteLine($"Player {player.Name} (ID: {PlayerId}) connected.");
                    var response = new ConnectResponsePayload
                    {
                        Status = "success",
                        PlayerId = PlayerId,
                        Message = "Connected successfully!"
                    };
                    await SendMessageAsync(MessageType.ConnectResponse, response);

                    _server.CheckAndStartGame();
                }
                break;

            case MessageType.SendUnits:
                var sendUnitsPayload = JsonSerializer.Deserialize<SendUnitsRequestPayload>(baseMessage.Payload ?? "{}");
                if (sendUnitsPayload != null && PlayerId != null)
                {
                    Console.WriteLine($"Player {PlayerId} wants to send {sendUnitsPayload.Percentage}% units from {sendUnitsPayload.FromPlanetId} to {sendUnitsPayload.ToPlanetId}");
                    Planet fromPlanet = _server.GameState.GetPlanet(sendUnitsPayload.FromPlanetId);
                    Planet toPlanet = _server.GameState.GetPlanet(sendUnitsPayload.ToPlanetId);

                    if (fromPlanet != null && toPlanet != null && fromPlanet.OwnerId == PlayerId && fromPlanet.Units > 0)
                    {
                        int unitsToSend = (int)(fromPlanet.Units * (sendUnitsPayload.Percentage / 100.0));
                        if (unitsToSend > 0)
                        {
                            fromPlanet.Units -= unitsToSend;

                            var fleet = new Fleet(PlayerId, fromPlanet.PlanetId, toPlanet.PlanetId, unitsToSend);
                            double distance = Math.Sqrt(Math.Pow(toPlanet.X - fromPlanet.X, 2) + Math.Pow(toPlanet.Y - fromPlanet.Y, 2));
                            fleet.EstimatedArrivalTime = DateTime.UtcNow.AddSeconds(distance / 50.0);
                            _server.GameState.Fleets.Add(fleet);

                            var fleetLaunchedData = new FleetLaunchedPayload
                            {
                                FleetId = fleet.FleetId,
                                OwnerId = fleet.OwnerId,
                                FromPlanetId = fleet.FromPlanetId,
                                ToPlanetId = fleet.ToPlanetId,
                                UnitCount = fleet.UnitCount,
                                StartTime = fleet.LaunchTime,
                                EstimatedArrivalTime = fleet.EstimatedArrivalTime
                            };
                            await _server.BroadcastMessageAsync(MessageType.FleetLaunched, fleetLaunchedData);

                            var planetUpdateData = new PlanetUpdatePayload
                            {
                                Updates = new List<PlanetData> { ConvertToPlanetData(fromPlanet) }
                            };
                            await _server.BroadcastMessageAsync(MessageType.PlanetUpdate, planetUpdateData);
                        }
                    }
                    else
                    {
                        await SendErrorAsync("Invalid SendUnits request.");
                    }
                }
                break;
            default:
                Console.WriteLine($"Received unhandled message type: {baseMessage.Type}");
                await SendErrorAsync($"Unhandled message type: {baseMessage.Type}");
                break;
        }
    }

    public async Task SendMessageAsync<T>(MessageType type, T payload)
    {
        try
        {
            string payloadJson = JsonSerializer.Serialize(payload);
            var baseMessage = new BaseMessage { Type = type, Payload = payloadJson };
            string messageJson = JsonSerializer.Serialize(baseMessage);
            await _writer.WriteLineAsync(messageJson);
            Console.WriteLine($"Sent to {PlayerId ?? "client"}: {messageJson}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending message to {PlayerId}: {ex.Message}");
        }
    }

    public async Task SendErrorAsync(string errorMessage)
    {
        var errorPayload = new ErrorPayload { Message = errorMessage };
        await SendMessageAsync(MessageType.Error, errorPayload);
    }

    public static PlanetData ConvertToPlanetData(Planet planet)
    {
        return new PlanetData
        {
            PlanetId = planet.PlanetId,
            X = planet.X,
            Y = planet.Y,
            Size = planet.Size,
            OwnerId = planet.OwnerId,
            Units = planet.Units,
            ProductionRate = planet.ProductionRate
        };
    }
}