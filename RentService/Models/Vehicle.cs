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
   public float Mileage { get; set; }
   public string Notes { get; set; }
   
   public ICollection<ExploitationPart> ExploitationParts { get; set; }
   public ICollection<Repair> Repairs { get; set; }

}