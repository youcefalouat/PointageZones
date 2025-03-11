using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using PointageZones.Models;

namespace PointageZones.Areas.Identity.Data;

// Add profile data for application users by adding properties to the User class
public class User : IdentityUser
{
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty ;
    public bool IsChecking { get; set; } = false;

    public bool? IsLocked { get; set; }  = false ;
    public ICollection<PlanTour> PlanTour {  get; set; } = new List<PlanTour>();
    public ICollection<PointageAgent> Pointages { get; set; } = new List<PointageAgent>();

}

