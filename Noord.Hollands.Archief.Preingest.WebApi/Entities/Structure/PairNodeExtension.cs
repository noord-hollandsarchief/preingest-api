using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Noord.Hollands.Archief.Preingest.WebApi.Entities.Structure
{	public static class PairNodeExtension
	{
		public static IEnumerable<PairNode<ISidecar>> Flatten(this IEnumerable<PairNode<ISidecar>> e)
		{
			return e.SelectMany(c => c.Children.Flatten()).Concat(e);
		}

		public static IEnumerable<PairNode<ISidecar>> Flatten(this PairNode<ISidecar> e)
		{
			//return e.Children.SelectMany(c => c.Children.Flatten()).Concat(new[] { e });
			return e.Children.SelectMany(c => c.Children.Flatten()).Concat(e.Children).Concat(new[] { e });
		}

	}
}
