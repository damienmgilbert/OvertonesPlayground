namespace OvertonesPlayground.Tests.TestSupport;

/// <summary>
/// An in-memory <see cref="IPreferences"/>, so a view model's saved settings can be set up before it is built and read back after
/// it has changed them, with nothing touching the device.
/// </summary>
internal sealed class FakePreferences : IPreferences
{
    private readonly Dictionary<(string? SharedName, string Key), object?> _values = [];

    /// <summary>
    /// Every key that has been written, in order, so a test can check a setting was saved (or left alone).
    /// </summary>
    public List<string> Writes { get; } = [];

    public void Clear(string? sharedName = null)
    {
        foreach ((string? SharedName, string Key) key in _values.Keys.Where(k => k.SharedName == sharedName).ToList())
        {
            _values.Remove(key);
        }
    }

    public bool ContainsKey(string key, string? sharedName = null) => _values.ContainsKey((sharedName, key));

    public T Get<T>(string key, T defaultValue, string? sharedName = null) =>
        _values.TryGetValue((sharedName, key), out object? value) && value is T typed ? typed : defaultValue;

    public void Remove(string key, string? sharedName = null) => _values.Remove((sharedName, key));

    public void Set<T>(string key, T value, string? sharedName = null)
    {
        _values[(sharedName, key)] = value;
        Writes.Add(key);
    }
}
