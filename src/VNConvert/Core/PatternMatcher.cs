namespace VNConvert.Core;

/// <summary>
/// KMP pattern matcher used for VIQR URL/email escape detection.
/// </summary>
internal sealed class PatternMatcher
{
    private sealed class PatternState
    {
        private readonly byte[] _pattern;
        private readonly int[] _border;
        private int _pos;

        public PatternState(string pattern)
        {
            _pattern = System.Text.Encoding.ASCII.GetBytes(pattern);
            _border = new int[_pattern.Length + 1];
            _pos = 0;

            int i = 0, j = -1;
            _border[i] = j;
            while (i < _pattern.Length)
            {
                while (j >= 0 && _pattern[i] != _pattern[j])
                    j = _border[j];
                i++;
                j++;
                _border[i] = j;
            }
        }

        public void Reset()
        {
            _pos = 0;
        }

        public bool FoundAtNextChar(byte ch)
        {
            while (_pos >= 0 && ch != _pattern[_pos])
                _pos = _border[_pos];

            _pos++;
            if (_pos == _pattern.Length)
            {
                _pos = _border[_pos];
                return true;
            }
            return false;
        }
    }

    private readonly PatternState[] _states;

    public PatternMatcher(string[] patterns)
    {
        _states = new PatternState[patterns.Length];
        for (int i = 0; i < patterns.Length; i++)
        {
            _states[i] = new PatternState(patterns[i]);
        }
    }

    public void Reset()
    {
        for (int i = 0; i < _states.Length; i++)
        {
            _states[i].Reset();
        }
    }

    public int FoundAtNextChar(byte ch)
    {
        int found = -1;
        for (int i = 0; i < _states.Length; i++)
        {
            if (_states[i].FoundAtNextChar(ch))
            {
                found = i;
            }
        }
        return found;
    }
}
