namespace Identity.Features.Authentication.Registration;

/// <summary>
/// Данные, необходимые для создания пользовательской учётной записи.
/// </summary>
/// <param name="Email">Адрес электронной почты; обрезается по краям и должен быть валидным, длиной не более 256 символов.</param>
/// <param name="Password">Пароль в открытом виде только в HTTPS-запросе; он сохраняется в базе исключительно как хеш.</param>
public sealed record RegistrationRequest(string? Email, string? Password);