using UnityGIF.Enums;
using UnityGIF.Core;

namespace UnityGIF.Data
{
	/// <summary>
	/// Texture + delay + disposal method
	/// </summary>
	public class GifFrame
	{
		public Texture2D Texture;
		public float Delay;
		public DisposalMethod DisposalMethod = DisposalMethod.RestoreToBackgroundColor;

		public void ApplyPalette(MasterPalette palette)
		{
			TextureConverter.ConvertTo8Bits(ref Texture, palette);
		}
	}
}