namespace PointageZones.DTO
{
    public class TourDuJourViewModel
    {
        public int TourId { get; set; }
        public string? TourRefTour { get; set; }
        public DateTime? Date { get; set; }
        public int NumeroTour { get; set; }
        public DateTime debTour {  get; set; }
        public DateTime finTour { get; set; }
        public DateTime? debPointage { get; set; }
        public DateTime? finPointage { get; set; }
        public bool tourFait { get; set; }
        public int? ZonesRequises { get; set; }
        public int? ZonesPointees { get; set; }
        public bool TourAssigné { get;set; }
        public string? userId { get; set; }
        public string? observation { get; set; }
    }



}
