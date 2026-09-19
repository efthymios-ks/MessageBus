namespace MessageBus.Operations.Storage;

/// <summary>Lifecycle state of a failure row.</summary>
public enum FailureStatus
{
    /// <summary>Recorded but no operator action taken yet.</summary>
    Unresolved,

    /// <summary>Republished by an operator; retained as history.</summary>
    Retried,

    /// <summary>Written off by an operator; retained as history.</summary>
    Discarded
}
