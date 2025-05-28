using System;
using System.Collections.Generic;
using PointageZones.Areas.Identity.Data;
using PointageZones.Models;

namespace PointageZones.DTO
{
    public class AdminDashboardViewModel
    {
        public List<PointageAgent> Pointages { get; set; }
        public List<UtilisateurPointageDto> NombrePointagesParUtilisateur { get; set; }
        public List<PlanTourneeRatioDto> RatioTournéesParPlan { get; set; }
        public List<TourDuJourViewModel>? tourDuJourViewModels { get; set; } 
        public DateTime DateDebut { get; set; }
        public DateTime DateFin { get; set; }
        public string SelectedUser { get; set; }
        public string SelectedTour { get; set; } 


    }

    public class UtilisateurPointageDto
    {
        public  required string UtilisateurId { get; set; }
        public int Nombre { get; set; }
    }

    public class PlanTourneeRatioDto
    {
        public int PlanTourneeId { get; set; }
        public string? refTour { get; set; }
        public int Effectue { get; set; }
        public int Total { get; set; }
    }
}
