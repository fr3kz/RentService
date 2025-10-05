using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RentService.Database;
using RentService.Models;
using System.Text;

namespace RentService.Controllers;
public class ServiceController : Controller
{
    private readonly ILogger<ServiceController> _logger;
    private readonly AppDbContext _context;
    
    public ServiceController(ILogger<ServiceController> logger,
        IWebHostEnvironment environment,
        AppDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    // Lista wszystkich napraw
    [HttpGet]
    public async Task<IActionResult> RepairsList()
    {
        try
        {
            var repairs = await _context.Repairs
                .Include(r => r.Vehicle)
                .Include(r => r.RepairParts)
                    .ThenInclude(rp => rp.ExploitationPart)
                .OrderByDescending(r => r.StartDate)
                .ToListAsync();

            _logger.LogInformation("Pobrano {Count} napraw z bazy danych", repairs.Count);
            return View(repairs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania listy napraw");
            TempData["ErrorMessage"] = "Wystąpił błąd podczas ładowania listy napraw.";
            return View(new List<Repair>());
        }
    }

    // Szczegóły naprawy
    [HttpGet]
    public async Task<IActionResult> RepairDetails(int id, bool ajax = false)
    {
        try
        {
            if (ajax)
            {
                var repair = await _context.Repairs
                    .Where(r => r.ID == id)
                    .Select(r => new { 
                        r.Status, 
                        IsCompleted = r.Status == RepairStatus.Completed,
                        PartsCount = r.RepairParts.Count 
                    })
                    .FirstOrDefaultAsync();

                if (repair == null)
                {
                    return Json(new { error = "Naprawa nie znaleziona" });
                }

                return Json(repair);
            }

            var repairDetails = await _context.Repairs
                .Include(r => r.Vehicle)
                .Include(r => r.RepairParts)
                    .ThenInclude(rp => rp.ExploitationPart)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (repairDetails == null)
            {
                _logger.LogWarning("Nie znaleziono naprawy o ID: {Id}", id);
                TempData["ErrorMessage"] = "Nie znaleziono naprawy.";
                return RedirectToAction(nameof(RepairsList));
            }

            _logger.LogInformation("Wyświetlanie szczegółów naprawy ID: {Id}", id);
            return View(repairDetails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania szczegółów naprawy ID: {Id}", id);
            if (ajax)
            {
                return Json(new { error = "Błąd serwera" });
            }
            TempData["ErrorMessage"] = "Wystąpił błąd podczas ładowania szczegółów naprawy.";
            return RedirectToAction(nameof(RepairsList));
        }
    }

    // Formularz dodawania naprawy - GET
    [HttpGet]
    public async Task<IActionResult> RepairAdd(int? vehicleId)
    {
        try
        {
            await PrepareRepairViewData(vehicleId);

            var repair = new Repair
            {
                StartDate = DateTime.Now.Date,
                Status = RepairStatus.Scheduled
            };

            if (vehicleId.HasValue)
            {
                repair.VehicleID = vehicleId.Value;
                var vehicle = await GetVehicleByIdAsync(vehicleId.Value);
                if (vehicle != null)
                {
                    repair.MileageAtRepair = (int)vehicle.Mileage;
                    _logger.LogInformation("Przygotowanie formularza dodawania naprawy dla pojazdu ID: {VehicleId}", vehicleId);
                }
            }

            return View(repair);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas przygotowania formularza dodawania naprawy");
            TempData["ErrorMessage"] = "Wystąpił błąd podczas ładowania formularza.";
            return RedirectToAction(nameof(RepairsList));
        }
    }

    // Dodawanie naprawy - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RepairAdd(Repair repair)
    {
        if (ModelState.IsValid)
        {
            try
            {
                if (repair.CompletionDate.HasValue  && repair.CompletionDate < repair.StartDate)
                {
                    ModelState.AddModelError("CompletionDate", "Data zakończenia nie może być wcześniejsza niż data rozpoczęcia.");
                }

                var vehicleExists = await VehicleExistsAsync(repair.VehicleID);
                if (!vehicleExists)
                {
                    ModelState.AddModelError("VehicleID", "Wybrany pojazd nie istnieje.");
                }

                if (!ModelState.IsValid)
                {
                    await PrepareRepairViewData(repair.VehicleID);
                    return View(repair);
                }

               if (repair.Status == default)
                {
                    repair.Status = RepairStatus.Scheduled;
                }

                Vehicle? vehicle = await _context.Cars
                    .Include(v => v.MileageHistory)
                    .FirstOrDefaultAsync(v => v.ID == repair.VehicleID);
                
                
                if(vehicle == null)
                {
                    ModelState.AddModelError("VehicleID", "Wybrany pojazd nie istnieje.");
                    return View(repair);
                }
                repair.Vehicle = vehicle;
                var parts =  _context.ExploitationParts.Where(r => r.VehicleID == vehicle.ID);


                if (!repair.CzyNaprawaHistoryczna)
                {
                    foreach (var p in parts)
                    {
                        p.CurrentKm = (int)(-vehicle.Mileage + repair.MileageAtRepair);
                        _context.ExploitationParts.Update(p);
                    }    
                }
               
                
                var mileageEntry = new VehicleMileage
                {
                    CreatedAt = repair.StartDate,
                    Mileage = repair.MileageAtRepair,        
                    Type = MileageAddEnum.Serwis,
                    Car = vehicle  
                };
                _context.VehicleMileages.Add(mileageEntry);                

                _context.Repairs.Add(repair);
                _context.Cars.Update(vehicle);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Dodano nową naprawę ID: {RepairId} dla pojazdu ID: {VehicleId}", repair.ID, repair.VehicleID);
                TempData["SuccessMessage"] = "Naprawa została pomyślnie dodana.";
                return RedirectToAction(nameof(RepairDetails), new { id = repair.ID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas dodawania naprawy");
                ModelState.AddModelError("", ex.Message);
            }
        }

        await PrepareRepairViewData(repair.VehicleID);
        return View(repair);
    }

    // Formularz edycji naprawy - GET
    [HttpGet]
    public async Task<IActionResult> RepairEdit(int id)
    {
        try
        {
            var repair = await _context.Repairs
                .Include(r => r.RepairParts)
                    .ThenInclude(rp => rp.ExploitationPart)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (repair == null)
            {
                _logger.LogWarning("Nie znaleziono naprawy do edycji o ID: {Id}", id);
                TempData["ErrorMessage"] = "Nie znaleziono naprawy.";
                return RedirectToAction(nameof(RepairsList));
            }

            await PrepareRepairViewData(repair.VehicleID);
            _logger.LogInformation("Wyświetlanie formularza edycji naprawy ID: {Id}", id);
            return View(repair);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas przygotowania edycji naprawy ID: {Id}", id);
            TempData["ErrorMessage"] = "Wystąpił błąd podczas ładowania formularza edycji.";
            return RedirectToAction(nameof(RepairsList));
        }
    }

    // Edycja naprawy - POST
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RepairEdit(Repair repair)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var existingRepair = await _context.Repairs.FindAsync(repair.ID);
                if (existingRepair == null)
                {
                    _logger.LogWarning("Próba edycji nieistniejącej naprawy ID: {Id}", repair.ID);
                    TempData["ErrorMessage"] = "Naprawa nie została znaleziona.";
                    return RedirectToAction(nameof(RepairsList));
                }

                existingRepair.VehicleID = repair.VehicleID;
                existingRepair.StartDate = repair.StartDate;
                existingRepair.MileageAtRepair = repair.MileageAtRepair;
                existingRepair.Description = repair.Description;
                existingRepair.Cost = repair.Cost;
                existingRepair.RepairType = repair.RepairType;
                existingRepair.Status = repair.Status;
                existingRepair.AdditionalNotes = repair.AdditionalNotes;
                existingRepair.CompletionDate = repair.CompletionDate;
                existingRepair.InvoiceNumber = repair.InvoiceNumber;

                if (repair.Status == RepairStatus.Completed && existingRepair.CompletionDate == null)
                {
                    existingRepair.CompletionDate = DateTime.Now;
                }

                await _context.SaveChangesAsync();

                _logger.LogInformation("Zaktualizowano naprawę ID: {Id}", repair.ID);
                TempData["SuccessMessage"] = "Naprawa została pomyślnie zaktualizowana.";
                return RedirectToAction(nameof(RepairDetails), new { id = repair.ID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji naprawy ID: {Id}", repair.ID);
                ModelState.AddModelError("", "Wystąpił błąd podczas aktualizacji naprawy.");
            }
        }

        await PrepareRepairViewData(repair.VehicleID);
        return View(repair);
    }

    // Usuwanie naprawy
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RepairDelete(int id)
    {
        try
        {
            var repair = await _context.Repairs
                .Include(r => r.RepairParts)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (repair == null)
            {
                _logger.LogWarning("Próba usunięcia nieistniejącej naprawy ID: {Id}", id);
                if (Request.Headers.Accept.ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message = "Naprawa nie została znaleziona." });
                }
                TempData["ErrorMessage"] = "Naprawa nie została znaleziona.";
                return RedirectToAction(nameof(RepairsList));
            }

            if (repair.RepairParts.Count != 0)
            {
                _context.RepairExploitationParts.RemoveRange(repair.RepairParts);
            }
            
            _context.Repairs.Remove(repair);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usunięto naprawę ID: {Id} wraz z {Count} powiązaniami z częściami", id, repair.RepairParts.Count);
            
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Naprawa została pomyślnie usunięta." });
            }

            TempData["SuccessMessage"] = "Naprawa została pomyślnie usunięta.";
            return RedirectToAction(nameof(RepairsList));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas usuwania naprawy ID: {Id}", id);
            
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = false, message = "Wystąpił błąd podczas usuwania naprawy." });
            }

