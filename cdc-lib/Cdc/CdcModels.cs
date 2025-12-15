
using System.Collections.Generic;
using System.Linq;

namespace Softbase;

public interface IProfile :
    IDictionary<string, IEnumerable<IDictionary<string, object>>>
{

}

public class SqlTable
{

    public SqlTable(string catalog, string schema, string name)
    {
        Catalog = catalog;
        Schema = schema;
        Name = name;
    }

    public string Catalog { get; set; }
    public string Schema { get; set; }
    public string Name { get; set; }
    public IEnumerable<SqlIndex> Indexes { get; set; } = new List<SqlIndex>();

    public bool HasPrimaryKey
    {
        get
        {
            return Indexes.Any(i => i.IndexType.Contains("primary"));
        }
    }

    public SqlIndex? GetPrimaryIndex()
    {
        return Indexes.FirstOrDefault(i => i.IndexType.Contains("primary"));
    }
}

public class SqlIndex
{
    public SqlIndex(string indexName, string indexType, string indexColumns)
    {
        IndexName = indexName;
        IndexType = indexType;
        IndexKeys = indexColumns;
    }

    public string IndexName { get; }
    public string IndexType { get; }
    public string IndexKeys { get; }
}
