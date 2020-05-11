using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace EquityTrading.Server
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<T> TakeLast<T>(this IEnumerable<T> source, int N)
        {
            return source.Skip(Math.Max(0, source.Count() - N));
        }
        public static IEnumerable<T> Concat<T>(this IEnumerable<T> collection1, IEnumerable<T> collection2)
        {
            HashSet<T> result = new HashSet<T>();
            foreach (var element in collection1)
            {
                result.Add(element);
            }
            foreach (var element in collection2)
            {
                result.Add(element);
            }
            return result;
        }
    }
}
