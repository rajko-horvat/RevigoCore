using System;
using System.IO;
using System.Text;

/// <summary>
/// The class that adds a buffering layer to read operations on FileStream
/// 
/// Authors:
/// 	Rajko Horvat (rhorvat at irb.hr)
/// 
/// License:
/// 	MIT
/// 	Copyright (c) 2011-2023, Ruđer Bošković Institute
///		
/// 	Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
/// 	and associated documentation files (the "Software"), to deal in the Software without restriction, 
/// 	including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
/// 	and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
/// 	subject to the following conditions: 
/// 	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
/// 	The names of authors and contributors may not be used to endorse or promote Software products derived from this software 
/// 	without specific prior written permission.
/// 	
/// 	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
/// 	INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
/// 	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
/// 	IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
/// 	DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
/// 	ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
/// </summary>
public sealed class BufferedReader : IDisposable
{
	private FileStream oStream;
	private byte[] aBuffer;
	private int iReadPosition = 0;
	private int iReadLength = 0;
	private long lFilePosition = 0;
	private long lFileLength = 0;

	private BufferedReader()
	{
	}

	/// <summary>Initializes a new instance of the <see cref="T:System.IO.BufferedStream" /> class with a default buffer size of 4096 bytes.</summary>
	/// <param name="stream">The current stream. </param>
	/// <exception cref="T:System.ArgumentNullException">
	/// <paramref name="stream" /> is null. </exception>
	public BufferedReader(FileStream stream)
		: this(stream, 4096)
	{
	}

	/// <summary>Initializes a new instance of the <see cref="T:System.IO.BufferedStream" /> class with the specified buffer size.</summary>
	/// <param name="stream">The current stream. </param>
	/// <param name="bufferSize">The buffer size in bytes. </param>
	/// <exception cref="T:System.ArgumentNullException">
	/// <paramref name="stream" /> is null. </exception>
	/// <exception cref="T:System.ArgumentOutOfRangeException">
	/// <paramref name="bufferSize" /> is negative. </exception>
	public BufferedReader(FileStream stream, int bufferSize)
	{
		if (stream == null)
			throw new ArgumentNullException("The parameter 'stream' cannot be null");
		if (bufferSize <= 0)
			throw new ArgumentOutOfRangeException("The parameter 'bufferSize' must be larger than zero");

		this.oStream = stream;
		this.aBuffer = new byte[bufferSize];
		this.lFilePosition = stream.Position;
		this.lFileLength = stream.Length; // we have to precache file length, otherwise it's slow

		if (this.oStream.CanRead && this.oStream.CanSeek)
			return;

		throw new Exception("The stream is closed, cannot read or cannot seek");
	}

	~BufferedReader()
	{
		this.Dispose(false);
	}

	public void Dispose()
	{
		this.Dispose(true);
	}

	private void Dispose(bool disposing)
	{
		if (disposing)
		{
			try
			{
				if (this.oStream == null)
					return;

				this.oStream.Close();
			}
			finally
			{
				this.oStream = null;
				this.aBuffer = null;
			}
		}

		this.oStream = null;
		this.aBuffer = null;
	}

	public void Close()
	{
		if (this.oStream != null)
			this.oStream.Close();

		this.oStream = null;
		this.aBuffer = null;
	}

	public FileStream UnderlyingStream
	{
		get
		{
			return this.oStream;
		}
	}

	public int BufferSize
	{
		get
		{
			return this.aBuffer.Length;
		}
	}

	/// <summary>Gets a value indicating whether the current stream supports reading.</summary>
	/// <returns>true if the stream supports reading; false if the stream is closed or was opened with write-only access.</returns>
	/// <filterpriority>2</filterpriority>
	public bool CanRead
	{
		get
		{
			if (this.oStream != null)
				return this.oStream.CanRead;
			return false;
		}
	}

	/// <summary>Gets a value indicating whether the current stream supports seeking.</summary>
	/// <returns>true if the stream supports seeking; false if the stream is closed or if the stream was constructed from an operating system handle such as a pipe or output to the console.</returns>
	/// <filterpriority>2</filterpriority>
	public bool CanSeek
	{
		get
		{
			if (this.oStream != null)
				return this.oStream.CanSeek;
			return false;
		}
	}

