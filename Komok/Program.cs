using Komok.Data;
using Komok.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("KomokTest");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("Set ConnectionStrings:KomokTest in appsettings.json or an environment-specific appsettings file.");
}

builder.Services.AddDbContext<TestDbContext>(options => options.UseNpgsql(connectionString));
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TestDbContext>();
    await db.Database.EnsureCreatedAsync();
    if (!await db.Tests.AnyAsync())
    {
        await db.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"Test\" (\"Txt\") VALUES ('ТестТест')");
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/test", async (TestDbContext db) =>
{
    var record = await db.Tests.SingleOrDefaultAsync();
    return record is null
        ? Results.Problem("В таблице Test пока нет строки.")
        : Results.Ok(new { txt = record.Txt });
});

app.MapPut("/api/test", async (TestValueUpdate update, TestDbContext db) =>
{
    var newValue = update.Txt.Trim();
    if (string.IsNullOrWhiteSpace(newValue) || newValue.Length > 500)
        return Results.BadRequest(new { message = "Значение должно содержать от 1 до 500 символов." });

    var record = await db.Tests.SingleOrDefaultAsync();
    if (record is null)
    {
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO \"Test\" (\"Txt\") VALUES ({newValue})");
    }
    else
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Test\" SET \"Txt\" = {newValue}");
    }

    return Results.Ok(new { txt = newValue });
});

app.Run();
