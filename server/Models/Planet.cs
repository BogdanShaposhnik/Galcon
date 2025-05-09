public class Planet
{
    public int PlanetId { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Size { get; set; }
    public string OwnerId { get; set; }
    public int Units { get; set; }
    public int ProductionRate { get; set; }

    public Planet(int planetId, int x, int y, int size, int initialUnits, int productionRate, string ownerId = null)
    {
        PlanetId = planetId;
        X = x;
        Y = y;
        Size = size;
        Units = initialUnits;
        ProductionRate = productionRate;
        OwnerId = ownerId;
    }
}