namespace RentService.Models.VehicleModule;

public class Vehicle
{
   public int ID { get;  private set; }
   public string Model { get; set; }
   public string RegistrationNumber { get; set; }
   public DateTime YearOfManufacture { get; set; }
   public string Color { get; set; }
   public string VIN { get; set; }
   public decimal DailyRentalPrice { get; set; }
   public bool IsAvailable { get; set; }
   public float Mileage { get; set; }
   public string Notes { get; set; }
   
   public ICollection<ExploitationPart> ExploitationParts { get; set; }
}