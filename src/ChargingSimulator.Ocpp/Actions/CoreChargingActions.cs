using System.Text.Json;
using ChargingSimulator.Domain.Models;
using ChargingSimulator.Ocpp.Protocol;

namespace ChargingSimulator.Ocpp.Actions;

public static class CoreChargingActions
{
    public static OcppCall BootNotification(string id, string vendor, string model)
    {
        return Call(id, OcppActions.BootNotification, new { chargePointVendor = vendor, chargePointModel = model });
    }

    public static OcppCall Heartbeat(string id)
    {
        return Call(id, OcppActions.Heartbeat, new { });
    }

    public static OcppCall StatusNotification(string id, ConnectorName connector, ConnectorStatus status, DateTimeOffset timestamp)
    {
        return Call(id, OcppActions.StatusNotification, new { connectorId = (int)connector, errorCode = "NoError", status = status.ToString(), timestamp });
    }

    public static OcppCall Authorize(string id, string idTag)
    {
        return Call(id, OcppActions.Authorize, new { idTag });
    }

    public static OcppCall StartTransaction(string id, ConnectorName connector, string idTag, decimal meterStart, DateTimeOffset timestamp)
    {
        return Call(id, OcppActions.StartTransaction, new { connectorId = (int)connector, idTag, meterStart, timestamp });
    }

    public static OcppCall MeterValues(string id, ConnectorName connector, decimal value, DateTimeOffset timestamp)
    {
        return Call(id, OcppActions.MeterValues, new { connectorId = (int)connector, meterValue = new[] { new { timestamp, sampledValue = new[] { new { value = value.ToString(System.Globalization.CultureInfo.InvariantCulture), unit = "Wh" } } } } });
    }

    public static OcppCall StopTransaction(string id, int transactionId, decimal meterStop, DateTimeOffset timestamp)
    {
        return Call(id, OcppActions.StopTransaction, new { transactionId, meterStop, timestamp });
    }

    private static OcppCall Call(string id, string action, object payload)
    {
        return new(id, action, JsonSerializer.SerializeToElement(payload));
    }
}
