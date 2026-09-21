namespace ChargingSimulator.Application.Manual;

public sealed record ManualConnectionProfile(
    string ChargeLabUrl,
    string ChargeLabWss,
    string ChargeLabApiKey,
    string ChargeLabCompanyId,
    string ChargerId,
    string OcppId)
{
    public void Validate()
    {
        if (!Uri.TryCreate(ChargeLabWss, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeWs && uri.Scheme != Uri.UriSchemeWss))
        {
            throw new ManualProfileException("ChargeLabWss must be a ws:// or wss:// URL.");
        }

        if (string.IsNullOrWhiteSpace(OcppId) || string.IsNullOrWhiteSpace(ChargeLabCompanyId))
        {
            throw new ManualProfileException("OcppId and ChargeLabCompanyId are required.");
        }
    }

    public object SafeProjection()
    {
        return new { ChargeLabWss, ChargeLabCompanyId, OcppId };
    }
}
