using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cirrious.MvvmCross.Community.Plugins.Sqlite;

namespace Community.SQLite
{
    /// <summary>
    /// a No-Expressions Table Query
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class NxTableQuery<T> : INxTableQuery<T> where T : new()
    {
        private static readonly object[] EmptyArgs = new object[0];

        public SQLiteConnection Connection { get; private set; }
        ISQLiteConnection INxTableQuery<T>.Connection { get { return Connection; } }

        public TableMapping Table { get; private set; }
        ITableMapping INxTableQuery<T>.Table { get { return Table; } }

        private string _overwriteTableName;
        private List<string> _where;
        private List<object> _whereArgs;

        private string _orderBy;
        private int? _limit;
        private int? _offset;

        private NxTableQuery(SQLiteConnection conn, TableMapping table)
        {
            Connection = conn;
            Table = table;
        }

        public NxTableQuery(SQLiteConnection conn)
        {
            Connection = conn;
            Table = Connection.GetMapping(typeof (T));
        }

        public NxTableQuery(SQLiteConnection conn, string overwriteTableName)
            : this(conn)
        {
            _overwriteTableName = overwriteTableName;
        }

        public INxTableQuery<U> Clone<U>() where U : new()
        {
            var q = new NxTableQuery<U>(Connection, Table);
            q._overwriteTableName = _overwriteTableName;
            q._deferred = _deferred;
            q._orderBy = _orderBy;
            q._limit = _limit;
            q._offset = _offset;
            q._where = _where == null ? null : _where.ToList();
            q._whereArgs = _whereArgs == null ? null : _whereArgs.ToList();
            return q;
        }

        /// <summary>
        /// can be used multiple times, all clauses are combined with "AND"
        /// </summary>
        public INxTableQuery<T> Where(string where)
        {
            if (_where == null)
                _where = new List<string>();
            _where.Add(where);
            return this;
        }

        /// <summary>
        /// can be used multiple times, all clauses are combined with "AND"
        /// </summary>
        public INxTableQuery<T> Where(string where, object[] args)
        {
            if (_where == null)
                _where = new List<string>();
            if (_whereArgs == null)
                _whereArgs = new List<object>();

            _where.Add(where);
            _whereArgs.AddRange(args);
            return this;
        }

        public INxTableQuery<T> Take(int n)
        {
            _limit = n;
            return this;
        }

        public INxTableQuery<T> Skip(int n)
        {
            _offset = n;
            return this;
        }

        public T ElementAt(int index)
        {
            return Skip(index).Take(1).First();
        }

        private bool _deferred;
        private string _selector;

        public INxTableQuery<T> Deferred()
        {
            _deferred = true;
            return this;
        }

        public INxTableQuery<T> OrderBy(string orderBy)
        {
            _orderBy = orderBy;
            return this;
        }

        public INxTableQuery<TResult> Select<TResult>(string selector) where TResult : new()
        {
            var q = (NxTableQuery<TResult>)Clone<TResult>();
            q._selector = selector;
            return q;
        }

        private ISQLiteCommand GenerateCommand(string selectionList)
        {
            StringBuilder bld = new StringBuilder();
            bld.Append("SELECT ");
            bld.Append(selectionList);
            bld.Append(" FROM \"");
            bld.Append(_overwriteTableName ?? Table.TableName);
            bld.Append("\"");

            if (_where != null)
            {
                bld.Append(" WHERE ( ");
                bld.Append(string.Join(" ) AND ( ", _where));
                bld.Append(" )");
            }
            if (_orderBy != null)
            {
                bld.Append(" ORDER BY ");
                bld.Append(_orderBy);
            }
            if (_limit.HasValue)
            {
                bld.Append(" LIMIT ");
                bld.Append(_limit.Value.ToString(CultureInfo.InvariantCulture));
            }
            if (_offset.HasValue)
            {
                if (!_limit.HasValue)
                {
                    bld.Append(" LIMIT -1 ");
                }
                bld.Append(" OFFSET ");
                bld.Append(_offset.Value.ToString(CultureInfo.InvariantCulture));
            }

            return Connection.CreateCommand(bld.ToString(), _whereArgs == null?EmptyArgs:_whereArgs.ToArray());
        }

        public int Count()
        {
            return GenerateCommand("COUNT(*)").ExecuteScalar<int>();
        }

        public IEnumerator<T> GetEnumerator()
        {
            if (!_deferred)
                return GenerateCommand(_selector ?? "*").ExecuteQuery<T>().GetEnumerator();

            return GenerateCommand(_selector ?? "*").ExecuteDeferredQuery<T>().GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public T First()
        {
            var query = Take(1);
            return query.ToList<T>().First();
        }

        public T FirstOrDefault()
        {
            var query = Take(1);
            return query.ToList<T>().FirstOrDefault();
        }
    }
}