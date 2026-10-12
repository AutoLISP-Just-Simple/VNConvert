namespace VNConvert.Core;

internal interface ICharsetEngine
{
    void StartInput(in VnConvOptions options);
    void StartOutput(in VnConvOptions options);
    bool NextInput(IByteInStream stream, out uint stdChar, out int bytesRead);
    bool PutChar(IByteOutStream stream, uint stdChar, out int bytesWritten);
}
