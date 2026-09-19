namespace MessageBus.Operations.Storage;

/// <summary>The kind of action an operator took against a failure.</summary>
public enum MessageActionKind
{
    /// <summary>Republished byte-for-byte to the endpoint that failed it.</summary>
    Retry,

    /// <summary>Republished to a different endpoint than the one that failed it.</summary>
    ReturnToSource,

    /// <summary>Marked as written off. Retained as history.</summary>
    Discard,

    /// <summary>Republished with a corrected body or headers under a new id.</summary>
    EditAndRetry,

    /// <summary>Physically removed the failure row. The action row survives.</summary>
    Delete
}
