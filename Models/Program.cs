using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GBC_Ticketing.Web.Models;
using GBC_Ticketing.Web.Services;
using GBC_Ticketing.Web.Middleware;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File(
        path: "wwwroot/logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Console()
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddControllersWithViews()
    .AddNewtonsoftJson()
    .AddRazorRuntimeCompilation();
builder.Services.AddRazorPages(); 

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.FromMinutes(30);
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
});

builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    
    db.Database.Migrate();
    
    string[] roles = { "Admin", "Organizer", "Attendee" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
    
    var adminEmail = "admin@gbc.com";
    var adminPassword = "Admin123!";
    var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
    if (existingAdmin == null)
    {
        var adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Administrator",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(adminUser, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(adminUser, "Admin");
    }
    
    var organizerEmail = "organizer@gbc.com";
    var organizerPassword = "Organizer123!";
    var existingOrganizer = await userManager.FindByEmailAsync(organizerEmail);
    if (existingOrganizer == null)
    {
        var organizerUser = new ApplicationUser
        {
            UserName = organizerEmail,
            Email = organizerEmail,
            FullName = "Event Organizer",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(organizerUser, organizerPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(organizerUser, "Organizer");
    }
    
    var attendeeEmail = "attendee@gbc.com";
    var attendeePassword = "Attendee123!";
    var existingAttendee = await userManager.FindByEmailAsync(attendeeEmail);
    if (existingAttendee == null)
    {
        var attendeeUser = new ApplicationUser
        {
            UserName = attendeeEmail,
            Email = attendeeEmail,
            FullName = "Test Attendee",
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(attendeeUser, attendeePassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(attendeeUser, "Attendee");
    }
    
    if (!await db.Categories.AnyAsync())
    {
        var defaultCategories = new[]
        {
            new Category { Name = "Music", Description = "Concerts and live music" },
            new Category { Name = "Sports", Description = "Sporting events" },
            new Category { Name = "Theater", Description = "Plays and musicals" },
            new Category { Name = "Comedy", Description = "Comedy shows" },
            new Category { Name = "Conference", Description = "Conferences and seminars" }
        };
        await db.Categories.AddRangeAsync(defaultCategories);
        await db.SaveChangesAsync();
    }
    
    Log.Information("=== Test Accounts ===");
    Log.Information("Admin: admin@gbc.com / Admin123!");
    Log.Information("Organizer: organizer@gbc.com / Organizer123!");
    Log.Information("Attendee: attendee@gbc.com / Attendee123!");
}

app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error", "?statusCode={0}");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuthenticationLoggingMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Event}/{action=Index}/{id?}");
app.MapRazorPages();

Log.Information("Application starting up");
app.Run();
