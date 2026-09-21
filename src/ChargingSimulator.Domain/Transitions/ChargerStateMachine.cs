using ChargingSimulator.Domain.Models;

namespace ChargingSimulator.Domain.Transitions;

public static class ChargerStateMachine
{
    public static bool CanTransition(ManagedChargerState from, ManagedChargerState to)
    {
        return (from, to) switch
        {
            (ManagedChargerState.Available, ManagedChargerState.Reserved) => true,
            (ManagedChargerState.Reserved, ManagedChargerState.Connecting or ManagedChargerState.Recovering) => true,
            (ManagedChargerState.Connecting, ManagedChargerState.Ready or ManagedChargerState.Recovering) => true,
            (ManagedChargerState.Ready, ManagedChargerState.Recovering) => true,
            (ManagedChargerState.Recovering, ManagedChargerState.Available or ManagedChargerState.RecoveryFailed) => true,
            (ManagedChargerState.RecoveryFailed, ManagedChargerState.Recovering) => true,
            (_, ManagedChargerState.Stopped) => true,
            _ => from == to
        };
    }
}
