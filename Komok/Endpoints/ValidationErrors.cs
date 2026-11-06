namespace Komok.Endpoints;

internal static class ValidationErrors
{
    public static IResult BadRequest(params (string Field, string Message)[] errors) =>
        Results.BadRequest(new
        {
            errors = errors
                .GroupBy(error => error.Field)
                .ToDictionary(group => group.Key, group => group.Select(error => error.Message).ToArray())
        });

    public static IResult BadRequest(IReadOnlyDictionary<string, string[]> errors) =>
        Results.BadRequest(new { errors });
}
