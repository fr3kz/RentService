using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace RentService.Area.EmployeModule.Models
{
    public class Employee
    {
        public int EmployeeId { get; set; }
        [BindNever]
        public string? UserId { get; set; }

        
        // Dane osobowe
        [Required(ErrorMessage = "Imię jest wymagane")]
        [Display(Name = "Imię")]
        public string FirstName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Nazwisko jest wymagane")]
        [Display(Name = "Nazwisko")]
        public string LastName { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "PESEL jest wymagany")]
        [StringLength(11, MinimumLength = 11, ErrorMessage = "PESEL musi mieć 11 cyfr")]
        [Display(Name = "PESEL")]
        public string Pesel { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Data urodzenia jest wymagana")]
        [Display(Name = "Data urodzenia")]
        public DateTime BirthDate { get; set; }
        
        [Display(Name = "Miejsce urodzenia")]
        public string? BirthPlace { get; set; }
        
        [Required(ErrorMessage = "Płeć jest wymagana")]
        [Display(Name = "Płeć")]
        public string Gender { get; set; } = string.Empty;
        
        [Display(Name = "Obywatelstwo")]
        public string? Nationality { get; set; } = "Polskie";
        
        [Display(Name = "Wykształcenie")]
        public string? Education { get; set; }
        
        // Kontakt
        
        [EmailAddress(ErrorMessage = "Nieprawidłowy format email")]
        [Display(Name = "Email prywatny")]
        public string? PersonalEmail { get; set; }
        
        [Phone(ErrorMessage = "Nieprawidłowy numer telefonu")]
        [Display(Name = "Telefon prywatny")]
        public string? PersonalPhone { get; set; }
        
        // Adres
        [Required(ErrorMessage = "Ulica jest wymagana")]
        [Display(Name = "Ulica i numer")]
        public string Street { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Kod pocztowy jest wymagany")]
        [Display(Name = "Kod pocztowy")]
        public string PostalCode { get; set; } = string.Empty;
        
        [Required(ErrorMessage = "Miasto jest wymagane")]
        [Display(Name = "Miasto")]
        public string City { get; set; } = string.Empty;
        
        [Display(Name = "Kraj")]
        public string? Country { get; set; } = "Polska";
        [Required(ErrorMessage = "Data zatrudnienia jest wymagana")]
        [Display(Name = "Data zatrudnienia")]
        public DateTime HireDate { get; set; }
        
        [Required(ErrorMessage = "Typ umowy jest wymagany")]
        [Display(Name = "Typ umowy")]
        public string ContractType { get; set; } = string.Empty;
        
     
        [Display(Name = "Uwagi")]
        public string? Notes { get; set; }
        
    
        // Pliki
        public string? ImagePath { get; set; }
        public string? ContractPath { get; set; }
        public string? IdCopyPath { get; set; }
        public string? OtherDocumentsPath { get; set; }
        
        // Metadane
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
    
    // ViewModel dla formularza z plikami
    public class EmployeeCreateViewModel
    {
        public Employee Employee { get; set; } = new Employee();
        
        [Display(Name = "Zdjęcie pracownika")]
        public IFormFile? Photo { get; set; }
        
        [Display(Name = "CV")]
        public IFormFile? Cv { get; set; }
        
        [Display(Name = "Umowa o pracę")]
        public IFormFile? Contract { get; set; }
        
        [Display(Name = "Kopia dowodu osobistego")]
        public IFormFile? IdCopy { get; set; }
        
        [Display(Name = "Inne dokumenty")]
        public List<IFormFile>? OtherDocuments { get; set; }
    }
}