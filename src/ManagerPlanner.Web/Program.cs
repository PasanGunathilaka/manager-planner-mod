using ManagerPlanner.Core.Data;
using ManagerPlanner.Core.Domain;
using ManagerPlanner.Core.Services;
using ManagerPlanner.Web.Components;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMudServices();

// A DbContext factory, not a single injected scoped DbContext, per ADR-0002's
// warning to be deliberate about DbContext lifetime across a Blazor Server circuit.
builder.Services.AddDbContextFactory<PlanningDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PlanningDatabase")));

builder.Services.AddScoped<PlanningService>();

var app = builder.Build();

// Apply any pending EF Core migrations before serving requests (ADR-0003 —
// replaces the legacy app's EnsureCreated()).
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PlanningDbContext>>();
    using var db = dbFactory.CreateDbContext();
    db.Database.Migrate();

    // Stand-in for real authentication/multi-user support (not yet decided, ADR-0001):
    // guarantee exactly one Manager user exists so AddProjectAsync's required ownerId is
    // always resolvable without full DbSeeder-style sample data (item 11).
    if (!db.Users.Any(u => u.Role == UserRole.Manager))
    {
        db.Users.Add(new User { FullName = "Manager", Email = "manager@example.com", Role = UserRole.Manager });
        db.SaveChanges();
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
