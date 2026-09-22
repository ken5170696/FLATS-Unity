using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
namespace ImageTools.IO
{
	public static class Decoders
	{
		private static List<Type> _decoderTypes = new List<Type>();

		public static void AddDecoder<TDecoder>() where TDecoder : IImageDecoder
		{
			if (!_decoderTypes.Contains(typeof(TDecoder)))
			{
				_decoderTypes.Add(typeof(TDecoder));
			}
		}

		public static ReadOnlyCollection<IImageDecoder> GetAvailableDecoders()
		{
			List<IImageDecoder> list = new List<IImageDecoder>();
			foreach (Type decoderType in _decoderTypes)
			{
				if ((object)decoderType != null)
				{
					list.Add(Activator.CreateInstance(decoderType) as IImageDecoder);
				}
			}
			return new ReadOnlyCollection<IImageDecoder>(list);
		}


	}
}
