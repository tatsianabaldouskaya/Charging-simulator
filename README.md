# Charging Simulator

The simulator is a .NET 10 OCPP 1.6-J service. Start it with `dotnet run --project src/ChargingSimulator.Host` and check `GET /health`.

Run the solution checks with `dotnet test ChargingSimulator.sln` and `dotnet format ChargingSimulator.sln --verify-no-changes`.

Configure `Simulator__ChargerIds__0`, `Simulator__ChargerIds__1`, and further pool identities through deployment configuration. Keep credentials out of source control. The API is documented in [the session-control contract](specs/001-charger-session-management/contracts/session-control-api.md); the dashboard deliberately never calls a ChargeLab management API.
