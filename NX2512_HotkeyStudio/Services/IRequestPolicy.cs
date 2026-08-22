using NXKeys.Protocol;

namespace NX2512_HotkeyStudio.Services
{
    /// <summary>
    /// Политика подписи и допуска запроса (модуль D): загрузка профиля, allowlist,
    /// client-проверка и HMAC-подпись. Не знает про файловую очередь — готовит
    /// аутентифицированный запрос.
    /// </summary>
    public interface IRequestPolicy
    {
        /// <summary>Текущий активный профиль (путь), используемый конфигуратором nxeskd.</summary>
        string ActiveProfilePath { get; }

        void ConfigureSecurity(string configPath);

        /// <summary>Подписать и допустить запрос (SessionId, Nonce, Sequence, PayloadHmac).</summary>
        void PrepareAuthenticated(NxCommandRequest request);
    }
}
