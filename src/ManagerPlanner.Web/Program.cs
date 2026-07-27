using ManagerPlanner.Core.Data;
using ManagerPlanner.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// A DbContext factory, not a single injected scoped DbContext, per ADR-0002's
// warning to be deliberate about DbContext lifetime across a Blazor Server circuit.
builder.Services.AddDbContextFactory<PlanningDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("PlanningDatabase")));

var app = builder.Build();

// Apply any pending EF Core migrations before serving requests (ADR-0003 —
// replaces the legacy app's EnsureCreated()).
using (var scope = app.Services.CreateScope())
{
    var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PlanningDbContext>>();
    using var db = dbFactory.CreateDbContext();
    db.Database.Migrate();
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
