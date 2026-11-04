using Microsoft.EntityFrameworkCore;
using PrimeVideoApp.Data;
using PrimeVideoApp.Hubs;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PrimeVideoDB")
    ?? throw new InvalidOperationException("No se encontró la cadena de conexión PrimeVideoDB.");

builder.Services.AddDbContext<PrimeVideoDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Content}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<ContentHub>("/contentHub");

app.Run();
