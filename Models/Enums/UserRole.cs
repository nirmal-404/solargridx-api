// Smart Solar Microgrid Trading System - staff role definition.
namespace SolarGridX.Api.Models.Enums;

// Only staff users receive an assignable role. A Prosumer is an NIC-keyed profile, not a role.
public enum UserRole
{
    Backoffice,
    GridOperator
}
