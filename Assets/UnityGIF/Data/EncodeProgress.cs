﻿using System;

namespace UnityGIF.Data
{
	public class EncodeProgress
	{
		public int Progress;
		public int FrameCount;
		public bool Completed;
		public byte[] Bytes;
		public Exception Exception;
	}
}