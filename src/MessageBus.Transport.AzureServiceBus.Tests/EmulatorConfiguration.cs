namespace MessageBus.Transport.AzureServiceBus.Tests;

/// <summary>
/// Writes the emulator's topology to a temporary file. Written rather than checked in because the
/// container needs an absolute path, and a relative one resolves differently under every runner.
/// Each subscription forwards to the endpoint queue, which is what keeps one queue per endpoint
/// true on a broker where a subscription is itself a receive point.
/// </summary>
internal static class EmulatorConfiguration
{
    public const string EndpointQueue = "verify-endpoint";
    public const string ErrorQueue = "verify-error";
    public const string BoundTopic = "Tests.Bound.v1";
    public const string UnboundTopic = "Tests.Unbound.v1";

    /// <summary>
    /// Another endpoint's subscription on the unbound topic. The emulator refuses a topic with no
    /// subscription at all, and a topic somebody else listens to is the case worth testing anyway:
    /// the topic exists, the messages flow, and this endpoint still never sees one.
    /// </summary>
    public const string OtherSubscription = "somebody-else";

    public static string Path { get; } = Write();

    private static string Write()
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"servicebus-{Guid.NewGuid():N}.json");

        File.WriteAllText(path, Contents);

        return path;
    }

    private const string Contents = $$"""
        {
          "UserConfig": {
            "Namespaces": [
              {
                "Name": "sbemulatorns",
                "Queues": [
                  { "Name": "{{EndpointQueue}}", "Properties": { "DeadLetteringOnMessageExpiration": false } },
                  { "Name": "{{ErrorQueue}}", "Properties": { "DeadLetteringOnMessageExpiration": false } }
                ],
                "Topics": [
                  {
                    "Name": "{{BoundTopic}}",
                    "Subscriptions": [
                      {
                        "Name": "{{EndpointQueue}}",
                        "Properties": { "ForwardTo": "{{EndpointQueue}}" }
                      }
                    ]
                  },
                  {
                    "Name": "{{UnboundTopic}}",

                    "Subscriptions": [
                      { "Name": "{{OtherSubscription}}", "Properties": {} }
                    ]
                  }
                ]
              }
            ],
            "Logging": { "Type": "File" }
          }
        }
        """;
}
