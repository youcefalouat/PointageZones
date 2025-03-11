namespace PointageZones.DTO
{
    public class checkTourPointee
    {
            public bool success { get; set; }
            public string? message { get; set; }
            public string? currentTourTime { get; set; }
            public string? nextTourTime { get; set; }
            public string? countdown { get; set; }
            public bool tourPointee { get; set; }
            public List<int>? zonePointée { get; set; }
            public bool? tourAssigné { get; set; }

    }
}
