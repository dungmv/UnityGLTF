using System.Linq;

namespace UnityGIF.Data
{
	/// <summary>
	/// Stub for Texture2D from UnityEngine.CoreModule
	/// </summary>
	public class Texture2D
	{
		// ReSharper disable once InconsistentNaming (original naming saved)
		public readonly int width;

		// ReSharper disable once InconsistentNaming (original naming saved)
		public readonly int height;
		
		private Color32[] _pixels;

		public Texture2D(int width, int height)
		{
			this.width = width;
			this.height = height;
		}

        public void SetPixelsFloat(UnityEngine.Color[] pixels)
        {
            _pixels = new UnityGIF.Data.Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                byte r = (byte)(pixels[i].r * 255);
                byte g = (byte)(pixels[i].g * 255);
                byte b = (byte)(pixels[i].b * 255);
                byte a = (byte)(255);
                _pixels[i] = new UnityGIF.Data.Color32(r, g, b, a);
            }
        }

        public void SetPixels32(Color32[] pixels)
		{
			_pixels = pixels.ToArray();
		}

		public Color32[] GetPixels32()
		{
			return _pixels.ToArray();
		}

		public void Apply()
		{
		}
	}
}