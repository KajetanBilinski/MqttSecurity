namespace MqttSecurity.Core.Enums;

[Flags]
internal enum MqttAction
{
    Subscribe = 0, 
    Publish = 1, 
    PublishAndSubscribe = Publish | Subscribe
}
