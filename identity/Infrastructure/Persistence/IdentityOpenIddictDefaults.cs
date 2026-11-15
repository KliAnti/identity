namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Имена протокольных объектов OpenIddict, которые должны совпадать при регистрации клиента,
/// выдаче токена и проверке токена API.
/// </summary>
public static class IdentityOpenIddictDefaults
{
    /// <summary>
    /// Scope, который клиент указывает в параметре <c>scope</c> запроса токена.
    /// </summary>
    /// <remarks>Сервер принимает этот scope и включает его в выданный access token.</remarks>
    public const string ApiScope = "api";

    /// <summary>
    /// Идентификатор API-получателя, для которого предназначен access token.
    /// </summary>
    /// <remarks>OpenIddict записывает это значение в claim <c>aud</c>; API использует его, чтобы отличить свои токены от токенов для других сервисов.</remarks>
    public const string ApiAudience = "va-api";

    /// <summary>
    /// Идентификатор мобильного/нативного приложения в OpenIddict.
    /// </summary>
    /// <remarks>Клиент передаёт это значение как <c>client_id</c>. Это публичное имя клиента, а не пароль или секрет.</remarks>
    public const string NativeClientId = "va-native";
}