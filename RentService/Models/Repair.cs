using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using RentService.Models.VehicleModule;

namespace RentService.Models
{
    public class Repair
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "ID pojazdu jest wymagane")]
        public int VehicleID { get; set; }

        // Właściwość nawigacyjna do pojazdu
        [ValidateNever]
        public Vehicle Vehicle { get; set; }

        [Required(ErrorMessage = "Data naprawy jest wymagana")]
        [DataType(DataType.Date)]
        [MaxDate(ErrorMessage = "Data naprawy nie może być z przyszłości")]
        public DateTime RepairDate { get; set; }

        [Required(ErrorMessage = "Przebieg w momencie naprawy jest wymagany")]
        [Range(0, int.MaxValue, ErrorMessage = "Przebieg nie może być ujemny")]
        public int MileageAtRepair { get; set; }

        [Required(ErrorMessage = "Opis naprawy jest wymagany")]
        [StringLength(1000, MinimumLength = 5, ErrorMessage = "Opis naprawy musi mieć od 5 do 1000 znaków")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Koszt naprawy jest wymagany")]
        [Range(0.01, 999999.99, ErrorMessage = "Koszt naprawy musi być większy od 0")]
        [Column(TypeName = "decimal(10,2)")]
        public decimal Cost { get; set; }

        [Required(ErrorMessage = "Typ naprawy jest wymagany")]
        [EnumDataType(typeof(RepairType), ErrorMessage = "Wybierz prawidłowy typ naprawy")]
        public RepairType RepairType { get; set; }

        [Required(ErrorMessage = "Status naprawy jest wymagany")]
        [EnumDataType(typeof(RepairStatus), ErrorMessage = "Wybierz prawidłowy status naprawy")]
        public RepairStatus Status { get; set; }

        [StringLength(500, ErrorMessage = "Dodatkowe uwagi nie mogą przekraczać 500 znaków")]
        public string? AdditionalNotes { get; set; }

        // Data rozpoczęcia naprawy (opcjonalna - może być różna od RepairDate)
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        // Data zakończenia naprawy (opcjonalna)
        [DataType(DataType.Date)]
        [DateGreaterThan("StartDate", ErrorMessage = "Data zakończenia musi być późniejsza niż data rozpoczęcia")]
        public DateTime? CompletionDate { get; set; }

        // Numer faktury/rachunku
        [StringLength(50, ErrorMessage = "Numer faktury nie może przekraczać 50 znaków")]
        public string? InvoiceNumber { get; set; }

        // Relacja many-to-many z częściami, które były naprawiane/wymieniane
        public ICollection<RepairExploitationPart> RepairParts { get; set; } = new List<RepairExploitationPart>();
    }
    
    public class RepairExploitationPart
    {
        public int ID { get; set; }
        public int RepairID { get; set; }
        public Repair Repair { get; set; }

        public int ExploitationPartID { get; set; }
        public ExploitationPart ExploitationPart { get; set; }

        [Required(ErrorMessage = "Akcja na części jest wymagana")]
        [EnumDataType(typeof(PartAction), ErrorMessage = "Wybierz prawidłową akcję")]
        public PartAction Action { get; set; }

        [StringLength(200, ErrorMessage = "Uwag" +
                                          "`i o części nie mogą przekraczać 200 znaków")]
        public string PartNotes { get; set; }
        
        public DateTime NextServiceCheck { get; set; }
        public int NextMillageCheck { get; set; }
    }

    public enum RepairType
    {
        [Display(Name = "Naprawa awaryjna")]
        Emergency,
        
        [Display(Name = "Przegląd okresowy")]
        Maintenance,
        
        [Display(Name = "Wymiana części")]
        PartReplacement,
        
        [Display(Name = "Inne")]
        Other
    }

    public enum RepairPriority
    {
        [Display(Name = "Niski")]
        Low,
        
        [Display(Name = "Średni")]
        Medium,
        
        [Display(Name = "Wysoki")]
        High,
        
        [Display(Name = "Krytyczny")]
        Critical
    }

    public enum RepairStatus
    {
        [Display(Name = "Zaplanowana")]
        Scheduled,
        
        [Display(Name = "W trakcie")]
        InProgress,
        
        [Display(Name = "Zakończona")]
        Completed,
        
        [Display(Name = "Anulowana")]
        Cancelled,
        
        [Display(Name = "Oczekuje na części")]
        WaitingForParts,
    }

    public enum PartAction
    {
        [Display(Name = "Wymieniono")]
        Replaced,
        
        [Display(Name = "Naprawiono")]
        Repaired,
        
        [Display(Name = "Sprawdzono")]
        Inspected,
    }
}