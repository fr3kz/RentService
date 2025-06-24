using Microsoft.AspNetCore.Mvc;
using RentService.Area.EmployeModule.Models;
using System.IO;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentService.Database;
using RentService.Models;

namespace RentService.Controllers;

public class EmployeController : Controller
{
    private readonly ILogger<EmployeController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly UserManager<User> _userManager;
    private readonly AppDbContext _context;

    public EmployeController(
        ILogger<EmployeController> logger, 
        IWebHostEnvironment environment,
        UserManager<User> userManager,
        AppDbContext Context)
    {
        _logger = logger;
        _environment = environment;
        _userManager = userManager;
        _context = Context;
    }

    [HttpGet]
    public async Task<IActionResult> EmployeeList()
    {
        try
        {
            // Dodaj breakpoint tutaj
            var employeesCount = await _context.Employees.CountAsync();
            Console.WriteLine($"Liczba pracowników w bazie: {employeesCount}");
        
            var employees = await _context.Employees
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
            
            Console.WriteLine($"Pobrano pracowników: {employees.Count}");
        
            return View(employees);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Błąd: {ex.Message}");
            return View(new List<Employee>());
        }
    }
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult EmployeeAdd()
    {
        var viewModel = new EmployeeCreateViewModel
        {
            Employee = new Employee
            {
                // Ustawienie domyślnych wartości
                Nationality = "Polskie",
                Country = "Polska",
                HireDate = DateTime.Today,
                CreatedAt = DateTime.Now
            }
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EmployeeAdd(EmployeeCreateViewModel model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                foreach (var key in ModelState.Keys)
                {
                    var state = ModelState[key];
                    foreach (var error in state.Errors)
                    {
                        Console.WriteLine($"{key}: {error.ErrorMessage}");
                    }
                }
                // Jeśli model nie jest prawidłowy, zwróć formularz z błędami
                return View(model);
            }

            // Walidacja PESEL
            if (!IsValidPesel(model.Employee.Pesel))
            {
                ModelState.AddModelError("Employee.Pesel", "PESEL jest nieprawidłowy");
                return View(model);
            }

            // Sprawdzenie czy PESEL już istnieje w bazie
            // TODO: Dodać sprawdzenie w bazie danych
            // if (await _employeeService.PeselExistsAsync(model.Employee.Pesel))
            // {
            //     ModelState.AddModelError("Employee.Pesel", "Pracownik z tym numerem PESEL już istnieje");
            //     return View(model);
            // }

            // Sprawdzenie czy numer pracownika już istnieje
            // TODO: Dodać sprawdzenie w bazie danych
            // if (await _employeeService.EmployeeNumberExistsAsync(model.Employee.EmployeeNumber))
            // {
            //     ModelState.AddModelError("Employee.EmployeeNumber", "Pracownik z tym numerem już istnieje");
            //     return View(model);
            // }

            // Obsługa plików
            var user = new User
            {
                UserName = model.Employee.PersonalEmail,
                Email = model.Employee.PersonalEmail,
                Imie = model.Employee.FirstName,
                Nazwisko = model.Employee.LastName,
                EmailConfirmed = true // lub false, jeśli chcesz wymagać potwierdzenia
            };

// Tymczasowe hasło – później można go zmusić do zmiany hasła przy pierwszym logowaniu
            string defaultPassword = "Pracownik123!"; // możesz wygenerować losowo lub pobrać z formularza

            var result = await _userManager.CreateAsync(user, defaultPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }

// Przypisanie identyfikatora użytkownika do pracownika
            model.Employee.UserId = user.Id;
            
            await HandleFileUploads(model);

            // Ustawienie metadanych
            model.Employee.CreatedAt = DateTime.Now;
            model.Employee.CreatedBy = User.Identity?.Name ?? "System";

            // TODO: Zapisanie do bazy danych
            _context.Employees.Add(model.Employee);
            await _context.SaveChangesAsync();

            // Logowanie
            _logger.LogInformation("Dodano nowego pracownika: {FirstName} {LastName} (PESEL: {Pesel})", 
                model.Employee.FirstName, model.Employee.LastName, model.Employee.Pesel);

            TempData["SuccessMessage"] = $"Pracownik {model.Employee.FirstName} {model.Employee.LastName} został pomyślnie dodany do systemu.";
            
            return RedirectToAction("EmployeeList");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas dodawania pracownika");
            ModelState.AddModelError("", "Wystąpił błąd podczas zapisywania danych. Spróbuj ponownie.");
            return View(model);
        }
    }

    private async Task HandleFileUploads(EmployeeCreateViewModel model)
    {
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "employees");
        Directory.CreateDirectory(uploadsFolder);

        var employeeFolder = Path.Combine(uploadsFolder, model.Employee.UserId);
        Directory.CreateDirectory(employeeFolder);

