public class Player
{
    public string PlayerId { get; set; }
    public string Name { get; set; }
    public string Color { get; set; } 
    public Player(string playerId, string name)
    {
        PlayerId = playerId;
        Name = name;
        Color = "";
    }
}