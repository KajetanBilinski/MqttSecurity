namespace MqttSecurity.Core.Enums;

[Flags]
public enum MqttAction
{
    Subscribe = 0, 
    Publish = 1, 
    PublishAndSubscribe = Publish | Subscribe
}
