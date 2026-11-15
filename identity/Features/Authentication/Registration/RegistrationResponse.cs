namespace Identity.Features.Authentication.Registration;

/// <summary>
/// Публичные данные созданной учётной записи, возвращаемые после успешной регистрации.
/// </summary>
/// <param name="Id">Сгенерированный идентификатор пользователя.</param>
/// <param name="Email">Нормализованный по краям, но сохраняемый с исходным регистром адрес электронной почты.</param>
/// <remarks>Ответ не содержит пароль, его хеш, соль или access token.</remarks>
public sealed record RegistrationResponse(Guid Id, string Email);