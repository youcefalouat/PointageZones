using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointageZones.Models
{
    public class PlanTour
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int TourId { get; set; }
        public required Tour Tour { get; set; }
        public int ZoneId { get; set; }
        public required Zone Zone { get; set; }
        public ICollection<PointageAgent> Pointages { get; set; } = new List<PointageAgent>();

    }
}
