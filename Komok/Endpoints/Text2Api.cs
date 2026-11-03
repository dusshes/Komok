using Komok.Data;
using Komok.Models;
using Microsoft.EntityFrameworkCore;

namespace Komok.Endpoints;

public static class Text2Api
{
    public static RouteGroupBuilder MapText2Api(this IEndpointRouteBuilder endpoints)
    {
        var text2Api = endpoints.MapGroup("/api/text2").WithTags("Text2");

        text2Api.MapGet("/{id:int}", GetFioAsync)
            .WithName("GetFio");

        text2Api.MapPut("/{id:int}", UpdateFioAsync)
            .WithName("UpdateFio");

        return text2Api;
    }

    private static async Task<IResult> GetFioAsync(int id, KomokDbContext db)
    {
        var record = await db.Text2Records.FindAsync(id);
        return record is null
            ? Results.NotFound()
            : Results.Ok(new { fio = record.Fio });
    }

    private static async Task<IResult> UpdateFioAsync(int id, FioUpdate update, KomokDbContext db)
    {
        if (string.IsNullOrWhiteSpace(update.Fio) || update.Fio.Trim().Length > 500)
            return Results.BadRequest(new { message = "Поле fio должно содержать от 1 до 500 символов." });

        var record = await db.Text2Records.FindAsync(id);
        if (record is null)
            return Results.NotFound();

        record.Fio = update.Fio.Trim();
        await db.SaveChangesAsync();

        return Results.Ok(new { fio = record.Fio });
    }
}