        // Obsługa zdjęcia pracownika
        if (model.Photo != null && model.Photo.Length > 0)
        {
            if (IsValidImageFile(model.Photo))
            {
                var photoFileName = $"photo_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(model.Photo.FileName)}";
                var photoPath = Path.Combine(employeeFolder, photoFileName);
                
                using (var stream = new FileStream(photoPath, FileMode.Create))
                {
                    await model.Photo.CopyToAsync(stream);
                }
                
                model.Employee.ImagePath = Path.Combine("uploads", "employees", model.Employee.UserId, photoFileName);
            }
            else
            {
                throw new InvalidOperationException("Nieprawidłowy format pliku zdjęcia. Dozwolone formaty: JPG, JPEG, PNG, GIF");
            }
        }

        // Obsługa CV
        if (model.Cv != null && model.Cv.Length > 0)
        {
            if (IsValidDocumentFile(model.Cv))
            {
                var cvFileName = $"cv_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(model.Cv.FileName)}";
                var cvPath = Path.Combine(employeeFolder, cvFileName);
                
                using (var stream = new FileStream(cvPath, FileMode.Create))
                {
                    await model.Cv.CopyToAsync(stream);
                }
            }
            else
            {
                throw new InvalidOperationException("Nieprawidłowy format pliku CV. Dozwolone formaty: PDF, DOC, DOCX");
            }
        }

        // Obsługa umowy
        if (model.Contract != null && model.Contract.Length > 0)
        {
            if (IsValidDocumentFile(model.Contract))
            {
                var contractFileName = $"contract_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(model.Contract.FileName)}";
                var contractPath = Path.Combine(employeeFolder, contractFileName);
                
                using (var stream = new FileStream(contractPath, FileMode.Create))
                {
                    await model.Contract.CopyToAsync(stream);
                }
                
                model.Employee.ContractPath = Path.Combine("uploads", "employees", model.Employee.UserId, contractFileName);
            }
            else
            {
                throw new InvalidOperationException("Nieprawidłowy format pliku umowy. Dozwolone formaty: PDF, DOC, DOCX");
            }
        }

        // Obsługa kopii dowodu
        if (model.IdCopy != null && model.IdCopy.Length > 0)
        {
            if (IsValidImageFile(model.IdCopy) || IsValidDocumentFile(model.IdCopy))
            {
                var idCopyFileName = $"id_copy_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(model.IdCopy.FileName)}";
                var idCopyPath = Path.Combine(employeeFolder, idCopyFileName);
                
                using (var stream = new FileStream(idCopyPath, FileMode.Create))
                {
                    await model.IdCopy.CopyToAsync(stream);
                }
                
                model.Employee.IdCopyPath = Path.Combine("uploads", "employees", model.Employee.UserId, idCopyFileName);
            }
            else
            {
                throw new InvalidOperationException("Nieprawidłowy format pliku kopii dowodu. Dozwolone formaty: JPG, JPEG, PNG, GIF, PDF");
            }
        }

