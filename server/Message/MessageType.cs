public enum MessageType
{
    ConnectRequest,
    ConnectResponse,
    GameStart,
    SendUnits,
    PlanetUpdate,
    FleetLaunched,
    FleetArrived, // Можливо, інтегровано в PlanetUpdate
    GameOver,
    Error,
    PlayerDisconnected
    // Додайте інші типи за потреби
}