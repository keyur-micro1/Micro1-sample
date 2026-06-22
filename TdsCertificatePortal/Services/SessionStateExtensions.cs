using System.Text.Json;
using TdsCertificatePortal.Models;

namespace TdsCertificatePortal.Services;

public static class SessionStateExtensions
{
    private const string SessionKey = "TdsCertificatePortal.State";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static PortalSessionState GetPortalState(this ISession session)
    {
        var json = session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new PortalSessionState();
        }

        return JsonSerializer.Deserialize<PortalSessionState>(json, JsonOptions) ?? new PortalSessionState();
    }

    public static void SetPortalState(this ISession session, PortalSessionState state)
    {
        session.SetString(SessionKey, JsonSerializer.Serialize(state, JsonOptions));
    }

    public static void ClearPortalState(this ISession session)
    {
        session.Remove(SessionKey);
    }
}
