using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using PointageZones.Areas.Identity.Data;

namespace PointageZones.Models
{
    public class PushSubscription
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public required string Endpoint { get; set; }
        public required string P256DH { get; set; }
        public required string Auth { get; set; }
        public string? UserId { get; set; } // Optional: to associate with a user
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Optional: Track creation time

        // Navigation property (optional but recommended)
         public virtual User User { get; set; } 


        public PushSubscription() { }
        public PushSubscription(string userId, string endpoint, string p256DH, string auth)
        {
            UserId = userId;
            Endpoint = endpoint;
            P256DH = p256DH;
            Auth = auth;
        }

    }
}
