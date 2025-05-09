using System;

public class Fleet
{
    public string FleetId { get; set; }
    public string OwnerId { get; set; }
    public int FromPlanetId { get; set; }
    public int ToPlanetId { get; set; }
    public int UnitCount { get; set; }
    public DateTime LaunchTime { get; set; }
    public DateTime EstimatedArrivalTime { get; set; }

    public Fleet(string ownerId, int fromPlanetId, int toPlanetId, int unitCount)
    {
        FleetId = Guid.NewGuid().ToString();
        OwnerId = ownerId;
        FromPlanetId = fromPlanetId;
        ToPlanetId = toPlanetId;
        UnitCount = unitCount;
        LaunchTime = DateTime.UtcNow;
    }
}