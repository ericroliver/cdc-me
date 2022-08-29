using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Softbase;

public class ProfileDiffer
{
    public ProfileDiffer()
    {

    }

    public IDictionary<string, IDictionary<string,object>> Diff(IEnumerable<SqlTable> tables,
                            IDictionary<string, IEnumerable<IDictionary<string, object>>> leftProfile,
                            IDictionary<string, IEnumerable<IDictionary<string, object>>> rightProfile)
    {

        var diffResult = new Dictionary<string, IDictionary<string, object>>();

        foreach (var table in tables)
        {

            var changeInstance = $"{table.Schema}_{table.Name}";
            var left = default(IEnumerable<IDictionary<string, object>>);
            var right = default(IEnumerable<IDictionary<string, object>>);
            var index = table.GetPrimaryIndex();
            var tableObject = new Dictionary<string,object>();

            tableObject["table"] = table;
            tableObject["index"] = index;

            var tableDiff = new List<Diff>();
            tableObject["diff"] = tableDiff;
            
            if (index == null)
                continue;

            if (leftProfile.ContainsKey(changeInstance))
                left = leftProfile[changeInstance];
            if (rightProfile.ContainsKey(changeInstance))
                right = rightProfile[changeInstance];

            if (left != null && right != null)
            {
                var leftIndex = Index(index, left);
                var rightIndex = Index(index, right);

                foreach (var kv in leftIndex)
                {
                    var leftRecord = kv.Value;

                    var diff = new Diff();
                    diff.Key = kv.Key;
                    diff.Left = leftRecord;
                    diff.Action = DiffType.None;

                    if (rightIndex.ContainsKey(kv.Key))
                    {
                        var rightRecord = rightIndex[kv.Key];
                        diff.Right = rightRecord;
                        diff.Changes = GetChangedFields(leftRecord, rightRecord);
                    }

                    if (diff.Changes?.Count() > 0)
                    {
                        diff.Action = DiffType.Changed;
                        tableDiff.Add(diff);
                    }
                }
            }
            else if (left != null && right == null)
            {
                var leftIndex = Index(index, left);

                foreach (var kv in leftIndex)
                {
                    var leftRecord = kv.Value;

                    var diff = new Diff();
                    diff.Key = kv.Key;
                    diff.Left = leftRecord;
                    diff.Action = DiffType.Deleted;

                    tableDiff.Add(diff);
                }

            }
            else if (left == null && right != null)
            {
                var rightIndex = Index(index, right);

                foreach (var kv in rightIndex)
                {
                    var rightRecord = kv.Value;

                    var diff = new Diff();
                    diff.Key = kv.Key;
                    diff.Right = rightRecord;
                    diff.Action = DiffType.New;

                    tableDiff.Add(diff);
                }

            }

            if (tableDiff.Count() > 0)
                diffResult![changeInstance] = tableObject;
        }

        return diffResult!;
    }

    private string[] GetFieldsToIgnore()
    {
        return new[] { "__$start_lsn", "__$operation" };
    }
    private IDictionary<string, ValueDiff> GetChangedFields(IDictionary<string, object> left, IDictionary<string, object> right)
    {
        var diff = new Dictionary<string, ValueDiff>();
        var fieldsToIgnore = GetFieldsToIgnore();

        foreach (var kv in left)
        {

            if (fieldsToIgnore.Contains(kv.Key))
                continue;

            var valDiff = new ValueDiff();
            valDiff.Action = DiffType.None;
            valDiff.Left = kv.Value;

            if (right.ContainsKey(kv.Key))
            {
                var rv = right[kv.Key];

                if (!object.Equals(kv.Value, rv))
                {
                    if (kv.Value is DateTime)
                    {
                        var dt = (DateTime)rv;
                        var min = DateTime.Now.Subtract(TimeSpan.FromDays(1));
                        var max = DateTime.Now.Add(TimeSpan.FromDays(1));
                        if (dt.Ticks < min.Ticks || dt.Ticks > max.Ticks)
                        {
                            valDiff.Action = DiffType.Changed;
                            valDiff.Right = rv;
                        }
                    }
                    else
                    {
                        valDiff.Action = DiffType.Changed;
                        valDiff.Right = rv;
                    }
                }
            }
            else
            {
                valDiff.Action = DiffType.Deleted;
            }

            if (valDiff.Action != DiffType.None)
            {
                diff[kv.Key] = valDiff;
            }
        }

        foreach (var kv in right)
        {
            if (!left.ContainsKey(kv.Key))
            {

                var valDiff = new ValueDiff();
                valDiff.Action = DiffType.Deleted;
                valDiff.Right = kv.Value;
            }
        }

        return diff;
    }

    private IDictionary<string, IDictionary<string, object>> Index(SqlIndex index, IEnumerable<IDictionary<string, object>> records)
    {
        var indexedRecords = new Dictionary<string, IDictionary<string, object>>();

        foreach (var record in records)
        {
            var key = GetRecordKey(index, record);
            indexedRecords[key] = record;
        }

        return indexedRecords;
    }

    private string GetRecordKey(SqlIndex index, IDictionary<string, object> record)
    {
        var sb = new StringBuilder();

        foreach (var field in index.IndexKeys.Split(","))
        {
            sb.Append(record.ContainsKey(field) ? Convert.ToString(record[field]) : "<null>");
        }

        return sb.ToString();
    }
}

public enum DiffType { None = 0, New = 1, Changed = 2, Deleted = 3 }

public class ValueDiff
{
    public DiffType Action { get; set; }
    public object Left { get; set; }
    public object Right { get; set; }
}

public class Diff
{
    public DiffType Action { get; set; }
    public string Key { get; set; }
    public IDictionary<string, object> Left { get; set; }
    public IDictionary<string, object> Right { get; set; }
    public IDictionary<string, ValueDiff> Changes { get; set; }

}