            TempData["ErrorMessage"] = "Wystąpił błąd podczas usuwania naprawy.";
            return RedirectToAction(nameof(RepairsList));
        }
    }

    // Zarządzanie częściami w naprawie - GET
    [HttpGet]
    public async Task<IActionResult> ManageRepairParts(int repairId)
    {
        try
        {
            var repair = await _context.Repairs
                .Include(r => r.Vehicle)
                .Include(r => r.RepairParts)
                    .ThenInclude(rp => rp.ExploitationPart)
                .FirstOrDefaultAsync(r => r.ID == repairId);

            if (repair == null)
            {
                _logger.LogWarning("Nie znaleziono naprawy o ID: {RepairId} do zarządzania częściami", repairId);
                TempData["ErrorMessage"] = "Nie znaleziono naprawy.";
                return RedirectToAction(nameof(RepairsList));
            }

            if (Request.Query.ContainsKey("ajax"))
            {
                return Json(new { partsCount = repair.RepairParts.Count });
            }

            var usedPartIds = repair.RepairParts.Select(rp => rp.ExploitationPartID).ToList();
            var availableParts = await _context.ExploitationParts
                .Where(ep => ep.VehicleID == repair.VehicleID && !usedPartIds.Contains(ep.ID))
                .Select(ep => new { ep.ID, DisplayText = $"{ep.PartName} - {ep.PartType}" })
                .ToListAsync();

            ViewBag.AvailableParts = new SelectList(availableParts, "ID", "DisplayText");
            ViewBag.PartActions = new SelectList(Enum.GetValues<PartAction>()
                .Cast<PartAction>()
                .Select(x => new { 
                    Value = (int)x, 
                    Text = GetPartActionDisplayName(x)
                }), "Value", "Text");

            _logger.LogInformation("Wyświetlanie zarządzania częściami dla naprawy ID: {RepairId}, dostępnych części: {Count}", repairId, availableParts.Count);
            return View(repair);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas ładowania zarządzania częściami dla naprawy ID: {RepairId}", repairId);
            TempData["ErrorMessage"] = "Wystąpił błąd podczas ładowania zarządzania częściami.";
            return RedirectToAction(nameof(RepairsList));
        }
    }

    // Dodawanie części do naprawy
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPartToRepair(int repairId, int exploitationPartId, PartAction action, string? partNotes, DateTime? nextReplacementDate, int? nextReplacementMileage)
    {
        try{  
            var repairExists = await _context.Repairs.AnyAsync(r => r.ID == repairId);
            if (!repairExists)
            {
                _logger.LogWarning("Próba dodania części do nieistniejącej naprawy ID: {RepairId}", repairId);
                TempData["ErrorMessage"] = "Naprawa nie została znaleziona.";
                return RedirectToAction(nameof(RepairsList));
            }

            var exploitationPart = await _context.ExploitationParts.FirstOrDefaultAsync(ep => ep.ID == exploitationPartId);
            if (exploitationPart == null)
            {
                _logger.LogWarning("Próba dodania nieistniejącej części ID: {PartId} do naprawy ID: {RepairId}", exploitationPartId, repairId);
                TempData["ErrorMessage"] = "Wybrana część nie została znaleziona.";
                return RedirectToAction(nameof(ManageRepairParts), new { repairId });
            }

            var existingPart = await _context.RepairExploitationParts
                .FirstOrDefaultAsync(rp => rp.RepairID == repairId && rp.ExploitationPartID == exploitationPartId);

            if (existingPart != null)
            {
                _logger.LogWarning("Próba dodania już istniejącego powiązania: naprawa {RepairId}, część {PartId}", repairId, exploitationPartId);
                TempData["ErrorMessage"] = "Ta część jest już przypisana do tej naprawy.";
            }
            else
            {
                var repairPart = new RepairExploitationPart
                {
                    RepairID = repairId,
                    ExploitationPartID = exploitationPartId,
                    ExploitationPart = exploitationPart,
                    Action = action,
                    PartNotes = string.IsNullOrWhiteSpace(partNotes) ? null : partNotes.Trim()
                   
                };
                
                if (nextReplacementDate.HasValue)
                {
                    repairPart.NextServiceCheck = nextReplacementDate.Value;
                    _logger.LogInformation("Ustawiono datę następnej wymiany części ID: {PartId} na: {Date:yyyy-MM-dd}", exploitationPartId, nextReplacementDate.Value);
                }

                if (nextReplacementMileage.HasValue)
                {
                    repairPart.NextMillageCheck = nextReplacementMileage.Value;
                    _logger.LogInformation("Ustawiono przebieg następnej wymiany części ID: {PartId} na: {Mileage}", exploitationPartId, nextReplacementMileage.Value);
                }

                _context.RepairExploitationParts.Add(repairPart);
                await _context.SaveChangesAsync();

                var successMessage = nextReplacementDate.HasValue 
                    ? $"Część została pomyślnie dodana do naprawy. Data następnej wymiany ustawiona na: {nextReplacementDate.Value:dd.MM.yyyy}."
                    : "Część została pomyślnie dodana do naprawy.";

                _logger.LogInformation("Dodano część ID: {PartId} do naprawy ID: {RepairId} z akcją: {Action}", exploitationPartId, repairId, action);
                TempData["SuccessMessage"] = successMessage;
            }
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Nieprawidłowe dane podczas dodawania części {PartId} do naprawy {RepairId}", exploitationPartId, repairId);
            TempData["ErrorMessage"] = "Podane dane są nieprawidłowe. Sprawdź wprowadzone wartości.";
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Błąd bazy danych podczas dodawania części {PartId} do naprawy {RepairId}", exploitationPartId, repairId);
            TempData["ErrorMessage"] = "Wystąpił błąd podczas zapisywania do bazy danych.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nieoczekiwany błąd podczas dodawania części {PartId} do naprawy {RepairId}", exploitationPartId, repairId);
            TempData["ErrorMessage"] = "Wystąpił nieoczekiwany błąd podczas dodawania części.";
        }

        return RedirectToAction(nameof(ManageRepairParts), new { repairId });
    }

    // Usuwanie części z naprawy
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemovePartFromRepair(int repairId, int exploitationPartId)
    {
        try
        {
            var repairPart = await _context.RepairExploitationParts
                .FirstOrDefaultAsync(rp => rp.RepairID == repairId && rp.ExploitationPartID == exploitationPartId);

            if (repairPart != null)
            {
                _context.RepairExploitationParts.Remove(repairPart);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Usunięto część ID: {PartId} z naprawy ID: {RepairId}", exploitationPartId, repairId);
                TempData["SuccessMessage"] = "Część została usunięta z naprawy.";
            }
            else
            {
                _logger.LogWarning("Próba usunięcia nieistniejącego powiązania: naprawa {RepairId}, część {PartId}", repairId, exploitationPartId);
                TempData["ErrorMessage"] = "Nie znaleziono powiązania części z naprawą.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas usuwania części {PartId} z naprawy {RepairId}", exploitationPartId, repairId);
            TempData["ErrorMessage"] = "Wystąpił błąd podczas usuwania części.";
        }

        return RedirectToAction(nameof(ManageRepairParts), new { repairId });
    }

    // Historia napraw dla pojazdu
    [HttpGet]
    public async Task<IActionResult> VehicleRepairHistory(int vehicleId)
    {
        try
        {
            var vehicle = await _context.Cars
                .Include(v => v.Repairs.OrderByDescending(r => r.StartDate))
                    .ThenInclude(r => r.RepairParts)
                        .ThenInclude(rp => rp.ExploitationPart)
                .FirstOrDefaultAsync(v => v.ID == vehicleId);

            if (vehicle == null)
            {
                _logger.LogWarning("Nie znaleziono pojazdu o ID: {VehicleId}", vehicleId);
                TempData["ErrorMessage"] = "Nie znaleziono pojazdu.";
                return RedirectToAction("VehicleList", "Vehicle");
            }


            _logger.LogInformation("Wyświetlanie historii napraw dla pojazdu ID: {VehicleId}, liczba napraw: {Count}", vehicleId, vehicle.Repairs.Count);
            return View(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania historii napraw dla pojazdu ID: {VehicleId}", vehicleId);
            TempData["ErrorMessage"] = "Wystąpił błąd podczas ładowania historii napraw.";
            return RedirectToAction("VehicleList", "Vehicle");
        }
    }

    // Zmiana statusu naprawy
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRepairStatus(int repairId, RepairStatus newStatus)
    {
        try
        {
            var repair = await _context.Repairs.FindAsync(repairId);
            if (repair == null)
            {
                _logger.LogWarning("Próba zmiany statusu nieistniejącej naprawy ID: {RepairId}", repairId);
                if (Request.Headers.Accept.ToString().Contains("application/json"))
                {
                    return Json(new { success = false, message = "Naprawa nie została znaleziona." });
                }
                TempData["ErrorMessage"] = "Naprawa nie została znaleziona.";
                return RedirectToAction(nameof(RepairDetails), new { id = repairId });
            }

            var oldStatus = repair.Status;
            repair.Status = newStatus;

            switch (newStatus)
            {
                case RepairStatus.InProgress:
                    break;

                case RepairStatus.Completed:
                    if (repair.CompletionDate == null)
                    {
                        repair.CompletionDate = DateTime.Now;
                    }
                    break;

                case RepairStatus.Cancelled:
                    break;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Zmieniono status naprawy ID: {RepairId} z {OldStatus} na {NewStatus}", repairId, oldStatus, newStatus);
            
            
            
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = true, message = "Status naprawy został zaktualizowany." });
            }

            TempData["SuccessMessage"] = "Status naprawy został zaktualizowany.";
            return RedirectToAction(nameof(RepairDetails), new { id = repairId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zmiany statusu naprawy ID: {RepairId}", repairId);
            
            if (Request.Headers.Accept.ToString().Contains("application/json"))
            {
                return Json(new { success = false, message = "Wystąpił błąd podczas zmiany statusu." });
            }

            TempData["ErrorMessage"] = "Wystąpił błąd podczas zmiany statusu.";
            return RedirectToAction(nameof(RepairDetails), new { id = repairId });
        }
    }

    // Oznacz naprawę jako zakończoną
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsCompleted(int id)
    {
        var repair = await _context.Repairs.FindAsync(id);

        
        if (repair == null)
        {
            return NotFound();
        }

        var repairParts = _context.RepairExploitationParts.Where(r => r.RepairID == id);
        
        if (!repair.CzyNaprawaHistoryczna)
        {
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                foreach (var p in repairParts)
                {
                    var epart = await _context.ExploitationParts.FindAsync(p.ExploitationPartID);
                    if (epart != null)
                    {
                        epart.NextReplacementDueDate = p.NextServiceCheck;
                        epart.TotalKm = p.NextMillageCheck;
                    }
                }
                await transaction.CommitAsync();
            }
        }
        
        repair.CompletionDate = DateTime.Now;
        await _context.SaveChangesAsync();
        
        return await ChangeRepairStatus(id, RepairStatus.Completed);
    }

    // Ponownie otwórz naprawę
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenRepair(int id)
    {
        try
        {
            var repair = await _context.Repairs.FindAsync(id);
            if (repair == null)
            {
                return Json(new { success = false, message = "Naprawa nie została znaleziona." });
            }

            repair.Status = RepairStatus.InProgress;
            repair.CompletionDate = null;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Ponownie otwarto naprawę ID: {Id}", id);
            return Json(new { success = true, message = "Naprawa została ponownie otwarta." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas ponownego otwierania naprawy ID: {Id}", id);
            return Json(new { success = false, message = "Wystąpił błąd podczas ponownego otwierania naprawy." });
        }
    }

    

    // Generuj raport PDF (placeholder)
    [HttpGet]
    public async Task<IActionResult> GenerateReport(int id)
    {
        try
        {
            var repair = await _context.Repairs
                .Include(r => r.Vehicle)
                .Include(r => r.RepairParts)
                    .ThenInclude(rp => rp.ExploitationPart)
                .FirstOrDefaultAsync(r => r.ID == id);

            if (repair == null)
            {
                TempData["ErrorMessage"] = "Nie znaleziono naprawy.";
                return RedirectToAction(nameof(RepairDetails), new { id });
            }

            _logger.LogInformation("Żądanie generowania raportu PDF dla naprawy ID: {Id}", id);
            TempData["InfoMessage"] = "Funkcja generowania raportu PDF będzie dostępna wkrótce.";
            return RedirectToAction(nameof(RepairDetails), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas generowania raportu dla naprawy ID: {Id}", id);
            TempData["ErrorMessage"] = "Wystąpił błąd podczas generowania raportu.";
            return RedirectToAction(nameof(RepairDetails), new { id });
        }
    }

    // Aktualizacja uwag o części w naprawie - AJAX
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> UpdatePartNotes(int repairId, int exploitationPartId, string? notes)
{
    try
    {
        var repairPart = await _context.RepairExploitationParts
            .FirstOrDefaultAsync(rp => rp.RepairID == repairId && rp.ExploitationPartID == exploitationPartId);

        if (repairPart == null)
        {
            return Json(new { success = false, message = "Nie znaleziono powiązania części z naprawą." });
        }

        repairPart.PartNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Zaktualizowano uwagi części {PartId} w naprawie {RepairId}", exploitationPartId, repairId);
        return Json(new { success = true, message = "Uwagi zostały zaktualizowane." });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd podczas aktualizacji uwag części {PartId} w naprawie {RepairId}", exploitationPartId, repairId);
        return Json(new { success = false, message = "Wystąpił błąd podczas aktualizacji uwag." });
    }
}

// Statystyki napraw - API endpoint
[HttpGet]
public async Task<IActionResult> GetRepairStatistics()
{
    try
    {
        var stats = new
        {
            TotalRepairs = await _context.Repairs.CountAsync(),
            CompletedRepairs = await _context.Repairs.CountAsync(r => r.Status == RepairStatus.Completed),
            InProgressRepairs = await _context.Repairs.CountAsync(r => r.Status == RepairStatus.InProgress),
            ScheduledRepairs = await _context.Repairs.CountAsync(r => r.Status == RepairStatus.Scheduled),
            TotalCost = await _context.Repairs.SumAsync(r => r.Cost),
            AverageCost = await _context.Repairs.AverageAsync(r => r.Cost),
            RepairsByType = await _context.Repairs
                .GroupBy(r => r.RepairType)
                .Select(g => new { Type = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(),
            RecentRepairs = await _context.Repairs
                .Include(r => r.Vehicle)
                .OrderByDescending(r => r.StartDate)
                .Take(5)
                .Select(r => new 
                { 
                    r.ID, 
                    r.StartDate, 
                    VehicleModel = r.Vehicle.Model,
                    r.Cost,
                    r.Status 
                })
                .ToListAsync()
        };

        return Json(stats);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd podczas pobierania statystyk napraw");
        return Json(new { error = "Błąd podczas pobierania statystyk" });
    }
}

// Wyszukiwanie napraw - API endpoint
[HttpGet]
public async Task<IActionResult> SearchRepairs(string? query, RepairStatus? status = null, RepairType? type = null)
{
    try
    {
        var repairsQuery = _context.Repairs
            .Include(r => r.Vehicle)
            .Include(r => r.RepairParts)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            repairsQuery = repairsQuery.Where(r => 
                r.Vehicle.Model.Contains(query) ||
                r.Vehicle.RegistrationNumber.Contains(query) ||
                r.Description.Contains(query) ||
                (r.InvoiceNumber != null && r.InvoiceNumber.Contains(query)));
        }

        if (status.HasValue)
        {
            repairsQuery = repairsQuery.Where(r => r.Status == status.Value);
        }

        if (type.HasValue)
        {
            repairsQuery = repairsQuery.Where(r => r.RepairType == type.Value);
        }

        var repairs = await repairsQuery
            .OrderByDescending(r => r.StartDate)
            .Take(50)
            .Select(r => new
            {
                r.ID,
                r.StartDate,
                VehicleModel = r.Vehicle.Model,
                VehicleRegistration = r.Vehicle.RegistrationNumber,
                r.RepairType,
                r.Status,
                r.Cost,
                PartsCount = r.RepairParts.Count
            })
            .ToListAsync();

        return Json(repairs);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd podczas wyszukiwania napraw");
        return Json(new { error = "Błąd podczas wyszukiwania" });
    }
}

// Eksport napraw do CSV
[HttpGet]
public async Task<IActionResult> ExportRepairsToCSV(DateTime? startDate = null, DateTime? endDate = null)
{
    try
    {
        var repairsQuery = _context.Repairs
            .Include(r => r.Vehicle)
            .Include(r => r.RepairParts)
                .ThenInclude(rp => rp.ExploitationPart)
            .AsQueryable();

        if (startDate.HasValue)
        {
            repairsQuery = repairsQuery.Where(r => r.StartDate >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            repairsQuery = repairsQuery.Where(r => r.StartDate <= endDate.Value);
        }

        var repairs = await repairsQuery
            .OrderByDescending(r => r.StartDate)
            .ToListAsync();

        var csv = GenerateCSVContent(repairs);
        var fileName = $"naprawy_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

        _logger.LogInformation("Eksportowano {Count} napraw do pliku CSV", repairs.Count);
        
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd podczas eksportu napraw do CSV");
        TempData["ErrorMessage"] = "Wystąpił błąd podczas eksportu danych.";
        return RedirectToAction(nameof(RepairsList));
    }
}

// Generowanie zawartości CSV
private static string GenerateCSVContent(List<Repair> repairs)
{
    var csvContent = new StringBuilder();
    
    csvContent.AppendLine("ID,Data naprawy,Pojazd,Numer rejestracyjny,Typ naprawy,Status,Koszt,Przebieg,Opis,Data rozpoczęcia,Data zakończenia,Numer faktury,Gwarancja (miesiące),Liczba części");

    foreach (var repair in repairs)
    {
        csvContent.AppendLine($"{repair.ID}," +
            $"\"{repair.Vehicle.Model}\"," +
            $"\"{repair.Vehicle.RegistrationNumber}\"," +
            $"\"{GetRepairTypeDisplayName(repair.RepairType)}\"," +
            $"\"{GetRepairStatusDisplayName(repair.Status)}\"," +
            $"{repair.Cost}," +
            $"{repair.MileageAtRepair}," +
            $"\"{repair.Description?.Replace("\"", "\"\"")}\"," +
            $"\"{repair.StartDate:yyyy-MM-dd}\"," +
            $"\"{repair.CompletionDate?.ToString("yyyy-MM-dd")}\"," +
            $"\"{repair.InvoiceNumber}\"," +
            $"{repair.RepairParts.Count}");
    }

    return csvContent.ToString();
}

// Metoda pomocnicza do przygotowania danych dla widoków
private async Task PrepareRepairViewData(int? selectedVehicleId = null)
{
    try
    {
        var vehicles = await GetAllVehiclesAsync();
        ViewBag.Vehicles = new SelectList(vehicles, "ID", "DisplayText", selectedVehicleId);

        var repairTypes = Enum.GetValues<RepairType>()
            .Cast<RepairType>()
            .Select(x => new { 
                Value = (int)x, 
                Text = GetRepairTypeDisplayName(x) 
            })
            .ToList();

        var repairStatuses = Enum.GetValues<RepairStatus>()
            .Cast<RepairStatus>()
            .Select(x => new { 
                Value = (int)x, 
                Text = GetRepairStatusDisplayName(x) 
            })
            .ToList();

        ViewBag.RepairTypes = new SelectList(repairTypes, "Value", "Text");
        ViewBag.RepairStatuses = new SelectList(repairStatuses, "Value", "Text");

        _logger.LogInformation("Przygotowano dane widoku: {VehicleCount} pojazdów, {TypeCount} typów napraw, {StatusCount} statusów", vehicles.Count, repairTypes.Count, repairStatuses.Count);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Błąd podczas przygotowania danych widoku");
        
        ViewBag.Vehicles = new SelectList(new List<object>(), "ID", "DisplayText");
        ViewBag.RepairTypes = new SelectList(new List<object>(), "Value", "Text");
        ViewBag.RepairStatuses = new SelectList(new List<object>(), "Value", "Text");
    }
}

// Metody pomocnicze do wyświetlania nazw enum-ów z obsługą Display attributes
private static string GetRepairTypeDisplayName(RepairType repairType)
{
    var displayAttribute = repairType.GetType()
        .GetField(repairType.ToString())
        ?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
        .FirstOrDefault() as System.ComponentModel.DataAnnotations.DisplayAttribute;

    if (displayAttribute != null)
    {
        return displayAttribute.Name ?? repairType.ToString();
    }

    return repairType switch
    {
        RepairType.Emergency => "Naprawa awaryjna",
        RepairType.Maintenance => "Przegląd okresowy",
        RepairType.Other => "Inne",
        _ => repairType.ToString()
    };
}

private static string GetRepairStatusDisplayName(RepairStatus status)
{
    var displayAttribute = status.GetType()
        .GetField(status.ToString())
        ?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
        .FirstOrDefault() as System.ComponentModel.DataAnnotations.DisplayAttribute;

    if (displayAttribute != null)
    {
        return displayAttribute.Name ?? status.ToString();
    }

    return status switch
    {
        RepairStatus.Scheduled => "Zaplanowana",
        RepairStatus.InProgress => "W trakcie",
        RepairStatus.Completed => "Zakończona",
        RepairStatus.Cancelled => "Anulowana",
        RepairStatus.WaitingForParts => "Oczekuje na części", 
        _ => status.ToString()
    };
}

private static string GetPartActionDisplayName(PartAction action)
{
    var displayAttribute = action.GetType()
        .GetField(action.ToString())
        ?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
        .FirstOrDefault() as System.ComponentModel.DataAnnotations.DisplayAttribute;

    if (displayAttribute != null)
    {
        return displayAttribute.Name ?? action.ToString();
    }

    return action switch
    {
        PartAction.Replaced => "Wymieniono",
        PartAction.Repaired => "Naprawiono",
        PartAction.Inspected => "Sprawdzono",
        _ => action.ToString()
    };
}

[HttpGet]
[Obsolete("Użyj RepairsList zamiast ServiceList")]
public IActionResult ServiceList()
{
    _logger.LogWarning("Użyto przestarzałej metody ServiceList, przekierowanie do RepairsList");
    return RedirectToAction(nameof(RepairsList));
}

[HttpGet]
[Obsolete("Użyj RepairAdd zamiast ServiceAdd")]
public IActionResult ServiceAdd(int vehicleId)
{
    _logger.LogWarning("Użyto przestarzałej metody ServiceAdd, przekierowanie do RepairAdd");
    return RedirectToAction(nameof(RepairAdd), new { vehicleId });
}

private async Task<Vehicle?> GetVehicleByIdAsync(int vehicleId)
{
    try
    {
        return await _context.Cars.FindAsync(vehicleId);
    }
    catch
    {
        try
        {
            var vehiclesProperty = _context.GetType().GetProperty("Vehicles");
            if (vehiclesProperty != null)
            {
                var vehiclesDbSet = vehiclesProperty.GetValue(_context) as IQueryable<Vehicle>;
                if (vehiclesDbSet != null)
                {
                    return await vehiclesDbSet.FirstOrDefaultAsync(v => v.ID == vehicleId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie można znaleźć pojazdu ID: {VehicleId}", vehicleId);
        }
    }
    return null;
}

private async Task<bool> VehicleExistsAsync(int vehicleId)
{
    try
    {
        return await _context.Cars.AnyAsync(v => v.ID == vehicleId);
    }
    catch
    {
        try
        {
            var vehiclesProperty = _context.GetType().GetProperty("Vehicles");
            if (vehiclesProperty != null)
            {
                var vehiclesDbSet = vehiclesProperty.GetValue(_context) as IQueryable<Vehicle>;
                if (vehiclesDbSet != null)
                {
                    return await vehiclesDbSet.AnyAsync(v => v.ID == vehicleId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie można sprawdzić istnienia pojazdu ID: {VehicleId}", vehicleId);
        }
    }
    return false;
}

private async Task<List<object>> GetAllVehiclesAsync()
{
    try
    {
        return await _context.Cars
            .Select(c => new { c.ID, DisplayText = $"{c.Model} - {c.RegistrationNumber}" })
            .Cast<object>()
            .ToListAsync();
    }
    catch
    {
        try
        {
            var vehiclesProperty = _context.GetType().GetProperty("Vehicles");
            if (vehiclesProperty != null)
            {
                var vehiclesDbSet = vehiclesProperty.GetValue(_context) as IQueryable<Vehicle>;
                if (vehiclesDbSet != null)
                {
                    return await vehiclesDbSet
                        .Select(c => new { c.ID, DisplayText = $"{c.Model} - {c.RegistrationNumber}" })
                        .Cast<object>()
                        .ToListAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nie można pobrać listy pojazdów");
        }
    }
    return new List<object>();
}
    
  }