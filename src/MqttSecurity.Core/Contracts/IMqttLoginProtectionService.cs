using System.Net;
using MqttSecurity.Core.Models;

namespace MqttSecurity.Core.Contracts;

public interface IMqttLoginProtectionService
{
    MqttLoginProtectionDecision Check(
        string username,
        IPAddress ipAddress);

    void RegisterFailure(
        string username,
        IPAddress ipAddress);

    void RegisterSuccess(
        string username);
}