	/// <summary>Gets the stream length in bytes.</summary>
	/// <returns>The stream length in bytes.</returns>
	/// <exception cref="T:System.IO.IOException">The underlying stream is null or closed. </exception>
	/// <exception cref="T:System.NotSupportedException">The stream does not support seeking. </exception>
	/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
	/// <filterpriority>2</filterpriority>
	public long Length
	{
		get
		{
			if (this.oStream == null)
				throw new Exception("The stream is closed, cannot read or cannot seek");
			return this.lFileLength;
		}
	}

	/// <summary>Gets the position within the current stream.</summary>
	/// <returns>The position within the current stream.</returns>
	/// <exception cref="T:System.ArgumentOutOfRangeException">The value passed to <see cref="M:System.IO.BufferedStream.Seek(System.Int64,System.IO.SeekOrigin)" /> is negative. </exception>
	/// <exception cref="T:System.IO.IOException">An I/O error occurs, such as the stream being closed. </exception>
	/// <exception cref="T:System.NotSupportedException">The stream does not support seeking. </exception>
	/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
	/// <filterpriority>2</filterpriority>
	public long Position
	{
		get
		{
			if (this.oStream == null)
				throw new Exception("The stream is closed, cannot read or cannot seek");
			return this.lFilePosition;
		}
		set
		{
			if (value < 0L)
				throw new ArgumentOutOfRangeException("The property value must be positive or zero");
			this.Seek(value, SeekOrigin.Begin);
		}
	}

	public bool EndOfStream
	{
		get
		{
			if (this.lFilePosition != this.lFileLength)
			{
				return false;
			}

			return true;
		}
	}

	private void ReadBufferFromStream()
	{
		int byteCount = this.oStream.Read(this.aBuffer, 0, this.aBuffer.Length);
		this.iReadLength = byteCount;
		this.iReadPosition = 0;
	}

	/// <summary>Copies bytes from the current buffered stream to an array.</summary>
	/// <returns>The total number of bytes read into <paramref name="array" />. This can be less than the number of bytes requested if that many bytes are not currently available, or 0 if the end of the stream has been reached before any data can be read.</returns>
	/// <param name="array">The buffer to which bytes are to be copied. </param>
	/// <param name="offset">The byte offset in the buffer at which to begin reading bytes. </param>
	/// <param name="count">The number of bytes to be read. </param>
	/// <exception cref="T:System.ArgumentException">Length of <paramref name="array" /> minus <paramref name="offset" /> is less than <paramref name="count" />. </exception>
	/// <exception cref="T:System.ArgumentNullException">
	/// <paramref name="array" /> is null. </exception>
	/// <exception cref="T:System.ArgumentOutOfRangeException">
	/// <paramref name="offset" /> or <paramref name="count" /> is negative. </exception>
	/// <exception cref="T:System.IO.IOException">The stream is not open or is null. </exception>
	/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
	/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
	/// <filterpriority>2</filterpriority>
	public int Read(byte[] array, int offset, int count)
	{
		if (array == null)
			throw new ArgumentNullException("The parameter 'array' cannot be null");
		if (offset < 0)
			throw new ArgumentOutOfRangeException("The parameter 'offset' must be positive or zero");
		if (count < 0)
			throw new ArgumentOutOfRangeException("The parameter 'count' must be positive or zero");
		if (array.Length - offset < count)
			throw new ArgumentException("The size of the array is less than required");

		if (this.oStream == null)
			throw new Exception("The stream is closed, cannot read or cannot seek");

		int iRead = 0;
		while (iRead < count)
		{
			if (this.iReadPosition < this.iReadLength)
			{
				int iBufferCount = Math.Min(count - iRead, this.iReadLength - this.iReadPosition);
				Array.Copy(this.aBuffer, this.iReadPosition, array, offset + iRead, iBufferCount);
				iRead += iBufferCount;
				this.lFilePosition += iBufferCount;
				this.iReadPosition += iBufferCount;
			}
			else if(this.iReadPosition == this.aBuffer.Length)
			{
				ReadBufferFromStream();
				if (this.iReadPosition == this.aBuffer.Length)
					break;
			}
		}

		return iRead;
	}

