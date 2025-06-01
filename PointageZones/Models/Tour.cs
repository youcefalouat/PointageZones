using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PointageZones.Models
{
    
    public class Tour : IValidatableObject
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public required string RefTour { get; set; }
        [Required]
        public required ZoneType Type { get; set; }

        public string? Observation { get; set; }

        // Ajout des champs pour gérer les horaires de la tournée
        public TimeOnly DebTour { get; set; }  // Heure de début de la tournée
        public TimeOnly? FinTour { get; set; }  // Heure de fin (peut être null)
        public int? FrqTourMin { get; set; }    // Fréquence en minutes (ex: 30 min pour une tournée toutes les 30 min)


        public ICollection<PlanTour> PlanTours { get; set; } = new List<PlanTour>();


        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (FinTour != null && FrqTourMin == null)
            {
                yield return new ValidationResult(
                    "La fréquence doit être renseignée si l'heure de fin est renseignée.",
                    new[] { nameof(FrqTourMin) });
            }
            if (FrqTourMin != null && FinTour == null)
            {
                yield return new ValidationResult(
                    "L'heure de fin doit être renseignée si la fréquence est renseignée.",
                    new[] { nameof(FinTour) });
            }
        }
    }
}
