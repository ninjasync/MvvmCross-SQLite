using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Android.Database;
using Android.Database.Sqlite;

namespace Community.SQLite
{
    /// <summary>
    /// Since the insert never changed, we only need to prepare once.
    /// </summary>
    public class PreparedSqlLiteInsertCommand : IDisposable
    {
        public bool Initialized { get; set; }

        protected SQLiteConnection Connection { get; set; }

        public string CommandText { get; set; }

        protected SQLiteStatement Statement { get; set; }
        internal static readonly SQLiteStatement NullStatement = default(SQLiteStatement);

        internal PreparedSqlLiteInsertCommand(SQLiteConnection conn)
        {
            Connection = conn;
        }

        public int ExecuteNonQuery(object[] source)
        {
            if (Connection.Trace)
            {
                Debug.WriteLine("Executing: " + CommandText);
            }

            var r = SQLite3.Result.OK;

            if (!Initialized)
            {
                Statement = Prepare();
                Initialized = true;
            }

            //bind the values.
            if (source != null)
            {
                for (int i = 0; i < source.Length; i++)
                {
                    SQLiteCommand.BindParameter(Statement, i + 1, source[i], Connection.DateTimeFormat);
                }
            }

            try
            {
                return Statement.ExecuteUpdateDelete();
            }
            catch (SQLException ex)
            {
                throw SQLiteException.New(ex);
            }
        }

        protected virtual SQLiteStatement Prepare()
        {
            var stmt = Connection.Handle.CompileStatement(CommandText);
            return stmt;
        }

        public void Dispose()
        {
            if (Statement != NullStatement)
            {
                try
                {
                    Statement.Close();
                }
                finally
                {
                    Statement = NullStatement;
                    Connection = null;
                }
            }
        }
    }
}