	/// <summary>Reads a byte from the underlying stream and returns the byte cast to an int, or returns -1 if reading from the end of the stream.</summary>
	/// <returns>The byte cast to an int, or -1 if reading from the end of the stream.</returns>
	/// <exception cref="T:System.IO.IOException">An I/O error occurs, such as the stream being closed. </exception>
	/// <exception cref="T:System.NotSupportedException">The stream does not support reading. </exception>
	/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
	/// <filterpriority>2</filterpriority>
	public int ReadByte()
	{
		if (this.oStream == null)
			throw new Exception("The stream is closed, cannot read or cannot seek");

		if (this.iReadPosition == this.iReadLength)
		{
			ReadBufferFromStream();
		}
		if (this.iReadPosition == this.iReadLength)
			return -1;

		this.lFilePosition++;

		return (int)this.aBuffer[this.iReadPosition++];
	}

	public int PeekByte()
	{
		if (this.oStream == null)
			throw new Exception("The stream is closed, cannot read or cannot seek");

		if (this.iReadPosition == this.iReadLength)
		{
			ReadBufferFromStream();
		}
		if (this.iReadPosition == this.iReadLength)
			return -1;

		return (int)this.aBuffer[this.iReadPosition];
	}

	public string ReadLine()
	{
		StringBuilder aResult = new StringBuilder(255);

		while (!this.EndOfStream)
		{
			int iCh = ReadByte();

			// end of file
			if (iCh < 0)
				break;

			if (iCh == (int)'\r' || iCh == (int)'\n')
			{
				int iCh1 = PeekByte();
				if (iCh != iCh1 && iCh1 == (int)'\r' || iCh1 == (int)'\n')
				{
					ReadByte();
				}
				break;
			}

			aResult.Append((char)iCh);
		}

		return aResult.ToString();
	}

	/// <summary>Sets the position within the current buffered stream.</summary>
	/// <returns>The new position within the current buffered stream.</returns>
	/// <param name="offset">A byte offset relative to <paramref name="origin" />. </param>
	/// <param name="origin">A value of type <see cref="T:System.IO.SeekOrigin" /> indicating the reference point from which to obtain the new position. </param>
	/// <exception cref="T:System.IO.IOException">The stream is not open or is null. </exception>
	/// <exception cref="T:System.NotSupportedException">The stream does not support seeking. </exception>
	/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
	/// <filterpriority>2</filterpriority>
	public long Seek(long offset, SeekOrigin origin)
	{
		if (this.oStream == null)
			throw new Exception("The stream is closed, cannot read or cannot seek");

		long lNewFilePosition;
		long lNewBufferPosition;

		switch (origin)
		{
			case SeekOrigin.Begin:
				lNewBufferPosition = (long)this.iReadPosition + (offset - this.lFilePosition);
				if (lNewBufferPosition < 0 || lNewBufferPosition >= (long)this.iReadLength)
				{
					// outside of boundaries
					this.oStream.Seek(offset, origin);
					this.iReadPosition = this.iReadLength = 0;
					this.lFilePosition = offset;
				}
				else
				{
					this.lFilePosition += lNewBufferPosition - (long)this.iReadPosition;
					this.iReadPosition = (int)lNewBufferPosition;
				}
				break;
			case SeekOrigin.Current:
				lNewBufferPosition = (long)this.iReadPosition + offset;
				if (lNewBufferPosition < 0 || lNewBufferPosition >= (long)this.iReadLength)
				{
					// outside of boundaries
					this.oStream.Seek(this.lFilePosition + offset, origin);
					this.iReadPosition = this.iReadLength = 0;
					this.lFilePosition += offset;
				}
				else
				{
					this.lFilePosition += lNewBufferPosition - (long)this.iReadPosition;
					this.iReadPosition = (int)lNewBufferPosition;
				}
				break;
			case SeekOrigin.End:
				lNewFilePosition = Math.Min(this.lFileLength, this.lFileLength + offset);
				lNewBufferPosition = (long)this.iReadPosition + (lNewFilePosition - this.lFilePosition);
				if (lNewBufferPosition < 0 || lNewBufferPosition >= (long)this.iReadLength)
				{
					// outside of boundaries
					this.oStream.Seek(lNewFilePosition - this.lFileLength, origin);
					this.iReadPosition = this.iReadLength = 0;
					this.lFilePosition = lNewFilePosition;
				}
				else
				{
					this.lFilePosition += lNewBufferPosition - (long)this.iReadPosition;
					this.iReadPosition = (int)lNewBufferPosition;
				}
				break;
		}

		return this.lFilePosition;
	}
}
