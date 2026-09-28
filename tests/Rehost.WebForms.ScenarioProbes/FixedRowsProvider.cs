using System.Collections;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

namespace Rehost.WebForms.ScenarioProbes;

public sealed class FixedRowsProviderFactory : DbProviderFactory
{
    public static readonly FixedRowsProviderFactory Instance = new();

    public override DbConnection CreateConnection() => new FixedRowsConnection();

    public override DbCommand CreateCommand() => new FixedRowsCommand();
}

internal sealed class FixedRowsConnection : DbConnection
{
    private ConnectionState state = ConnectionState.Closed;

    [AllowNull]
    public override string ConnectionString { get; set; } = string.Empty;

    public override string Database => string.Empty;

    public override string DataSource => string.Empty;

    public override string ServerVersion => string.Empty;

    public override ConnectionState State => state;

    public override void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    public override void Close() => state = ConnectionState.Closed;

    public override void Open() => state = ConnectionState.Open;

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
        throw new NotSupportedException();

    protected override DbCommand CreateDbCommand() => new FixedRowsCommand { Connection = this };
}

internal sealed class FixedRowsCommand : DbCommand
{
    [AllowNull]
    public override string CommandText { get; set; } = string.Empty;

    public override int CommandTimeout { get; set; }

    public override CommandType CommandType { get; set; } = CommandType.Text;

    public override bool DesignTimeVisible { get; set; }

    public override UpdateRowSource UpdatedRowSource { get; set; }

    protected override DbConnection? DbConnection { get; set; }

    protected override DbParameterCollection DbParameterCollection { get; } = new FixedRowsParameters();

    protected override DbTransaction? DbTransaction { get; set; }

    public override void Cancel()
    {
    }

    public override int ExecuteNonQuery() => throw new NotSupportedException();

    public override object? ExecuteScalar() => throw new NotSupportedException();

    public override void Prepare()
    {
    }

    protected override DbParameter CreateDbParameter() => throw new NotSupportedException();

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        var rows = new DataTable();
        rows.Columns.Add("Name", typeof(string));
        rows.Rows.Add("alpha");
        rows.Rows.Add("beta");
        return rows.CreateDataReader();
    }
}

internal sealed class FixedRowsParameters : DbParameterCollection
{
    private readonly List<DbParameter> items = [];

    public override int Count => items.Count;

    public override object SyncRoot => items;

    public override int Add(object value)
    {
        items.Add((DbParameter)value);
        return items.Count - 1;
    }

    public override void AddRange(Array values)
    {
        foreach (var value in values)
        {
            Add(value!);
        }
    }

    public override void Clear() => items.Clear();

    public override bool Contains(object value) => items.Contains((DbParameter)value);

    public override bool Contains(string value) => IndexOf(value) >= 0;

    public override void CopyTo(Array array, int index) => ((ICollection)items).CopyTo(array, index);

    public override IEnumerator GetEnumerator() => items.GetEnumerator();

    public override int IndexOf(object value) => items.IndexOf((DbParameter)value);

    public override int IndexOf(string parameterName) =>
        items.FindIndex(parameter => parameter.ParameterName == parameterName);

    public override void Insert(int index, object value) => items.Insert(index, (DbParameter)value);

    public override void Remove(object value) => items.Remove((DbParameter)value);

    public override void RemoveAt(int index) => items.RemoveAt(index);

    public override void RemoveAt(string parameterName) => items.RemoveAt(IndexOf(parameterName));

    protected override DbParameter GetParameter(int index) => items[index];

    protected override DbParameter GetParameter(string parameterName) => items[IndexOf(parameterName)];

    protected override void SetParameter(int index, DbParameter value) => items[index] = value;

    protected override void SetParameter(string parameterName, DbParameter value) =>
        items[IndexOf(parameterName)] = value;
}
