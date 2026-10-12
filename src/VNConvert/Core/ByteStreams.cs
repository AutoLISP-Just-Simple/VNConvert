using System.Buffers;
using System.Runtime.CompilerServices;

namespace VNConvert.Core;

internal interface IByteInStream
{
    bool IsEos { get; }
    int Left { get; }
    bool GetNext(out byte b);
    bool PeekNext(out byte b);
    bool GetNextW(out ushort w);
    bool PeekNextW(out ushort w);
}

internal interface IByteOutStream
{
    bool IsOk { get; }
    int BytesWritten { get; }
    bool PutB(byte b);
    bool PutW(ushort w);
}

/// <summary>
/// Fast in-memory byte input stream over unsafe pointer or span.
/// </summary>
internal unsafe sealed class SpanByteInStream : IByteInStream
{
    private readonly byte* _data;
    private byte* _current;
    private readonly int _length; // -1 if null-terminated
    private int _left;
    private bool _eos;

    public SpanByteInStream(byte* data, int length)
    {
        _data = data;
        _current = data;
        _length = length;
        _left = length;

        if (data == null)
        {
            _eos = true;
            _left = 0;
        }
        else if (length == -1)
        {
            _eos = *data == 0;
            _left = _eos ? 0 : int.MaxValue;
        }
        else
        {
            _eos = length <= 0;
        }
    }

