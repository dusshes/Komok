using Komok.Contracts;
using Komok.Data;
using Komok.Models;
using Komok.Security;
using Microsoft.EntityFrameworkCore;

namespace Komok.Endpoints;

public static class CategoryApi
{
    public static IEndpointRouteBuilder MapCategoryApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/categories/active", GetActiveAsync).WithTags("Categories");

        var admin = endpoints.MapGroup("/api/admin/categories")
            .RequireAuthorization(policy => policy.RequireRole(Roles.SuperAdmin))
            .WithTags("Category administration");
        admin.MapGet("/", GetAllAsync);
        admin.MapPost("/", CreateAsync);
        admin.MapPut("/{id:int}", UpdateAsync);

        return endpoints;
    }

    private static async Task<IResult> GetActiveAsync(KomokDbContext db, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking()
            .Where(category => category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new { category.Id, category.Name, category.IsActive })
            .ToListAsync(cancellationToken);
        return Results.Ok(categories);
    }

    private static async Task<IResult> GetAllAsync(KomokDbContext db, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking()
            .OrderBy(category => category.Name)
            .Select(category => new { category.Id, category.Name, category.IsActive })
            .ToListAsync(cancellationToken);
        return Results.Ok(categories);
    }

    private static async Task<IResult> CreateAsync(
        ProductCategoryUpsertRequest request,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return ValidationErrors.BadRequest(("name", "Введите название категории длиной не более 120 символов."));

        var normalizedName = Normalize(name);
        if (await db.Categories.AnyAsync(category => category.NormalizedName == normalizedName, cancellationToken))
            return ValidationErrors.BadRequest(("name", "Категория с таким названием уже есть."));

        var category = new ProductCategory
        {
            Name = name,
            NormalizedName = normalizedName,
            IsActive = true
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/admin/categories/{category.Id}", new { category.Id, category.Name, category.IsActive });
    }

    private static async Task<IResult> UpdateAsync(
        int id,
        ProductCategoryUpsertRequest request,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (category is null) return Results.NotFound();

        var name = request.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
            return ValidationErrors.BadRequest(("name", "Введите название категории длиной не более 120 символов."));

        var normalizedName = Normalize(name);
        if (await db.Categories.AnyAsync(item => item.Id != id && item.NormalizedName == normalizedName, cancellationToken))
            return ValidationErrors.BadRequest(("name", "Категория с таким названием уже есть."));

        category.Name = name;
        category.NormalizedName = normalizedName;
        category.IsActive = request.IsActive;
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { category.Id, category.Name, category.IsActive });
    }

    private static string Normalize(string name) => name.Trim().ToUpperInvariant();
}
