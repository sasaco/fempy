namespace FrameWeb.LocalRuntime;

internal sealed class BoundedLineBuffer(int maximumCharacters)
{
    private readonly object _gate = new();
    private readonly Queue<string> _lines = new();
    private int _characters;

    private bool _truncated;

    public bool Truncated
    {
        get
        {
            lock (_gate)
            {
                return _truncated;
            }
        }
    }

    public void Add(string line, bool sourceTruncated = false)
    {
        ArgumentNullException.ThrowIfNull(line);
        lock (_gate)
        {
            _truncated |= sourceTruncated;
            if (line.Length + 1 > maximumCharacters)
            {
                line = line[^Math.Max(1, maximumCharacters - 1)..];
                _truncated = true;
            }

            _lines.Enqueue(line);
            _characters += line.Length + 1;
            while (_characters > maximumCharacters && _lines.Count > 0)
            {
                string removed = _lines.Dequeue();
                _characters -= removed.Length + 1;
                _truncated = true;
            }
        }
    }

    public string[] Snapshot()
    {
        lock (_gate)
        {
            return _lines.ToArray();
        }
    }
}
