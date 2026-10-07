using BcReleasePlanPortal.Data;
using BcReleasePlanPortal.Publishing;
using BcReleasePlanPortal.Web.Components;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBcReleasePlanData(builder.Configuration);
builder.Services.Configure<PublishingOptions>(builder.Configuration.GetSection(PublishingOptions.SectionName));
builder.Services.AddSingleton<ReleasePlanService>();

var app = builder.Build();

// Same SQLite file the ingest Worker writes to (design doc: read-only view over the
// ingested data). Applying migrations here too means the UI works standalone even if it's
// started before the Worker has ever run.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<BcReleasePlanDbContext>();
    await db.Database.MigrateAsync();
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

// Word downloads are plain GET routes: an interactive Blazor component can't stream a file
// to the browser without JavaScript interop, and a link works everywhere.
const string WordMime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

app.MapGet("/customers/{customerId:guid}/release-plan/preview", async (Guid customerId, string? period, ReleasePlanService plans) =>
{
    var file = await plans.PreviewAsync(customerId, string.IsNullOrWhiteSpace(period) ? PlanRules.DefaultPeriod(DateOnly.FromDateTime(DateTime.Today)) : period);
    return file is null ? Results.NotFound() : Results.File(file.Content, WordMime, file.FileName);
});

app.MapGet("/release-plans/{planId:guid}", async (Guid planId, ReleasePlanService plans) =>
{
    var file = await plans.DownloadAsync(planId);
    return file is null ? Results.NotFound() : Results.File(file.Content, WordMime, file.FileName);
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
