namespace VNConvert.Core;

internal sealed class Utf8ViqrEngine : ICharsetEngine
{
    private readonly Utf8Engine _utf8Engine;
    private readonly ViqrEngine _viqrEngine;
    private VnConvOptions _options;

    public Utf8ViqrEngine(Utf8Engine utf8Engine, ViqrEngine viqrEngine)
    {
        _utf8Engine = utf8Engine;
        _viqrEngine = viqrEngine;
    }

    public void StartInput(in VnConvOptions options)
    {
        _options = options;
        _utf8Engine.StartInput(options);
        _viqrEngine.StartInput(options);
    }

    public void StartOutput(in VnConvOptions options)
    {
        _options = options;
        _utf8Engine.StartOutput(options);
        _viqrEngine.StartOutput(options);
    }

    public bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead)
    {
        if (!stream.PeekNext(out byte ch))
        {
            stdChar = 0;
            bytesRead = 0;
            return false;
        }

        if (ch > 0xBF && ch < 0xFE)
        {
            _viqrEngine.StartInput(_options);
            _viqrEngine.Suspicious = true;
            return _utf8Engine.NextInput(stream, out stdChar, out bytesRead);
        }

        return _viqrEngine.NextInput(stream, out stdChar, out bytesRead);
    }

    public bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten)
    {
        return _viqrEngine.PutChar(stream, stdChar, out bytesWritten);
    }
}
