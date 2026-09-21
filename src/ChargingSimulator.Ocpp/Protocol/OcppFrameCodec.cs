using System.Text.Json;

namespace ChargingSimulator.Ocpp.Protocol;

public static class OcppFrameCodec
{
    public static OcppParseResult Parse(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 3 || root[0].ValueKind != JsonValueKind.Number || root[1].ValueKind != JsonValueKind.String)
            {
                return new(null, "FormationViolation");
            }

            string id = root[1].GetString()!;
            return root[0].GetInt32() switch
            {
                2 when root.GetArrayLength() == 4 && root[2].ValueKind == JsonValueKind.String => new(new OcppCall(id, root[2].GetString()!, root[3].Clone()), null),
                3 when root.GetArrayLength() == 3 => new(new OcppCallResult(id, root[2].Clone()), null),
                4 when root.GetArrayLength() == 5 && root[2].ValueKind == JsonValueKind.String && root[3].ValueKind == JsonValueKind.String => new(new OcppCallError(id, root[2].GetString()!, root[3].GetString()!, root[4].Clone()), null),
                _ => new(null, "FormationViolation")
            };
        }
        catch (JsonException) { return new(null, "FormationViolation"); }
    }
    public static string Serialize(OcppFrame frame)
    {
        return frame switch
        {
            OcppCall call => JsonSerializer.Serialize(new object[] { 2, call.Id, call.Action, call.Payload }),
            OcppCallResult result => JsonSerializer.Serialize(new object[] { 3, result.Id, result.Payload }),
            OcppCallError error => JsonSerializer.Serialize(new object[] { 4, error.Id, error.ErrorCode, error.Description, error.Details }),
            _ => throw new ArgumentOutOfRangeException(nameof(frame))
        };
    }
}
