using System;
using System.Collections.Generic;
using System.Text;

namespace StereoKit.Framework
{
	static class Noise
	{
		// Based on Squirrel's talk here:
		// https://www.gdcvault.com/play/1024365/Math-for-Game-Programmers-Noise
		public static uint Hash(int x, uint seed)
		{
			const uint BIT_NOISE1 = 0x68E31DA4;
			const uint BIT_NOISE2 = 0xB5297A4D;
			const uint BIT_NOISE3 = 0x1B56C4E9;

			uint mangled = (uint)x;
			mangled *= BIT_NOISE1;
			mangled += seed;
			mangled ^= (mangled >> 8);
			mangled += BIT_NOISE2;
			mangled ^= (mangled << 8);
			mangled *= BIT_NOISE3;
			mangled ^= (mangled >> 8);
			return mangled;
		}

		public static uint Hash(int x, int y, uint seed) {
			const int PRIME_NUMBER = 198491317;
			return Hash(x + (y * PRIME_NUMBER), seed);
		}

		public static float HashF(int x, uint seed)
			=> Hash(x, seed) / (float)uint.MaxValue;

		public static float HashF(int x, int y, uint seed)
			=> Hash(x,y,seed) / (float)uint.MaxValue;

		public struct Seed
		{
			public uint seed;
			public int  seed_curr;
			public Seed(uint seed) { this.seed = seed; seed_curr = 0; }
		}
		public static Seed  NextSeed;
		public static float NextF => Hash(NextSeed.seed_curr++, NextSeed.seed) / (float)uint.MaxValue;
		public static uint  Next  => Hash(NextSeed.seed_curr++, NextSeed.seed);
		public static int   NextRange (int   min, int   max)  => min+(int)(Hash (NextSeed.seed_curr++, NextSeed.seed)%(max-min));
		public static float NextRangeF(float min, float max)  => min+     (HashF(NextSeed.seed_curr++, NextSeed.seed)*(max-min));

	}
}
