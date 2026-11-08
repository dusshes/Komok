using Komok.Contracts;
using Komok.Data;
using Komok.Models;
using Komok.Security;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Komok.Endpoints;

public static class AuthApi
{
    public static IEndpointRouteBuilder MapAuthApi(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth").WithTags("Account");
        auth.MapPost("/register", RegisterAsync).WithName("Register");
        auth.MapPost("/confirm-email", ConfirmEmailAsync).WithName("ConfirmEmail");
        auth.MapPost("/login", LoginAsync).WithName("Login");
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization().WithName("Logout");
        auth.MapGet("/me", GetMeAsync).RequireAuthorization().WithName("GetCurrentUser");
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        HttpContext http,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        KomokDbContext db,
        IConfiguration configuration,
        IWebHostEnvironment environment,
        CancellationToken cancellationToken)
    {
        if ((!environment.IsDevelopment())
            && (string.IsNullOrWhiteSpace(configuration["Smtp:Host"])
                || string.IsNullOrWhiteSpace(configuration["Smtp:From"])))
            return Results.Problem("Регистрация временно недоступна: не настроена отправка email.", statusCode: 503);
        var inputErrors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 160)
            inputErrors.Add(("name", "Укажите имя длиной не более 160 символов."));
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256)
            inputErrors.Add(("email", "Укажите email длиной не более 256 символов."));
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            inputErrors.Add(("confirmPassword", "Пароль и подтверждение не совпадают."));
        if (string.IsNullOrWhiteSpace(request.MessengerContact) || request.MessengerContact.Length > 200)
            inputErrors.Add(("messengerContact", "Укажите контакт в мессенджере длиной не более 200 символов."));
        if (!string.IsNullOrWhiteSpace(request.PayoutDetails) && request.PayoutDetails.Length > 1000)
            inputErrors.Add(("payoutDetails", "Реквизиты должны содержать не более 1000 символов."));
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 128)
            inputErrors.Add(("password", "Пароль должен содержать от 10 до 128 символов."));
        if (!new[] { "Telegram", "WhatsApp", "MAX", "Zalo" }.Contains(request.Messenger, StringComparer.Ordinal))
            inputErrors.Add(("messenger", "Выберите мессенджер из списка."));
        if (inputErrors.Count > 0)
            return ValidationErrors.BadRequest(inputErrors.ToArray());

        var normalizedEmail = request.Email.Trim();
        if (await users.FindByEmailAsync(normalizedEmail) is not null)
            return ValidationErrors.BadRequest(("email", "Пользователь с таким email уже зарегистрирован."));

        var user = new ApplicationUser
        {
            Name = request.Name.Trim(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            Messenger = request.Messenger,
            MessengerContact = request.MessengerContact.Trim(),
            PayoutDetails = request.PayoutDetails?.Trim() ?? ""
        };

        var created = await users.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var identityError in created.Errors)
            {
                var (field, message) = LocalizeIdentityError(identityError.Code);
                if (!errors.TryGetValue(field, out var messages))
                    errors[field] = messages = [];
                messages.Add(message);
            }

            return ValidationErrors.BadRequest(errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()));
        }

        var role = Roles.Seller;
        if (!await roles.RoleExistsAsync(role))
            await roles.CreateAsync(new IdentityRole(role));
        await users.AddToRoleAsync(user, role);

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var baseUrl = $"{http.Request.Scheme}://{http.Request.Host}/";
        var confirmationUrl = QueryHelpers.AddQueryString(baseUrl, new Dictionary<string, string?>
        {
            ["confirmEmail"] = normalizedEmail,
            ["token"] = token
        });
        db.EmailOutbox.Add(new EmailOutboxMessage
        {
            Recipient = normalizedEmail,
            Subject = "Подтверждение регистрации Komok",
            Body = $"Чтобы подтвердить email и завершить регистрацию, откройте ссылку:\n{confirmationUrl}",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Accepted(value: new { message = "Проверьте почту и перейдите по ссылке для подтверждения email." });
    }

    private static (string Field, string Message) LocalizeIdentityError(string code) => code switch
    {
        "PasswordRequiresNonAlphanumeric" => ("password", "Добавьте в пароль специальный символ"),
        "PasswordRequiresLower" => ("password", "Добавьте строчную латинскую букву"),
        "PasswordRequiresUpper" => ("password", "Добавьте заглавную латинскую букву"),
        "PasswordRequiresDigit" => ("password", "Добавьте цифру в пароль."),
        "PasswordTooShort" => ("password", "Пароль должен содержать не менее 10 символов."),
        "InvalidEmail" => ("email", "Введите корректный email."),
        "DuplicateEmail" or "DuplicateUserName" => ("email", "Пользователь с таким email уже зарегистрирован."),
        _ => ("password", "Пароль не соответствует требованиям безопасности.")
    };

    private static async Task<IResult> ConfirmEmailAsync(
        ConfirmEmailRequest request,
        UserManager<ApplicationUser> users,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token))
            return Results.BadRequest(new { message = "Ссылка подтверждения недействительна." });
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return Results.BadRequest(new { message = "Ссылка подтверждения недействительна." });

        var result = await users.ConfirmEmailAsync(user, request.Token);
        return result.Succeeded
            ? Results.Ok(new { message = "Email подтверждён. Теперь можно войти." })
            : Results.BadRequest(new { message = "Ссылка подтверждения недействительна или устарела." });
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        SignInManager<ApplicationUser> signIn,
        UserManager<ApplicationUser> users)
    {
        var inputErrors = new List<(string Field, string Message)>();
        if (string.IsNullOrWhiteSpace(request.Email))
            inputErrors.Add(("email", "Укажите email."));
        if (string.IsNullOrWhiteSpace(request.Password))
            inputErrors.Add(("password", "Укажите пароль."));
        if (inputErrors.Count > 0)
            return ValidationErrors.BadRequest(inputErrors.ToArray());
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            return Results.Unauthorized();

        var result = await signIn.PasswordSignInAsync(user, request.Password, request.RememberMe, lockoutOnFailure: true);
        return result.Succeeded
            ? Results.Ok(new { message = "Вход выполнен." })
            : Results.Unauthorized();
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signIn)
    {
        await signIn.SignOutAsync();
        return Results.Ok(new { message = "Вы вышли из аккаунта." });
    }

    private static async Task<IResult> GetMeAsync(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        KomokDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
            return Results.Unauthorized();

        var roles = await users.GetRolesAsync(user);
        var storeIds = await db.StoreAdministrators.AsNoTracking()
            .Where(link => link.UserId == user.Id)
            .Select(link => link.StoreId)
            .ToListAsync(cancellationToken);

        return Results.Ok(new
        {
            user.Name,
            user.Email,
            user.Messenger,
            user.MessengerContact,
            IsSeller = roles.Contains(Roles.Seller),
            IsStoreAdmin = roles.Contains(Roles.StoreAdmin),
            IsSuperAdmin = roles.Contains(Roles.SuperAdmin),
            StoreIds = storeIds
        });
    }
}