        // Obsługa innych dokumentów
        if (model.OtherDocuments != null && model.OtherDocuments.Any())
        {
            var otherDocumentsPaths = new List<string>();
            
            foreach (var document in model.OtherDocuments)
            {
                if (document.Length > 0)
                {
                    if (IsValidImageFile(document) || IsValidDocumentFile(document))
                    {
                        var docFileName = $"other_{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..8]}{Path.GetExtension(document.FileName)}";
                        var docPath = Path.Combine(employeeFolder, docFileName);
                        
                        using (var stream = new FileStream(docPath, FileMode.Create))
                        {
                            await document.CopyToAsync(stream);
                        }
                        
                        otherDocumentsPaths.Add(Path.Combine("uploads", "employees", model.Employee.UserId, docFileName));
                    }
                }
            }
            
            if (otherDocumentsPaths.Any())
            {
                model.Employee.OtherDocumentsPath = string.Join(";", otherDocumentsPaths);
            }
        }
    }

    private bool IsValidImageFile(IFormFile file)
    {
        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        return allowedExtensions.Contains(extension) && 
               file.ContentType.StartsWith("image/") && 
               file.Length <= 5 * 1024 * 1024; // 5MB limit
    }

    private bool IsValidDocumentFile(IFormFile file)
    {
        var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        return allowedExtensions.Contains(extension) && 
               file.Length <= 10 * 1024 * 1024; // 10MB limit
    }

    private bool IsValidPesel(string pesel)
    {
        if (string.IsNullOrWhiteSpace(pesel) || pesel.Length != 11)
            return false;

        if (!pesel.All(char.IsDigit))
            return false;

        // Walidacja sumy kontrolnej PESEL
        int[] weights = { 1, 3, 7, 9, 1, 3, 7, 9, 1, 3 };
        int sum = 0;

        for (int i = 0; i < 10; i++)
        {
            sum += int.Parse(pesel[i].ToString()) * weights[i];
        }

        int checkDigit = (10 - (sum % 10)) % 10;
        return checkDigit == int.Parse(pesel[10].ToString());
    }

    [HttpGet]
        public async Task<IActionResult> EmployeDetail(int id)
        {
            try
            {
                var employee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.EmployeeId == id);

                if (employee == null)
                {
                    TempData["ErrorMessage"] = "Pracownik nie został znaleziony.";
                    return RedirectToAction(nameof(EmployeeList));
                }

                return View(employee);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas pobierania szczegółów pracownika: {ex.Message}");
                TempData["ErrorMessage"] = "Wystąpił błąd podczas pobierania danych pracownika.";
                return RedirectToAction(nameof(EmployeeList));
            }
        }

        [HttpGet]
        public async Task<IActionResult> EmployeeEdit(int id)
        {
            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.EmployeeId == id);

            if (employee == null)
            {
                return NotFound();
            }

            var viewModel = new EmployeeCreateViewModel
            {
                Employee = employee
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EmployeeEdit(int id, EmployeeCreateViewModel model)
        {
            if (id != model.Employee.EmployeeId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var existingEmployee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.EmployeeId == id);

                    if (existingEmployee == null)
                    {
                        return NotFound();
                    }

                    // Aktualizacja danych
                    existingEmployee.FirstName = model.Employee.FirstName;
                    existingEmployee.LastName = model.Employee.LastName;
                    existingEmployee.Pesel = model.Employee.Pesel;
                    existingEmployee.BirthDate = model.Employee.BirthDate;
                    existingEmployee.BirthPlace = model.Employee.BirthPlace;
                    existingEmployee.Gender = model.Employee.Gender;
                    existingEmployee.Nationality = model.Employee.Nationality;
                    existingEmployee.Education = model.Employee.Education;
                    existingEmployee.PersonalEmail = model.Employee.PersonalEmail;
                    existingEmployee.PersonalPhone = model.Employee.PersonalPhone;
                    existingEmployee.Street = model.Employee.Street;
                    existingEmployee.PostalCode = model.Employee.PostalCode;
                    existingEmployee.City = model.Employee.City;
                    existingEmployee.Country = model.Employee.Country;
                    existingEmployee.HireDate = model.Employee.HireDate;
                    existingEmployee.ContractType = model.Employee.ContractType;
                    existingEmployee.Notes = model.Employee.Notes;
                    existingEmployee.UpdatedAt = DateTime.Now;
                    existingEmployee.UpdatedBy = User.Identity?.Name ?? "System";

                    // Obsługa nowych plików
                    if (model.Photo != null)
                    {
                        existingEmployee.ImagePath = await SaveFile(model.Photo, "photos");
                    }

                    if (model.Contract != null)
                    {
                        existingEmployee.ContractPath = await SaveFile(model.Contract, "contracts");
                    }

                    if (model.IdCopy != null)
                    {
                        existingEmployee.IdCopyPath = await SaveFile(model.IdCopy, "documents");
                    }

                    _context.Update(existingEmployee);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Dane pracownika zostały zaktualizowane.";
                    return RedirectToAction(nameof(EmployeDetail), new { id = existingEmployee.EmployeeId });
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await EmployeeExists(model.Employee.EmployeeId))
                    {
                        return NotFound();
                    }
                    throw;
                }
                catch (Exception ex)
                {
                    // _logger.LogError(ex, "Błąd podczas aktualizacji pracownika");
                    ModelState.AddModelError("", "Wystąpił błąd podczas zapisywania danych.");
                }
            }

            return View(model);
        }
    
        private async Task<string> SaveFile(IFormFile file, string folder)
        {
            if (file == null || file.Length == 0)
                return string.Empty;

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", folder);
            
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return Path.Combine("uploads", folder, uniqueFileName).Replace("\\", "/");
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var employee = await _context.Employees.FindAsync(id);
                
                if (employee == null)
                {
                    return Json(new { success = false, message = "Pracownik nie został znaleziony." });
                }

                _context.Employees.Remove(employee);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Pracownik został usunięty." });
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "Błąd podczas usuwania pracownika");
                return Json(new { success = false, message = "Wystąpił błąd podczas usuwania pracownika." });
            }
        }

        // Metody pomocnicze
        private async Task<bool> EmployeeExists(int id)
        {
            return await _context.Employees.AnyAsync(e => e.EmployeeId == id);
        }

    [HttpPost]
    public IActionResult CheckPeselAvailability(string pesel)
    {
        try
        {
            // TODO: Sprawdzenie dostępności PESEL w bazie danych
            // bool exists = await _employeeService.PeselExistsAsync(pesel);
            bool exists = false; // Tymczasowo
            
            return Json(new { available = !exists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas sprawdzania dostępności PESEL: {Pesel}", pesel);
            return Json(new { available = false, error = "Wystąpił błąd podczas sprawdzania" });
        }
    }

    [HttpPost]
    public IActionResult CheckEmployeeNumberAvailability(string employeeNumber)
    {
        try
        {
            // TODO: Sprawdzenie dostępności numeru pracownika w bazie danych
            // bool exists = await _employeeService.EmployeeNumberExistsAsync(employeeNumber);
            bool exists = false; // Tymczasowo
            
            return Json(new { available = !exists });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas sprawdzania dostępności numeru pracownika: {EmployeeNumber}", employeeNumber);
            return Json(new { available = false, error = "Wystąpił błąd podczas sprawdzania" });
        }
    }
    
    
    
}