    public bool IsEos => _eos;
    public int Left => _left;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetNext(out byte b)
    {
        if (_eos)
        {
            b = 0;
            return false;
        }

        b = *_current++;
        if (_length == -1)
        {
            _eos = (b == 0);
        }
        else
        {
            _left--;
            _eos = (_left <= 0);
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PeekNext(out byte b)
    {
        if (_eos)
        {
            b = 0;
            return false;
        }
        b = *_current;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool GetNextW(out ushort w)
    {
        if (_eos)
        {
            w = 0;
            return false;
        }

        if (_length != -1 && _left < 2)
        {
            _eos = true;
            w = 0;
            return false;
        }

        w = *(ushort*)_current;
        _current += 2;

        if (_length == -1)
        {
            _eos = (w == 0);
        }
        else
        {
            _left -= 2;
            _eos = (_left <= 0);
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PeekNextW(out ushort w)
    {
        if (_eos)
        {
            w = 0;
            return false;
        }

        if (_length != -1 && _left < 2)
        {
            w = 0;
            return false;
        }

        w = *(ushort*)_current;
        return true;
    }
}

/// <summary>
/// Fast in-memory byte output stream over unsafe pointer or span.
/// Emulates StringBOStream: even if buffer capacity is exceeded, BytesWritten continues
/// to increment so callers can query the required output size.
/// </summary>
internal unsafe sealed class SpanByteOutStream : IByteOutStream
{
    private readonly byte* _buffer;
    private byte* _current;
    private readonly int _capacity;
    private int _bytesWritten;
    private bool _bad;

    public SpanByteOutStream(byte* buffer, int capacity)
    {
        _buffer = buffer;
        _current = buffer;
        _capacity = capacity;
        _bytesWritten = 0;
        _bad = false;
    }

    public bool IsOk => !_bad;
    public int BytesWritten => _bytesWritten;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PutB(byte b)
    {
        _bytesWritten++;
        if (_bad)
            return false;

        if (_bytesWritten <= _capacity && _buffer != null)
        {
            *_current++ = b;
            return true;
        }

        _bad = true;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PutW(ushort w)
    {
        _bytesWritten += 2;
        if (_bad)
            return false;

        if (_bytesWritten <= _capacity && _buffer != null)
        {
            *(ushort*)_current = w;
            _current += 2;
            return true;
        }

        _bad = true;
        return false;
    }
}

/// <summary>
/// Dynamically growing byte output stream using array pooling.
/// </summary>
internal sealed class GrowingByteOutStream : IByteOutStream, IDisposable
{
    private byte[] _buffer;
    private int _count;

    public GrowingByteOutStream(int initialCapacity = 256)
    {
        _buffer = ArrayPool<byte>.Shared.Rent(Math.Max(initialCapacity, 64));
        _count = 0;
    }

    public bool IsOk => true;
    public int BytesWritten => _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacity(int additional)
    {
        if (_count + additional > _buffer.Length)
        {
            int newCap = Math.Max(_buffer.Length * 2, _count + additional);
            byte[] newBuf = ArrayPool<byte>.Shared.Rent(newCap);
            Buffer.BlockCopy(_buffer, 0, newBuf, 0, _count);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = newBuf;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PutB(byte b)
    {
        EnsureCapacity(1);
        _buffer[_count++] = b;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool PutW(ushort w)
    {
        EnsureCapacity(2);
        _buffer[_count++] = (byte)(w & 0xFF);
        _buffer[_count++] = (byte)(w >> 8);
        return true;
    }

    public ReadOnlySpan<byte> AsSpan() => _buffer.AsSpan(0, _count);

    public byte[] ToArray()
    {
        byte[] result = new byte[_count];
        Buffer.BlockCopy(_buffer, 0, result, 0, _count);
        return result;
    }

    public void Dispose()
    {
        if (_buffer != null)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = null!;
        }
    }
}

/// <summary>
/// Stream-backed byte input stream with lookahead buffer.
/// </summary>
internal sealed class StreamByteInStream : IByteInStream
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private int _bufferStart;
    private int _bufferLength;
    private bool _eos;

    public StreamByteInStream(Stream stream, int bufferSize = 8192)
    {
        _stream = stream;
        _buffer = new byte[Math.Max(bufferSize, 16)];
        _bufferStart = 0;
        _bufferLength = 0;
        _eos = false;
        Refill();
    }

    public bool IsEos => _eos && _bufferStart >= _bufferLength;
    public int Left => IsEos ? 0 : 1; // Left is positive if more bytes available

    private bool Refill()
    {
        if (_bufferStart >= _bufferLength)
        {
            _bufferStart = 0;
            _bufferLength = _stream.Read(_buffer, 0, _buffer.Length);
            if (_bufferLength == 0)
            {
                _eos = true;
                return false;
            }
        }
        return true;
    }

    private bool EnsureAvailable(int count)
    {
        int available = _bufferLength - _bufferStart;
        if (available >= count)
            return true;

        if (_bufferStart > 0 && available > 0)
        {
            Buffer.BlockCopy(_buffer, _bufferStart, _buffer, 0, available);
            _bufferLength = available;
            _bufferStart = 0;
        }
        else if (_bufferStart > 0)
        {
            _bufferStart = 0;
            _bufferLength = 0;
        }

        while (_bufferLength < count)
        {
            int read = _stream.Read(_buffer, _bufferLength, _buffer.Length - _bufferLength);
            if (read == 0)
            {
                _eos = true;
                return _bufferLength >= count;
            }
            _bufferLength += read;
        }
        return true;
    }

    public bool GetNext(out byte b)
    {
        if (!EnsureAvailable(1))
        {
            b = 0;
            return false;
        }
        b = _buffer[_bufferStart++];
        return true;
    }

    public bool PeekNext(out byte b)
    {
        if (!EnsureAvailable(1))
        {
            b = 0;
            return false;
        }
        b = _buffer[_bufferStart];
        return true;
    }

    public bool GetNextW(out ushort w)
    {
        if (!EnsureAvailable(2))
        {
            w = 0;
            return false;
        }
        w = (ushort)(_buffer[_bufferStart] | (_buffer[_bufferStart + 1] << 8));
        _bufferStart += 2;
        return true;
    }

    public bool PeekNextW(out ushort w)
    {
        if (!EnsureAvailable(2))
        {
            w = 0;
            return false;
        }
        w = (ushort)(_buffer[_bufferStart] | (_buffer[_bufferStart + 1] << 8));
        return true;
    }
}

/// <summary>
/// Stream-backed byte output stream with write buffering.
/// </summary>
internal sealed class StreamByteOutStream : IByteOutStream, IDisposable
{
    private readonly Stream _stream;
    private readonly byte[] _buffer;
    private int _count;
    private int _totalWritten;
    private bool _isOk;

    public StreamByteOutStream(Stream stream, int bufferSize = 8192)
    {
        _stream = stream;
        _buffer = new byte[Math.Max(bufferSize, 16)];
        _count = 0;
        _totalWritten = 0;
        _isOk = true;
    }

    public bool IsOk => _isOk;
    public int BytesWritten => _totalWritten;

    private void FlushInternal()
    {
        if (_count > 0)
        {
            _stream.Write(_buffer, 0, _count);
            _count = 0;
        }
    }

    public bool PutB(byte b)
    {
        if (_count >= _buffer.Length)
            FlushInternal();

        _buffer[_count++] = b;
        _totalWritten++;
        return true;
    }

    public bool PutW(ushort w)
    {
        if (_count + 2 > _buffer.Length)
            FlushInternal();

        _buffer[_count++] = (byte)(w & 0xFF);
        _buffer[_count++] = (byte)(w >> 8);
        _totalWritten += 2;
        return true;
    }

    public void Flush()
    {
        FlushInternal();
        _stream.Flush();
    }

    public void Dispose()
    {
        Flush();
    }
}
