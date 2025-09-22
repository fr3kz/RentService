using RentService.Models.VehicleModule;

namespace RentService.Models;

public class Vehicle
{
   public Vehicle()
   {
      ExploitationParts = new List<ExploitationPart>();
      Repairs = new List<Repair>();
   }
   public int ID { get;  set; }
   public string Model { get; set; }
   public string RegistrationNumber { get; set; }
   public DateTime YearOfManufacture { get; set; }
   public string Color { get; set; }
   public string VIN { get; set; }
   public decimal WeekRentalPrice { get; set; }
   public bool IsAvailable { get; set; }
   public float MileageForm { get; set; }

   public float Mileage
   {
      get
      {
         if (MileageHistory == null || !MileageHistory.Any())
            return 0; 
         return MileageHistory
            .OrderByDescending(m => m.CreatedAt)
            .First().Mileage;
      }
      set
      {
         MileageForm = value;
      }
   }

   public ICollection<VehicleMileage>? MileageHistory { get; set; }

   public string? Notes { get; set; }
   
   public ICollection<ExploitationPart> ExploitationParts { get; set; }
   public ICollection<Repair> Repairs { get; set; }

}

public class VehicleMileage{
   public int ID { get; set; }
   public DateTime CreatedAt { get; set; }
   public int Mileage { get; set; }
   public int CarID { get; set; }
   public Vehicle Car { get; set; }
   public MileageAddEnum Type { get; set; }
}

public enum MileageAddEnum
{
   Serwis,
   API
}