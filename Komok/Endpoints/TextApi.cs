using Komok.Data;
using Komok.Models;
using Microsoft.EntityFrameworkCore;

namespace Komok.Endpoints;

public static class TextApi
{
    public static RouteGroupBuilder MapTextApi(this IEndpointRouteBuilder endpoints)
    {
        var textApi = endpoints.MapGroup("/api/text").WithTags("Text");

        textApi.MapGet("/", GetTextAsync)
            .WithName("GetText");

        textApi.MapPut("/", UpdateTextAsync)
            .WithName("UpdateText");

        return textApi;
    }

    private static async Task<IResult> GetTextAsync(KomokDbContext db)
    {
        var record = await db.Texts.SingleOrDefaultAsync();
        return record is null
            ? Results.Problem("В таблице Text пока нет строки.")
            : Results.Ok(new { txt = record.Txt });
    }

    private static async Task<IResult> UpdateTextAsync(TextUpdate update, KomokDbContext db)
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
    }
}
