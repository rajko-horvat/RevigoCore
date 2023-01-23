using System;
using System.Runtime.InteropServices;

/// <summary>
/// MT19937, with initialization improved 2002/1/26.
/// http://www.math.sci.hiroshima-u.ac.jp/~m-mat/MT/emt.html
/// 
/// Authors:
///		Takuji Nishimura and Makoto Matsumoto (m - mat @ math.sci.hiroshima - u.ac.jp(remove space))
///		Rajko Horvat (C# version, rhorvat at irb.hr)
/// 
/// License:
///		MIT
///		Copyright (c) 1997 - 2002, Makoto Matsumoto and Takuji Nishimura.
///		
///		Permission is hereby granted, free of charge, to any person obtaining a copy of this software 
/// 	and associated documentation files (the "Software"), to deal in the Software without restriction, 
/// 	including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, 
/// 	and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, 
/// 	subject to the following conditions: 
/// 	The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
/// 	The names of authors and contributors may not be used to endorse or promote products derived from this software 
/// 	without specific prior written permission.
///			
///		THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
///		INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
///		FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. 
///		IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, 
///		DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, 
///		ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
///	
/// </summary>
[Serializable, ComVisible(true)]
public class RandomMT19937
{
	// Fields
	private const int N = 624;
	private const int M = 397;
	private const uint MATRIX_A = 0x9908b0dfU;   // constant vector a
	private const uint UPPER_MASK = 0x80000000U; // most significant w-r bits
	private const uint LOWER_MASK = 0x7fffffffU; // least significant r bits

	private uint[] mt = new uint[N]; // the array for the state vector
	private int mti = N + 1; // mti==N+1 means mt[N] is not initialized
	private uint[] mag01 = new uint[] { 0x0U, MATRIX_A }; // mag01[x] = x * MATRIX_A  for x=0,1

	// Methods
	public RandomMT19937()
		: this((uint)Environment.TickCount)
	{
	}

	// initializes mt[N] with a seed
	public RandomMT19937(uint Seed)
	{
		mt[0] = Seed & 0xffffffffU; // for >32 bit machines
		for (mti = 1; mti < N; mti++)
		{
			mt[mti] = (1812433253U * (mt[mti - 1] ^ (mt[mti - 1] >> 30)) + (uint)mti);
			// See Knuth TAOCP Vol2. 3rd Ed. P.106 for multiplier.
			// In the previous versions, MSBs of the seed affect
			// only MSBs of the array mt[].
			// 2002/01/09 modified by Makoto Matsumoto
			mt[mti] &= 0xffffffffU; // for >32 bit machines
		}
	}

	// generates a random number on [0,0xffffffff]-interval
	private uint InternalSample()
	{
		uint y;

		if (mti >= N)
		{
			// generate N words at one time
			int kk;

			for (kk = 0; kk < N - M; kk++)
			{
				y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
				mt[kk] = mt[kk + M] ^ (y >> 1) ^ mag01[y & 0x1U];
			}
			for (; kk < N - 1; kk++)
			{
				y = (mt[kk] & UPPER_MASK) | (mt[kk + 1] & LOWER_MASK);
				mt[kk] = mt[kk + (M - N)] ^ (y >> 1) ^ mag01[y & 0x1U];
			}
			y = (mt[N - 1] & UPPER_MASK) | (mt[0] & LOWER_MASK);
			mt[N - 1] = mt[M - 1] ^ (y >> 1) ^ mag01[y & 0x1U];

			mti = 0;
		}

		y = mt[mti++];

		// Tempering
		y ^= (y >> 11);
		y ^= (y << 7) & 0x9d2c5680U;
		y ^= (y << 15) & 0xefc60000U;
		y ^= (y >> 18);

		return y;
	}

	public virtual uint UNext()
	{
		return this.InternalSample();
	}

	public virtual int Next()
	{
		return (int)(this.InternalSample() >> 1);
	}

	public virtual int Next(int maxValue)
	{
		if (maxValue <= 0)
		{
			throw new ArgumentOutOfRangeException("maxValue", "'maxValue' must be greater than zero.");
		}
		return (int)(this.Sample() * maxValue);
	}

	public virtual int Next(int minValue, int maxValue)
	{
		if (minValue > maxValue)
		{
			throw new ArgumentOutOfRangeException("minValue", "'minValue' cannot be greater than maxValue.");
		}
		long num = maxValue - minValue;
		
		return (((int)(this.Sample() * num)) + minValue);
	}

	public virtual void NextBytes(byte[] buffer)
	{
		if (buffer == null)
		{
			throw new ArgumentNullException("buffer");
		}
		for (int i = 0; i < buffer.Length; i++)
		{
			// (this.InternalSample() & 0xff) is the same, but faster than (this.InternalSample() % 0x100)
			buffer[i] = (byte)(this.InternalSample() & 0xff);
		}
	}

	// generates a random number on [0,1)-real-interval
	public virtual double NextDouble()
	{
		return this.Sample();
	}

	// generates a random number on [0,1)-real-interval
	protected virtual double Sample()
	{
		// (1.0 / 4294967296.0) = 2.3283064365386962890625e-10
		return (this.InternalSample() * 2.3283064365386962890625e-10);
	}
}
