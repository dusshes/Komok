using Komok.Data;
using Komok.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("KomokDatabase");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Set ConnectionStrings:KomokDatabase in an appsettings file.");
}

builder.Services.AddDbContext<KomokDbContext>(options => options.UseNpgsql(connectionString));
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KomokDbContext>();
    await db.Database.MigrateAsync();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/text", async (KomokDbContext db) =>
{
    var record = await db.Texts.SingleOrDefaultAsync();
    return record is null
        ? Results.Problem("В таблице Text пока нет строки.")
        : Results.Ok(new { txt = record.Txt });
});

app.MapPut("/api/text", async (TextUpdate update, KomokDbContext db) =>
{
    var newValue = update.Txt.Trim();
    if (string.IsNullOrWhiteSpace(newValue) || newValue.Length > 500)
        return Results.BadRequest(new { message = "Значение должно содержать от 1 до 500 символов." });

    var record = await db.Texts.SingleOrDefaultAsync();
    if (record is null)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Text\" (\"Txt\") VALUES ({newValue})");
    }
    else
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Text\" SET \"Txt\" = {newValue}");
    }

    return Results.Ok(new { txt = newValue });
});

app.Run();
