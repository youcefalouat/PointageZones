using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointageZones.Models
{
    public enum ZoneType
    {
        Exterieur,
        Interieur
    }

    public class Zone
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public required string RefZone { get; set; }
        public ZoneType? Type { get; set; }
        public string? Tag { get; set; }
        public DateTime? lastUpdate { get; set; }
        //  public decimal? Latitude { get; set; } //Nullable
        //  public decimal? Longitude { get; set; } //Nullable

        public ICollection<PlanTour> PlanTours { get; set; } = new List<PlanTour>();
        

    }
}
