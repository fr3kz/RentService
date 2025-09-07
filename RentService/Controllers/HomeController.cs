using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentService.Models;
using RentService.Models.VehicleModule;
using RentService.Database;
using RentService.Models.ViewModels;

namespace RentService.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(AppDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            // Pobierz podstawowe statystyki
            var totalCars = await _context.Cars.CountAsync();
            var availableCars = await _context.Cars.CountAsync(v => v.IsAvailable);
            var totalEmployees = await _context.Employees.CountAsync();
            
            // Pojazdy w serwisie - te które mają aktywne naprawy
            var CarsInService = await _context.Repairs
                .Where(r => r.Status == RepairStatus.InProgress || r.Status == RepairStatus.WaitingForParts)
                .Select(r => r.VehicleID)
                .Distinct()
                .CountAsync();

            // Ostatnie naprawy (5 najnowszych)
            var recentRepairs = await _context.Repairs
                .Include(r => r.Vehicle)
                .OrderByDescending(r => r.RepairDate)
                .Take(5)
                .ToListAsync();

            // Lista pojazdów dla przeglądu (10 najnowszych)
            var vehicleList = await _context.Cars
                .OrderBy(v => v.Model)
                .Take(10)
                .ToListAsync();

            // Powiadomienia o serwisie - części wymagające wymiany w ciągu 30 dni
            var maintenanceAlerts = await _context.ExploitationParts
                .Where(ep => ep.NextReplacementDueDate <= DateTime.Now.AddDays(30) 
                          && ep.NextReplacementDueDate >= DateTime.Now
                          && ep.PartCondition != Condition.New)
                .OrderBy(ep => ep.NextReplacementDueDate)
                .ToListAsync();

            // Dane dla wykresów
            var repairCostData = await GetRepairCostData();

            // Przekaż dane do widoku
            ViewBag.TotalCars = totalCars;
            ViewBag.AvailableCars = availableCars;
            ViewBag.CarsInService = CarsInService;
            ViewBag.TotalEmployees = totalEmployees;
            ViewBag.RecentRepairs = recentRepairs;
            ViewBag.VehicleList = vehicleList;
            ViewBag.MaintenanceAlerts = maintenanceAlerts;
            ViewBag.RepairCostData = repairCostData;

            return View();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas ładowania dashboardu");
            
            // Jeśli wystąpił błąd, przekaż podstawowe dane
            ViewBag.TotalCars = 0;
            ViewBag.AvailableCars = 0;
            ViewBag.CarsInService = 0;
            ViewBag.TotalEmployees = 0;
            ViewBag.RecentRepairs = new List<Repair>();
            ViewBag.VehicleList = new List<Vehicle>();
            ViewBag.MaintenanceAlerts = new List<ExploitationPart>();
            
            return View();
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // API endpoints dla AJAX calls
    [HttpGet]
    public async Task<IActionResult> GetCarstatistics()
    {
        try
        {
            var stats = new
            {
                TotalCars = await _context.Cars.CountAsync(),
                AvailableCars = await _context.Cars.CountAsync(v => v.IsAvailable),
                RentedCars = await _context.Cars.CountAsync(v => !v.IsAvailable),
                CarsInService = await _context.Repairs
                    .Where(r => r.Status == RepairStatus.InProgress || r.Status == RepairStatus.WaitingForParts)
                    .Select(r => r.VehicleID)
                    .Distinct()
                    .CountAsync(),
                AverageAge = await GetAverageFleetAge(),
                TotalMileage = await _context.Cars.SumAsync(v => (double)v.Mileage)
            };

            return Json(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania statystyk pojazdów");
            return Json(new { error = "Błąd serwera" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRepairStatistics()
    {
        try
        {
            var currentMonth = DateTime.Now;
            var lastMonth = currentMonth.AddMonths(-1);

            var currentMonthRepairs = await _context.Repairs
                .Where(r => r.RepairDate.Month == currentMonth.Month && r.RepairDate.Year == currentMonth.Year)
                .CountAsync();

            var lastMonthRepairs = await _context.Repairs
                .Where(r => r.RepairDate.Month == lastMonth.Month && r.RepairDate.Year == lastMonth.Year)
                .CountAsync();

            var currentMonthCost = await _context.Repairs
                .Where(r => r.RepairDate.Month == currentMonth.Month && 
                           r.RepairDate.Year == currentMonth.Year &&
                           r.Status == RepairStatus.Completed)
                .SumAsync(r => r.Cost);

            var lastMonthCost = await _context.Repairs
                .Where(r => r.RepairDate.Month == lastMonth.Month && 
                           r.RepairDate.Year == lastMonth.Year &&
                           r.Status == RepairStatus.Completed)
                .SumAsync(r => r.Cost);

            var stats = new
            {
                CurrentMonthRepairs = currentMonthRepairs,
                LastMonthRepairs = lastMonthRepairs,
                CurrentMonthCost = currentMonthCost,
                LastMonthCost = lastMonthCost,
                RepairTrend = lastMonthRepairs == 0 ? 0 : ((currentMonthRepairs - lastMonthRepairs) * 100.0 / lastMonthRepairs),
                CostTrend = lastMonthCost == 0 ? 0 : ((double)(currentMonthCost - lastMonthCost) * 100.0 / (double)lastMonthCost)
            };

            return Json(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania statystyk napraw");
            return Json(new { error = "Błąd serwera" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetMaintenanceAlerts()
    {
        try
        {
            var alerts = await _context.ExploitationParts
                .Include(ep => ep.Car)
                .Where(ep => ep.NextReplacementDueDate <= DateTime.Now.AddDays(30) && 
                           ep.NextReplacementDueDate >= DateTime.Now)
                .OrderBy(ep => ep.NextReplacementDueDate)
                .Select(ep => new
                {
                    ep.ID,
                    ep.PartName,
                    Vehicle = new
                    {
                        ep.Car.Model,
                        ep.Car.RegistrationNumber
                    },
                    ep.NextReplacementDueDate,
                    ep.PartCondition,
                    DaysUntilDue = (ep.NextReplacementDueDate - DateTime.Now).Days
                })
                .ToListAsync();

            return Json(alerts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania alertów serwisowych");
            return Json(new { error = "Błąd serwera" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetFleetUtilization()
    {
        try
        {
            var totalCars = await _context.Cars.CountAsync();
            var availableCars = await _context.Cars.CountAsync(v => v.IsAvailable);
            
            if (totalCars == 0)
            {
                return Json(new { utilizationRate = 0 });
            }

            var utilizationRate = ((totalCars - availableCars) * 100.0) / totalCars;

            var result = new
            {
                UtilizationRate = Math.Round(utilizationRate, 1),
                TotalCars = totalCars,
                InUse = totalCars - availableCars,
                Available = availableCars
            };

            return Json(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas obliczania wykorzystania floty");
            return Json(new { error = "Błąd serwera" });
        }
    }

    [HttpPost]
    public async Task<IActionResult> MarkMaintenanceComplete(int partId)
    {
        try
        {
            var part = await _context.ExploitationParts.FindAsync(partId);
            if (part == null)
            {
                return Json(new { success = false, message = "Nie znaleziono części" });
            }

            part.LastReplacementDate = DateTime.Now;
            part.NextReplacementDueDate = DateTime.Now.AddDays(part.TotalKm / 365); // Przykładowe wyliczenie
            part.PartCondition = Condition.New;
            part.CurrentKm = 0;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Serwis został oznaczony jako ukończony" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Błąd podczas oznaczania serwisu jako ukończony dla części {partId}");
            return Json(new { success = false, message = "Błąd serwera" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetRecentActivity(int count = 10)
    {
        try
        {
            var activities = new List<ActivityItem>();

            // Pobierz naprawy
            var recentRepairs = await _context.Repairs
                .Include(r => r.Vehicle)
                .OrderByDescending(r => r.RepairDate)
                .Take(count)
                .ToListAsync();

            // Dodaj naprawy do listy aktywności
            activities.AddRange(recentRepairs.Select(r => new ActivityItem
            {
                Type = "repair",
                Title = $"Naprawa: {r.Vehicle.Model} ({r.Vehicle.RegistrationNumber})",
                Description = r.Description.Length > 50 ? r.Description.Substring(0, 50) + "..." : r.Description,
                Date = r.RepairDate,
                Status = GetRepairStatusText(r.Status),
                Cost = r.Cost,
                Icon = "fas fa-wrench",
                Color = GetRepairStatusColor(r.Status)
            }));

            // Pobierz pracowników
            var recentEmployees = await _context.Employees
                .OrderByDescending(e => e.CreatedAt)
                .Take(count / 2)
                .ToListAsync();

            // Dodaj pracowników do listy aktywności
            activities.AddRange(recentEmployees.Select(e => new ActivityItem
            {
                Type = "employee",
                Title = $"Nowy pracownik: {e.FirstName} {e.LastName}",
                Description = $"Zatrudniony: {e.HireDate:dd.MM.yyyy}",
                Date = e.CreatedAt,
                Status = "Aktywny",
                Cost = null,
                Icon = "fas fa-user-plus",
                Color = "text-blue-400"
            }));

            // Posortuj po dacie i ogranicz wyniki
            var sortedActivity = activities
                .OrderByDescending(a => a.Date)
                .Take(count)
                .ToList();

            return Json(sortedActivity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania ostatniej aktywności");
            return Json(new { error = "Błąd serwera" });
        }
    }

    private string GetRepairStatusText(RepairStatus status)
    {
        return status switch
        {
            RepairStatus.Scheduled => "Zaplanowana",
            RepairStatus.InProgress => "W trakcie",
            RepairStatus.Completed => "Zakończona",
            RepairStatus.Cancelled => "Anulowana",
            RepairStatus.WaitingForParts => "Oczekuje na części",
            _ => "Nieznany"
        };
    }

    private string GetRepairStatusColor(RepairStatus status)
    {
        return status switch
        {
            RepairStatus.Completed => "text-green-400",
            RepairStatus.InProgress => "text-yellow-400",
            RepairStatus.Cancelled => "text-red-400",
            RepairStatus.WaitingForParts => "text-orange-400",
            RepairStatus.Scheduled => "text-blue-400",
            _ => "text-gray-400"
        };
    }

    // Metody pomocnicze
    private async Task<object> GetRepairCostData()
    {
        try
        {
            var sixMonthsAgo = DateTime.Now.AddMonths(-6);
            
            var monthlyCosts = await _context.Repairs
                .Where(r => r.RepairDate >= sixMonthsAgo && r.Status == RepairStatus.Completed)
                .GroupBy(r => new { r.RepairDate.Year, r.RepairDate.Month })
                .Select(g => new
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalCost = g.Sum(r => r.Cost)
                })
                .OrderBy(g => g.Year).ThenBy(g => g.Month)
                .ToListAsync();

            // Wypełnij brakujące miesiące zerami
            var result = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var date = DateTime.Now.AddMonths(-i);
                var monthData = monthlyCosts.FirstOrDefault(mc => mc.Year == date.Year && mc.Month == date.Month);
                
                result.Add(new
                {
                    Month = date.ToString("MMM", new System.Globalization.CultureInfo("pl-PL")),
                    Cost = monthData?.TotalCost ?? 0
                });
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas pobierania danych o kosztach napraw");
            return new List<object>();
        }
    }

    private async Task<double> GetAverageFleetAge()
    {
        try
        {
            var Cars = await _context.Cars.ToListAsync();
            if (!Cars.Any())
                return 0;

            var totalAge = Cars.Sum(v => (DateTime.Now - v.YearOfManufacture).TotalDays / 365.25);
            return Math.Round(totalAge / Cars.Count, 1);
        }
        catch
        {
            return 0;
        }
    }
}