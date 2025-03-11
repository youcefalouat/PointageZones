namespace PointageZones.DTO
{
    public class ScanQRCodeDTO
    {
        public required string qrCodeText { get; set; }
        public int PlanTourId { get; set; }
        public DateTime? datetimescan { get; set; }
        public int PointageId { get; set; }
    }
}
