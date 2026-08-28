using System.IO;
using System.Threading.Tasks;

namespace Softbase.Cdc.Factory.Interfaces;

/// <summary>
/// Abstraction for file storage of template backup files.
/// Phase 1 uses a local mounted volume; cloud providers (S3, Azure Blob) can be added later.
/// </summary>
public interface ITemplateStorageProvider
{
    Task<string> StoreAsync(Stream stream, string fileName);
    Task<Stream> RetrieveAsync(string filePath);
    Task<bool> DeleteAsync(string filePath);
    bool Exists(string filePath);
}
