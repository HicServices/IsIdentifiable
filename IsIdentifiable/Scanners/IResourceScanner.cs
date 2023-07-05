using IsIdentifiable.Failures;

namespace IsIdentifiable.Scanners;

public interface IResourceScanner
{
    /// <summary>
    /// The number of <see cref="Failure"/>s produced since creation or since <see cref="Reset"/> has been called
    /// </summary>
    int FailureCount { get; }

    /// <summary>
    /// Reset the scanner to its initial state.
    /// </summary>
    void Reset();
}
