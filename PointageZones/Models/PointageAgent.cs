using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PointageZones.Areas.Identity.Data;

namespace PointageZones.Models
{
    public class PointageAgent
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public required string UserId { get; set; }
        public required User User { get; set; }

        public required int PlanTourId { get; set; }
        public required PlanTour PlanTour { get; set; }
        public int IsChecked { get; set; } = 0;

        public int IsValid { get; set; } = 0;
        public DateTime? DateTimeScan { get; set; } = DateTime.Now;
        public DateTime? DateTimeAssign { get; set; } = DateTime.Now;
        public DateTime? DateTimeDebTour { get; set; } = DateTime.Now;
        public DateTime? DateTimeFinTour { get; set; } = DateTime.Now;
        public int? ObservationId { get; set; } 
        public Observation? Observation { get; set; }
        public string? Ref_User_Assign { get; set; }
        public string? Ref_User_Update { get; set; }
        public DateTime? Last_Update {  get; set; } = DateTime.Now;
        // public decimal? Latitude { get; set; } //Nullable
        // public decimal? Longitude { get; set; } //Nullable

    }
}
