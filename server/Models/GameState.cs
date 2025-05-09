using System.Collections.Generic;
using System.Linq;

public class GameState
{
    public Dictionary<string, Player> Players { get; set; } = new Dictionary<string, Player>();
    public Dictionary<int, Planet> Planets { get; set; } = new Dictionary<int, Planet>();
    public List<Fleet> Fleets { get; set; } = new List<Fleet>();
    public GameStatus CurrentStatus { get; set; } = GameStatus.WaitingForPlayers;

    public GameState()
    {
        InitializeDefaultMap();
    }

    private void InitializeDefaultMap()
    {
        Planets.Add(1, new Planet(1, 100, 100, 30, 50, 5));
        Planets.Add(2, new Planet(2, 700, 500, 20, 50, 3));
        Planets.Add(3, new Planet(3, 400, 300, 15, 10, 2));
    }

    public void AddPlayer(Player player)
    {
        if (!Players.ContainsKey(player.PlayerId))
        {
            Players.Add(player.PlayerId, player);
        }
    }

    public Player GetPlayer(string playerId)
    {
        Players.TryGetValue(playerId, out var player);
        return player;
    }

    public Planet GetPlanet(int planetId)
    {
        Planets.TryGetValue(planetId, out var planet);
        return planet;
    }
}

public enum GameStatus
{
    WaitingForPlayers,
    InProgress,
    Finished
}