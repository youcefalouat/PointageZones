using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace PointageZones.Models
{
    public class Observation
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [StringLength(255)]
        public required string Description { get; set; }

        // Navigation property for PointageAgents with this observation
        public ICollection<PointageAgent>? PointageAgents { get; set; }
    }
}