using System;
using System.Threading;
using System.Threading.Tasks;

namespace Flats.Modules
{
    public interface IModSource
    {
        string Identity { get; }
        Task<CatalogPage> Browse(CatalogQuery query, CancellationToken cancel);
        Task Download(CatalogItem item, string destination, Action<long> progress, CancellationToken cancel);
        Task<byte[]> Image(string url, CancellationToken cancel);
    }

}
