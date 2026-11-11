using MqttSecurity.Core.Models;

namespace MqttSecurity.Core.Contracts;

public interface IMqttClientIdBindingService
{
    bool TryReserve(string username,string clientId,out Guid reservationId);
    bool Confirm(string clientId, Guid reservationId);
    bool IsActive(string clientId, Guid reservationId);
    void Release(string clientId, Guid reservationId);
}