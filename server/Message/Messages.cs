using System.Collections.Generic;
using System;
using System.Text.Json.Serialization;

public class BaseMessage
{
    public MessageType Type { get; set; }
    public string Payload { get; set; } 
}

public class ConnectRequestPayload
{
    [JsonPropertyName("playerName")]
    public string PlayerName { get; set; }
}

public class ConnectResponsePayload
{
    [JsonPropertyName("status")]
    public string Status { get; set; }
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; }
    [JsonPropertyName("message")]
    public string Message { get; set; }
}

public class GameStartPayload
{
    [JsonPropertyName("map")]
    public MapData Map { get; set; }
    [JsonPropertyName("players")]
    public List<PlayerData> Players { get; set; }
}

public class MapData
{
    [JsonPropertyName("width")]
    public int Width { get; set; } = 800;
    [JsonPropertyName("height")]
    public int Height { get; set; } = 600;
    [JsonPropertyName("planets")]
    public List<PlanetData> Planets { get; set; }
}

public class PlanetData
{
    [JsonPropertyName("planetId")]
    public int PlanetId { get; set; }
    [JsonPropertyName("x")]
    public int X { get; set; }
    [JsonPropertyName("y")]
    public int Y { get; set; }
    [JsonPropertyName("size")]
    public int Size { get; set; }
    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; }
    [JsonPropertyName("units")]
    public int Units { get; set; }
    [JsonPropertyName("productionRate")]
    public int ProductionRate { get; set; }
}

public class PlayerData
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; }
    [JsonPropertyName("name")]
    public string Name { get; set; }
    [JsonPropertyName("color")]
    public string Color { get; set; }
}


public class SendUnitsRequestPayload
{
    [JsonPropertyName("fromPlanetId")]
    public int FromPlanetId { get; set; }
    [JsonPropertyName("toPlanetId")]
    public int ToPlanetId { get; set; }
    [JsonPropertyName("percentage")]
    public int Percentage { get; set; }
}


public class PlanetUpdatePayload
{
    [JsonPropertyName("updates")]
    public List<PlanetData> Updates { get; set; }
}

public class FleetLaunchedPayload
{
    [JsonPropertyName("fleetId")]
    public string FleetId { get; set; }
    [JsonPropertyName("ownerId")]
    public string OwnerId { get; set; }
    [JsonPropertyName("fromPlanetId")]
    public int FromPlanetId { get; set; }
    [JsonPropertyName("toPlanetId")]
    public int ToPlanetId { get; set; }
    [JsonPropertyName("unitCount")]
    public int UnitCount { get; set; }
    [JsonPropertyName("startTime")]
    public DateTime StartTime { get; set; }
    [JsonPropertyName("estimatedArrivalTime")]
    public DateTime EstimatedArrivalTime { get; set; }
}

public class GameOverPayload
{
    [JsonPropertyName("winnerId")]
    public string WinnerId { get; set; }
    [JsonPropertyName("reason")]
    public string Reason { get; set; }
}

public class PlayerDisconnectedPayload
{
    [JsonPropertyName("playerId")]
    public string PlayerId { get; set; }
}

public class ErrorPayload
{
    [JsonPropertyName("message")]
    public string Message { get; set; }
}