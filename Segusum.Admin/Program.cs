using Microsoft.EntityFrameworkCore;
using Seg;
using Segusum.Admin;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Segusum")
    ?? Environment.GetEnvironmentVariable("SEGUSUM_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(connection))
    throw new InvalidOperationException("Configure ConnectionStrings:Segusum or SEGUSUM_CONNECTION_STRING.");

builder.Services.AddDbContextFactory<segusumDb>(options => options.UseSqlServer(connection));
builder.Services.AddScoped<AdminDataService>();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseExceptionHandler("/error");
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<Segusum.Admin.Components.App>().AddInteractiveServerRenderMode();
app.Run();
