using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;


using RentService.Database;
using RentService.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("Data Source=rentservice.db")
    //  options.UseSqlite("Data Source=/var/www/RentService/RentService/rentservice.db")

);

builder.Services.Configure<RazorViewEngineOptions>(options =>
{
    options.AreaViewLocationFormats.Add("/Views/Shared/{0}.cshtml");
});

builder.Services.AddIdentity<User, IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options => {
    options.LoginPath = "/Users/Login";
    options.LogoutPath = "/Users/Logout";
    options.AccessDeniedPath = "/Users/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(24);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireAuthenticatedUser()
            .RequireClaim("IsAdmin", "True"));
});
builder.Services.AddControllersWithViews(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});

builder.Services.AddCoreAdmin();


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();  // Tworzy wszystkie tabele jeśli nie istnieją
}

await SeedAdminUser(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();


static async Task SeedAdminUser(IHost app)
{
    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    

    string email = "tektmap1@gmail.com";
    string password = "Admin1234!.";

    if (await userManager.FindByEmailAsync(email) == null)
    {
        var user = new User
        {
            UserName = email,
            Email = email,
            Imie = "Admin",
            Nazwisko = "Konto",
            IsAdmin = true
        };

        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            Console.WriteLine("✅ Admin user created.");
        }
        else
        {
            Console.WriteLine("❌ Failed to create admin:");
            foreach (var error in result.Errors)
                Console.WriteLine($" - {error.Description}");
        }
    }
    else
    {
        Console.WriteLine("ℹ️ Admin user already exists.");
    }
}
/*
 * sudo systemctl daemon-reload
sudo systemctl enable rentservice
sudo systemctl start rentservice
sudo systemctl status rentservice

sudo mv /var/www/RentService/RentService/rentservice.db /var/www/RentService/RentService/out/
1️⃣ Wejdź do katalogu repozytorium
cd /var/www/RentService/RentService


Jeśli wcześniej git pokazywał błąd “dubious ownership”, dodaj katalog jako bezpieczny:

sudo git config --global --add safe.directory /var/www/RentService

2️⃣ Pobierz zmiany z repo
git pull origin main


Upewnij się, że w main jest aktualna wersja. Możesz zmienić main na swoją gałąź.

3️⃣ Skompiluj nową DLL
dotnet publish -c Release -o out


-c Release – kompiluje w trybie produkcyjnym.

-o out – zapisuje wynik w folderze out (tam, gdzie systemd oczekuje DLL).

4️⃣ Zrestartuj usługę systemd
sudo systemctl restart rentservice

5️⃣ Sprawdź status
sudo systemctl status rentservice


Jeśli jest active (running) – aplikacja działa z nową wersją.

Jeśli są błędy – sprawdź logi:

journalctl -u rentservice -f  

*/