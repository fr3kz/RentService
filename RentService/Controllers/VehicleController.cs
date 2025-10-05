using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentService.Database;
using RentService.Models.VehicleModule;
using System.ComponentModel.DataAnnotations;
using RentService.Models;

namespace RentService.Controllers.VehicleModule;

public class VehicleController : Controller
{
    private readonly ILogger<VehicleController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly AppDbContext _context;

    public VehicleController(ILogger<VehicleController> logger,
        IWebHostEnvironment environment,
        AppDbContext context)
    {
        _logger = logger;
        _environment = environment;
        _context = context;
    }

    // GET: Vehicle
    public async Task<IActionResult> Index()
    {
        try
        {
            var vehicles = await _context.Cars
                .Include(v => v.MileageHistory)    
                .OrderBy(v => v.Model)
                .ToListAsync();
            
            return View(vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicles list");
            TempData["ErrorMessage"] = "Wystąpił błąd podczas pobierania listy pojazdów.";
            return View(new List<Vehicle>());
        }
    }

    // GET: Vehicle/VehicleList (alias for Index)
    public async Task<IActionResult> VehicleList()
    {
        return await Index();
    }

    // GET: Vehicle/Details/5
    public async Task<IActionResult> VehicleDetail(int id)
    {
        if (id <= 0)
        {
            _logger.LogWarning("Invalid vehicle ID provided: {Id}", id);
            return NotFound();
        }

        try
        {
            var vehicle = await _context.Cars
                .Include(v => v.ExploitationParts)
                .Include(x => x.MileageHistory)
                .Include(b => b.Repairs)
                .FirstOrDefaultAsync(v => v.ID == id);

            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle not found with ID: {Id}", id);
                return NotFound();
            }

            return View(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle details for ID: {Id}", id);
            TempData["ErrorMessage"] = "Wystąpił błąd podczas pobierania szczegółów pojazdu.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> VehicleAdd()
    {
        return View();
    }

    // POST: Vehicle/VehicleAdd
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VehicleAdd(Vehicle vehicle)
    {
        try
        {
            // Additional server-side validation
            await ValidateVehicleAsync(vehicle);

            if (ModelState.IsValid)
            {
                if (vehicle.ExploitationParts == null)
                {
                    vehicle.ExploitationParts = new List<ExploitationPart>();
                }
                
                
            
                _context.Cars.Add(vehicle);
                
                var mileageEntry = new VehicleMileage
                {
                    CreatedAt = DateTime.UtcNow,
                    Mileage =  (int)vehicle.MileageForm,
                    Type = MileageAddEnum.API,
                    Car = vehicle
                };

                vehicle.MileageHistory = new List<VehicleMileage> { mileageEntry };

                

                _context.VehicleMileages.Add(mileageEntry);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Vehicle created successfully with ID: {Id}", vehicle.ID);
                TempData["SuccessMessage"] = $"Pojazd {vehicle.Model} został pomyślnie dodany.";
                
                return RedirectToAction(nameof(VehicleDetail), new { id = vehicle.ID });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating vehicle: {@Vehicle}", vehicle);
            ModelState.AddModelError("", "Wystąpił błąd podczas dodawania pojazdu. Spróbuj ponownie.");
        }

        return View(vehicle);
    }

 

    // GET: Vehicle/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        if (id <= 0)
        {
            return NotFound();
        }

        try
        {
            var vehicle = await _context.Cars.FindAsync(id);
            if (vehicle == null)
            {
                _logger.LogWarning("Vehicle not found for editing with ID: {Id}", id);
                return NotFound();
            }

            return View(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving vehicle for editing with ID: {Id}", id);
            TempData["ErrorMessage"] = "Wystąpił błąd podczas pobierania danych pojazdu do edycji.";
            return RedirectToAction(nameof(Index));
        }
    }

    // POST: Vehicle/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Vehicle vehicle)
    {
        if (id != vehicle.ID)
        {
            return NotFound();
        }

        try
        {
            // Additional server-side validation
            await ValidateVehicleAsync(vehicle, isEdit: true);

            if (ModelState.IsValid)
            {
                _context.Update(vehicle);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Vehicle updated successfully with ID: {Id}", vehicle.ID);
                TempData["SuccessMessage"] = $"Pojazd {vehicle.Model} został pomyślnie zaktualizowany.";
                
                return RedirectToAction(nameof(VehicleDetail), new { id = vehicle.ID });
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await VehicleExistsAsync(vehicle.ID))
            {
                return NotFound();
            }
            else
            {
                ModelState.AddModelError("", "Pojazd został zmodyfikowany przez innego użytkownika. Odśwież stronę i spróbuj ponownie.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating vehicle with ID: {Id}", id);
            ModelState.AddModelError("", "Wystąpił błąd podczas aktualizacji pojazdu. Spróbuj ponownie.");
        }

        return View(vehicle);
    }

    // POST: Vehicle/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var vehicle = await _context.Cars
                .Include(v => v.ExploitationParts)
                .FirstOrDefaultAsync(v => v.ID == id);

            if (vehicle == null)
            {
                return Json(new { success = false, message = "Pojazd nie został znaleziony." });
            }

            // Check if vehicle has any active rentals (if you have rental system)
            // if (await HasActiveRentalsAsync(id))
            // {
            //     return Json(new { success = false, message = "Nie można usunąć pojazdu, który ma aktywne wypożyczenia." });
            // }

            // Remove related exploitation parts first
            if (vehicle.ExploitationParts?.Any() == true)
            {
                _context.ExploitationParts.RemoveRange(vehicle.ExploitationParts);
            }

            _context.Cars.Remove(vehicle);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Vehicle deleted successfully with ID: {Id}", id);
            
            return Json(new { success = true, message = $"Pojazd {vehicle.Model} został pomyślnie usunięty." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting vehicle with ID: {Id}", id);
            return Json(new { success = false, message = "Wystąpił błąd podczas usuwania pojazdu." });
        }
    }

    // POST: Vehicle/ToggleAvailability/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAvailability(int id)
    {
        try
        {
            var vehicle = await _context.Cars.FindAsync(id);
            if (vehicle == null)
            {
                return Json(new { success = false, message = "Pojazd nie został znaleziony." });
            }

            vehicle.IsAvailable = !vehicle.IsAvailable;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Vehicle availability toggled for ID: {Id}, new status: {Status}", id, vehicle.IsAvailable);

            var statusText = vehicle.IsAvailable ? "dostępny" : "niedostępny";
            return Json(new { 
                success = true, 
                message = $"Pojazd {vehicle.Model} jest teraz {statusText}.",
                isAvailable = vehicle.IsAvailable
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling availability for vehicle ID: {Id}", id);
            return Json(new { success = false, message = "Wystąpił błąd podczas zmiany dostępności pojazdu." });
        }
        
    }
    
    // GET: Vehicle/ExploitationPartAdd/5
    [HttpGet]
    public async Task<IActionResult> ExploitationPartAdd(int vehicleId)
    {
        var vehicle = await _context.Cars.FindAsync(vehicleId);
        if (vehicle == null)
        {
            return NotFound();
        }

        var exploitationPart = new ExploitationPart
        {
            VehicleID = vehicleId,
            Car = vehicle,
            LastReplacementDate = DateTime.Now,
            NextReplacementDueDate = DateTime.Now.AddMonths(6)
        };

        return View(exploitationPart);
    }
    
    // POST: Vehicle/ExploitationPartAdd

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExploitationPartAdd(ExploitationPart model)
    {
        try
        {
            // Reload vehicle data for display in case of validation errors
            model.Car = await _context.Cars
                .FirstOrDefaultAsync(v => v.ID == model.VehicleID);

            if (model.Car == null)
            {
                TempData["ErrorMessage"] = "Pojazd nie został znaleziony.";
                return RedirectToAction("Index");
            }

            // Check if model is valid
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Clear navigation property before saving to avoid EF issues
            var exploitationPart = new ExploitationPart
            {
                VehicleID = model.VehicleID,
                PartType = model.PartType,
                PartName = model.PartName?.Trim(),
                TotalKm = model.TotalKm,
                CurrentKm = model.CurrentKm,
                LastReplacementDate = model.LastReplacementDate,
                NextReplacementDueDate = model.NextReplacementDueDate,
                PartCondition = model.PartCondition,
                Notes = model.Notes?.Trim()
            };
        
            _context.ExploitationParts.Add(exploitationPart);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Część eksploatacyjna '{exploitationPart.PartName}' została pomyślnie dodana.";
            return RedirectToAction("VehicleDetail", new { id = model.VehicleID });
        }
        catch (Exception ex)
        {
            // Log the exception here if you have logging configured
            ModelState.AddModelError("", "Wystąpił błąd podczas zapisywania części. Spróbuj ponownie.");
            return View(model);
        }
    }
    
    [HttpGet]
    public async Task<IActionResult> ExploitationPartEdit(int id)
    {
        var exploitationPart = await _context.ExploitationParts
            .FirstOrDefaultAsync(ep => ep.ID == id);

        if (exploitationPart == null)
        {
            TempData["ErrorMessage"] = "Część eksploatacyjna nie została znaleziona.";
            return RedirectToAction("Index");
        }

        // Jeśli chcesz pobrać dane pojazdu, wykonaj drugie zapytanie:
        var vehicle = await _context.Cars
            .FirstOrDefaultAsync(v => v.ID == exploitationPart.VehicleID);

        exploitationPart.Car = vehicle; // Ręcznie przypisz dane pojazdu do ExploitationPart

        return View(exploitationPart);
    }

    // POST: Vehicle/ExploitationPartEdit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExploitationPartEdit(int id, ExploitationPart model)
    {
        if (id != model.ID)
        {
            TempData["ErrorMessage"] = "Nieprawidłowe dane części.";
            return RedirectToAction("Index");
        }

        try
        {
            // Remove Car validation errors since it's just for display
            ModelState.Remove("Car");

            // Reload vehicle data for display in case of validation errors
            model.Car = await _context.Cars
                .FirstOrDefaultAsync(v => v.ID == model.VehicleID);

            if (model.Car == null)
            {
                TempData["ErrorMessage"] = "Pojazd nie został znaleziony.";
                return RedirectToAction("Index");
            }

            // Check if model is valid
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Get the existing entity from database
            var existingPart = await _context.ExploitationParts
                .FirstOrDefaultAsync(ep => ep.ID == id);

            if (existingPart == null)
            {
                TempData["ErrorMessage"] = "Część eksploatacyjna nie została znaleziona.";
                return RedirectToAction("Index");
            }

            // Update only the properties we want to change
            existingPart.PartType = model.PartType;
            existingPart.PartName = model.PartName?.Trim();
            existingPart.TotalKm = model.TotalKm;
            existingPart.CurrentKm = model.CurrentKm;
            existingPart.LastReplacementDate = model.LastReplacementDate;
            existingPart.NextReplacementDueDate = model.NextReplacementDueDate;
            existingPart.PartCondition = model.PartCondition;
            existingPart.Notes = model.Notes?.Trim();

            _context.Update(existingPart);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Część eksploatacyjna '{existingPart.PartName}' została pomyślnie zaktualizowana.";
            return RedirectToAction("VehicleDetail", new { id = model.VehicleID });
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!ExploitationPartExists(model.ID))
            {
                TempData["ErrorMessage"] = "Część eksploatacyjna nie została znaleziona.";
                return RedirectToAction("Index");
            }
            else
            {
                ModelState.AddModelError("", "Część została zmodyfikowana przez innego użytkownika. Odśwież stronę i spróbuj ponownie.");
                return View(model);
            }
        }
        catch (Exception ex)
        {
            // Log the exception here if you have logging configured
            ModelState.AddModelError("", "Wystąpił błąd podczas aktualizacji części. Spróbuj ponownie.");
            return View(model);
        }
    }

    // Helper method to check if ExploitationPart exists
    private bool ExploitationPartExists(int id)
    {
        return _context.ExploitationParts.Any(e => e.ID == id);
    }

    // DELETE: Vehicle/ExploitationPartDelete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExploitationPartDelete(int id, int vehicleId)
    {
        try
        {
            var exploitationPart = await _context.ExploitationParts
                .FirstOrDefaultAsync(ep => ep.ID == id);

            if (exploitationPart == null)
            {
                TempData["ErrorMessage"] = "Część eksploatacyjna nie została znaleziona.";
            }
            else
            {
                _context.ExploitationParts.Remove(exploitationPart);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Część eksploatacyjna '{exploitationPart.PartName}' została pomyślnie usunięta.";
            }
        }
        catch (Exception ex)
        {
            // Log the exception here if you have logging configured
            TempData["ErrorMessage"] = "Wystąpił błąd podczas usuwania części. Spróbuj ponownie.";
        }

        return RedirectToAction("VehicleDetail", new { id = vehicleId });
    }

    // GET: Vehicle/Search
    public async Task<IActionResult> Search(string query, bool? isAvailable, string color, decimal? minPrice, decimal? maxPrice)
    {
        try
        {
            var vehiclesQuery = _context.Cars.AsQueryable();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(query))
            {
                vehiclesQuery = vehiclesQuery.Where(v => 
                    v.Model.Contains(query) || 
                    v.RegistrationNumber.Contains(query) ||
                    v.VIN.Contains(query));
            }

            if (isAvailable.HasValue)
            {
                vehiclesQuery = vehiclesQuery.Where(v => v.IsAvailable == isAvailable.Value);
            }

            if (!string.IsNullOrWhiteSpace(color))
            {
                vehiclesQuery = vehiclesQuery.Where(v => v.Color == color);
            }

            if (minPrice.HasValue)
            {
                vehiclesQuery = vehiclesQuery.Where(v => v.WeekRentalPrice >= minPrice.Value);
            }

            if (maxPrice.HasValue)
            {
                vehiclesQuery = vehiclesQuery.Where(v => v.WeekRentalPrice <= maxPrice.Value);
            }

            var vehicles = await vehiclesQuery
                .OrderBy(v => v.Model)
                .ToListAsync();

            return PartialView("_VehicleList", vehicles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching vehicles with query: {Query}", query);
            return PartialView("_VehicleList", new List<Vehicle>());
        }
    }

    // GET: Vehicle/GetAvailableVehicles
    [HttpGet]
    public async Task<IActionResult> GetAvailableVehicles()
    {
        try
        {
            var availableVehicles = await _context.Cars
                .Where(v => v.IsAvailable)
                .Select(v => new
                {
                    id = v.ID,
                    model = v.Model,
                    registrationNumber = v.RegistrationNumber,
                    dailyPrice = v.WeekRentalPrice,
                    color = v.Color,
                    mileage = v.Mileage
                })
                .OrderBy(v => v.model)
                .ToListAsync();

            return Json(new { success = true, vehicles = availableVehicles });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available vehicles");
            return Json(new { success = false, message = "Wystąpił błąd podczas pobierania dostępnych pojazdów." });
        }
    }

    #region Private Methods

    private async Task<bool> VehicleExistsAsync(int id)
    {
        return await _context.Cars.AnyAsync(e => e.ID == id);
    }

    private async Task ValidateVehicleAsync(Vehicle vehicle, bool isEdit = false)
    {
        // Check for duplicate registration number
        var duplicateRegistration = await _context.Cars
            .AnyAsync(v => v.RegistrationNumber == vehicle.RegistrationNumber && 
                          (!isEdit || v.ID != vehicle.ID));

        if (duplicateRegistration)
        {
            ModelState.AddModelError(nameof(vehicle.RegistrationNumber), 
                "Pojazd o podanym numerze rejestracyjnym już istnieje w systemie.");
        }

        // Check for duplicate VIN
        var duplicateVIN = await _context.Cars
            .AnyAsync(v => v.VIN == vehicle.VIN && 
                          (!isEdit || v.ID != vehicle.ID));

        if (duplicateVIN)
        {
            ModelState.AddModelError(nameof(vehicle.VIN), 
                "Pojazd o podanym numerze VIN już istnieje w systemie.");
        }

        // Validate year of manufacture
        if (vehicle.YearOfManufacture > DateTime.Now)
        {
            ModelState.AddModelError(nameof(vehicle.YearOfManufacture), 
                "Rok produkcji nie może być z przyszłości.");
        }

        if (vehicle.YearOfManufacture < DateTime.Now.AddYears(-50))
        {
            ModelState.AddModelError(nameof(vehicle.YearOfManufacture), 
                "Rok produkcji nie może być starszy niż 50 lat.");
        }

        // Validate daily rental price
        if (vehicle.WeekRentalPrice <= 0)
        {
            ModelState.AddModelError(nameof(vehicle.WeekRentalPrice), 
                "Cena wynajmu musi być większa od 0.");
        }

        if (vehicle.WeekRentalPrice > 10000)
        {
            ModelState.AddModelError(nameof(vehicle.WeekRentalPrice), 
                "Cena wynajmu nie może przekraczać 10,000 zł dziennie.");
        }

        // Validate mileage
        if (vehicle.Mileage < 0)
        {
            ModelState.AddModelError(nameof(vehicle.Mileage), 
                "Przebieg nie może być ujemny.");
        }

        if (vehicle.Mileage > 1000000)
        {
            ModelState.AddModelError(nameof(vehicle.Mileage), 
                "Przebieg nie może przekraczać 1,000,000 km.");
        }

        // Validate VIN format (17 characters, alphanumeric)
        if (!string.IsNullOrEmpty(vehicle.VIN))
        {
            if (vehicle.VIN.Length != 17)
            {
                ModelState.AddModelError(nameof(vehicle.VIN), 
                    "Numer VIN musi mieć dokładnie 17 znaków.");
            }
            else if (!vehicle.VIN.All(c => char.IsLetterOrDigit(c)))
            {
                ModelState.AddModelError(nameof(vehicle.VIN), 
                    "Numer VIN może zawierać tylko litery i cyfry.");
            }
        }

        // Validate registration number format
        if (!string.IsNullOrEmpty(vehicle.RegistrationNumber))
        {
            var cleanedRegNumber = vehicle.RegistrationNumber.Replace(" ", "").Replace("-", "");
            if (cleanedRegNumber.Length < 4 || cleanedRegNumber.Length > 8)
            {
                ModelState.AddModelError(nameof(vehicle.RegistrationNumber), 
                    "Numer rejestracyjny ma nieprawidłowy format.");
            }
        }
    }

    // Uncomment if you have rental system
    // private async Task<bool> HasActiveRentalsAsync(int vehicleId)
    // {
    //     return await _context.Rentals
    //         .AnyAsync(r => r.VehicleId == vehicleId && 
    //                       r.Status == RentalStatus.Active);
    // }

    #endregion
}