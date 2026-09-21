using ChargingSimulator.Domain.Models;

namespace ChargingSimulator.Domain.Transitions;

public static class ConnectorStateMachine
{
    public static bool CanTransition(ConnectorStatus from, ConnectorStatus to)
    {
        return (from, to) switch
        {
            (_, ConnectorStatus.Faulted or ConnectorStatus.Unavailable) => true,
            (ConnectorStatus.Available, ConnectorStatus.Preparing or ConnectorStatus.Reserved) => true,
            (ConnectorStatus.Reserved, ConnectorStatus.Available or ConnectorStatus.Preparing) => true,
            (ConnectorStatus.Preparing, ConnectorStatus.Charging or ConnectorStatus.Available) => true,
            (ConnectorStatus.Charging, ConnectorStatus.SuspendedEV or ConnectorStatus.SuspendedEVSE or ConnectorStatus.Finishing) => true,
            (ConnectorStatus.SuspendedEV or ConnectorStatus.SuspendedEVSE, ConnectorStatus.Charging or ConnectorStatus.Finishing) => true,
            (ConnectorStatus.Finishing, ConnectorStatus.Available) => true,
            (ConnectorStatus.Faulted or ConnectorStatus.Unavailable, ConnectorStatus.Available) => true,
            _ => from == to
        };
    }
}
