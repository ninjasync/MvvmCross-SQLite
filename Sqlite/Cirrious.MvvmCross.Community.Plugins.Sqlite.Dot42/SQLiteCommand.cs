using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Android.Database;
using Android.Database.Sqlite;
using Android.OS;
using Cirrious.MvvmCross.Community.Plugins.Sqlite;
using Debug = System.Diagnostics.Debug;
using Environment = System.Environment;

namespace Community.SQLite
{
    public partial class SQLiteCommand : ISQLiteCommand, SQLiteDatabase.ICursorFactory
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        // parameter binding:
        // http://stackoverflow.com/questions/20911760/android-how-to-query-sqlitedatabase-with-non-string-selection-args

        readonly SQLiteConnection _conn;
        private readonly List<Binding> _bindings;

        public string CommandText { get; set; }

        internal SQLiteCommand(SQLiteConnection conn)
        {
            _conn = conn;
            _bindings = new List<Binding>();
            CommandText = "";
        }

        public int ExecuteNonQuery()
        {
            if (_conn.Trace)
            {
                Debug.WriteLine("Executing: " + this);
            }
            
            SQLiteStatement stmt = null;
            try
            {
                stmt = PrepareScalarQuery();
                return stmt.ExecuteUpdateDelete();
            }
            catch (SQLException ex)
            {
                throw SQLiteException.New(ex);
            }
            finally
            {
                if(stmt != null)
                    stmt.Close();
            }
        }

        public IEnumerable<T> ExecuteDeferredQuery<T>()
        {
            if (IsRowType(typeof(T)))
                return ExecuteDeferredQueryForPrimitive<T>();

            return ExecuteDeferredQuery<T>(_conn.GetMapping(typeof(T)));
        }

        public List<T> ExecuteQuery<T>()
        {
            if (IsRowType(typeof(T)))
                return ExecuteDeferredQueryForPrimitive<T>().ToList();

            return ExecuteDeferredQuery<T>().ToList();
        }

        public List<T> ExecuteQuery<T>(ITableMapping map)
        {
            return ExecuteDeferredQuery<T>(map).ToList();
        }

        /// <summary>
        /// Invoked every time an instance is loaded from the database.
        /// </summary>
        /// <param name='obj'>
        /// The newly created object.
        /// </param>
        /// <remarks>
        /// This can be overridden in combination with the <see cref="SQLiteConnection.NewCommand"/>
        /// method to hook into the life-cycle of objects.
        ///
        /// Type safety is not possible because MonoTouch does not support virtual generic methods.
        /// </remarks>
        protected virtual void OnInstanceCreated(object obj)
        {
            // Can be overridden.
        }

        public IEnumerable<T> ExecuteDeferredQuery<T>(ITableMapping map)
        {
            if (_conn.Trace)
            {
                Debug.WriteLine("Executing Query: " + this);
            }

            ICursor cursor = null;
            
            try
            {
                try
                {
                    cursor = _conn.Handle.RawQueryWithFactory(this, CommandText, null, "" /* wtf is an edit table?*/);
                }
                catch (SQLException ex)
                {
                    throw SQLiteException.New(ex);
                } 
                
                var cols = new TableMapping.Column[cursor.ColumnCount];
                var colNames = cursor.ColumnNames;
                for (int i = 0; i < cols.Length; i++)
                {
                    var name = colNames[i];
                    cols[i] = ((TableMapping)map).FindColumn(name);
                }

                while (true)
                {
                    T obj;
                    try
                    {
                        if (!cursor.MoveToNext()) 
                            break;

                        obj = (T)Activator.CreateInstance(((TableMapping)map).MappedType);
                        for (int i = 0; i < cols.Length; i++)
                        {
                            if (cols[i] == null)
                                continue;

                            var colType = cursor.GetType(i); // API level 11, used for IsNull and NaN check.
                            var val = ReadCol(cursor, i, colType, cols[i].ColumnType);
                            cols[i].SetValue(obj, val);
                        }
                        OnInstanceCreated(obj);
                    }
                    catch (SQLException ex)
                    {
                        throw SQLiteException.New(ex);
                    } 

                    yield return obj;
                }
            }
            finally
            {
                if(cursor != null)
                    cursor.Close();
            }
        }

        public IEnumerable<T> ExecuteDeferredQueryForPrimitive<T>()
        {
            if (_conn.Trace)
            {
                Debug.WriteLine("Executing Query: " + this);
            }

            ICursor cursor = null;

            try
            {
                try
                {
                    cursor = _conn.Handle.RawQueryWithFactory(this, CommandText, null, "" /* wtf is an edit table?*/);
                }
                catch (SQLException ex)
                {
                    throw SQLiteException.New(ex);
                }

                var type = typeof(T);

                while (true)
                {
                    T value;
                    try
                    {
                        if (!cursor.MoveToNext())
                            break;

                        var colType = cursor.GetType(0); // API level 11, only used for IsNull check.
                        value= (T)ReadCol(cursor, 0, colType, type);
                    }
                    catch (SQLException ex)
                    {
                        throw SQLiteException.New(ex);
                    }

                    yield return value;
                }
            }
            finally
            {
                if (cursor != null)
                    cursor.Close();
            }
        }

        public T ExecuteScalar<T>()
        {
            if (_conn.Trace)
            {
                Debug.WriteLine("Executing Query: " + this);
            }

            var type = typeof (T);

            bool isIntegerType = type == typeof (int) || type == typeof (long) || type == typeof (short) || type == typeof (byte);
            bool isStringType = type == typeof (string);
            if (isIntegerType || isStringType)
            {
                SQLiteStatement stmt = null;
                try
                {
                    stmt = PrepareScalarQuery();
                    if (isStringType)
                    {
                        return (T) (object) stmt.SimpleQueryForString();
                    }
                    else
                    {
                        long l = stmt.SimpleQueryForLong();

                        if (type == typeof (int))
                            return (T) (object) (int) l;
                        else if (type == typeof (byte))
                            return (T) (object) (byte) l;
                        else if (type == typeof (short))
                            return (T) (object) (short) l;
                        else if (type == typeof (long))
                            return (T) (object) l;
                    }
                }
                catch (SQLiteDoneException)
                {
                    return default(T);
                }
                catch (SQLException ex)
                {
                    throw SQLiteException.New(ex);
                }
                finally
                {
                    if (stmt != null)
                        stmt.Close();
                }
            }
            else
            {
                ICursor cursor = null;
                try
                {
                    cursor = _conn.Handle.RawQueryWithFactory(this, CommandText, null, "" /* wtf is an edit table?*/);
                    if (!cursor.MoveToFirst())
                        return default(T);

                    var colType = cursor.GetType(0);
                    return (T)ReadCol(cursor, 0, colType, type);
                }
                catch (SQLException ex)
                {
                    throw SQLiteException.New(ex);
                } 
                finally
                {
                    if (cursor != null)
                        cursor.Close();
                }
            }
            
            return default(T);
        }

        //public void Bind(string name, object val)
        //{
        //    throw new SQLException("named parameters not supported on android");
        //    //_bindings.Add(new Binding
        //    //{
        //    //    Name = name,
        //    //    Value = val
        //    //});
        //}

        public void Bind(object val)
        {
            _bindings.Add(new Binding
                {
                    Name = null,
                    Value = val
                });
        }

        public override string ToString()
        {
            var parts = new string[1 + _bindings.Count];
            parts[0] = CommandText;
            var i = 1;
            foreach (var b in _bindings)
            {
                parts[i] = string.Format("  {0}: {1}", i - 1, b.Value);
                i++;
            }
            return string.Join(Environment.NewLine, parts);
        }

        SQLiteStatement PrepareScalarQuery()
        {
            var stmt = _conn.Handle.CompileStatement(CommandText);
            BindAll(stmt);
            return stmt;
        }

        void BindAll(SQLiteProgram stmt)
        {
            int nextIdx = 1;
            foreach (var b in _bindings)
            {
                //if (b.Name != null)
                //{
                //    b.Index = SQLite3.BindParameterIndex(stmt, b.Name);
                //}
                //else
                {
                    b.Index = nextIdx++;
                }

                BindParameter(stmt, b.Index, b.Value, _conn.StoreDateTimeAsTicks);
            }
        }

        public ICursor NewCursor(SQLiteDatabase db, ISQLiteCursorDriver masterQuery, string editTable, SQLiteQuery query)
        {
            BindAll(query);
            if (Build.VERSION.SDK_INT < 11)
            {
                return new SQLiteCursor(db, masterQuery, editTable, query);
            }
            else
            {
                return new SQLiteCursor(masterQuery, editTable, query);
            }
        }

        internal static void BindParameter(SQLiteProgram stmt, int index, object value, bool storeDateTimeAsTicks)
        {
            if (value == null)
            {
                stmt.BindNull(index);
            }
            else
            {
                // NOTE: we are unable to distinguish between signed and unsigned values.
                // NOTE: the way Dot42 works, for primitives, the javagetclass will return
                //       the nullable type of the primitive.
                var type = value.JavaGetClass();
                if (type == typeof(int?))
                {
                    stmt.BindLong(index, (int)value);
                }
                else if (type == typeof(byte?))  // treat byte/sbyte as unsigned.
                {
                    stmt.BindLong(index, (byte)value);
                }
                else if (type == typeof(short?)) 
                {
                    stmt.BindLong(index, (short)value);
                }
                else if (type == typeof(long?)) 
                {
                    stmt.BindLong(index, (long)value);
                }
                else if (type == typeof(string))
                {
                    stmt.BindString(index, (string)value);
                }
                else if (type == typeof(bool?))
                {
                    stmt.BindLong(index, (bool)value ? 1 : 0);
                }
                else if (type == typeof(float?) || type == typeof(double?) || type == typeof(decimal))
                {
                    // FIXME: saving decimal as double is certainly wrong!
                    stmt.BindDouble(index, Convert.ToDouble(value));
                }
                else if (type == typeof(TimeSpan))
                {
                    stmt.BindLong(index, ((TimeSpan)value).Ticks);
                }
                else if (type == typeof(DateTime))
                {
                    if (storeDateTimeAsTicks)
                    {
                        stmt.BindLong(index, ((DateTime)value).Ticks);
                    }
                    else
                    {
                        stmt.BindString(index, ((DateTime)value).ToString(DateTimeFormat, CultureInfo.InvariantCulture));
                    }
                    //}
                //else if (value is DateTimeOffset)
                //{
                //    SQLite3.BindInt64(stmt, index, ((DateTimeOffset)value).UtcTicks);
#if !USE_NEW_REFLECTION_API
                }
                else if (type.IsEnum)
                {
#else
				} else if (value.GetType().GetTypeInfo().IsEnum) {
#endif
                    stmt.BindLong(index, Convert.ToInt64(value));
                }
                else if (type == typeof(byte[]))
                {
                    stmt.BindBlob(index, (byte[])value);
                }
                else if (value is Guid)
                {
                    stmt.BindString(index, ((Guid)value).ToString());
                }
                else
                {
                    throw new NotSupportedException("Cannot store type: " + value.GetType());
                }
            }
        }

        class Binding
        {
            public string Name { get; set; }

            public object Value { get; set; }

            public int Index { get; set; }
        }

        object ReadCol(ICursor cursor, int index, int fieldType, Type clrType)
        {
            if (fieldType == ICursorConstants.FIELD_TYPE_NULL)
            {
                return null;
            }
            else
            {
                if (clrType == typeof(String))
                {
                    return cursor.GetString(index);
                }
                else if (clrType == typeof(Int32))
                {
                    return (int) cursor.GetInt(index);
                }
                else if (clrType == typeof(Boolean))
                {
                    return cursor.GetInt(index) != 0;
                }
                else if (clrType == typeof(double))
                {
                    // http://sqlite.1065341.n5.nabble.com/NaN-in-0-0-out-td19086.html
                    if (fieldType != ICursorConstants.FIELD_TYPE_FLOAT && fieldType != ICursorConstants.FIELD_TYPE_INTEGER)
                        return double.NaN;

                    return cursor.GetDouble(index);
                }
                else if (clrType == typeof(float))
                {
                    // http://sqlite.1065341.n5.nabble.com/NaN-in-0-0-out-td19086.html
                    if (fieldType != ICursorConstants.FIELD_TYPE_FLOAT && fieldType != ICursorConstants.FIELD_TYPE_INTEGER)
                        return float.NaN;

                    return cursor.GetFloat(index);
                }
                else if (clrType == typeof(TimeSpan))
                {
                    return new TimeSpan(cursor.GetLong(index));
                }
                else if (clrType == typeof(DateTime))
                {
                    if (_conn.StoreDateTimeAsTicks)
                    {
                        return new DateTime(cursor.GetLong(index));
                    }
                    else
                    {
                        var text = cursor.GetString(index);
                        return DateTime.ParseExact(text, DateTimeFormat, CultureInfo.InvariantCulture);
                    }
                //}
                //else if (clrType == typeof(DateTimeOffset))
                //{
                //    return new DateTimeOffset(SQLite3.ColumnInt64(stmt, index), TimeSpan.Zero);
#if !USE_NEW_REFLECTION_API
                }
                else if (clrType.IsEnum)
                {
#else
				} else if (clrType.GetTypeInfo().IsEnum) {
#endif
                    return cursor.GetInt(index);
                }
                else if (clrType == typeof(Int64))
                {
                    return cursor.GetLong(index);
                }
                //else if (clrType == typeof(UInt32))
                //{
                //    return (uint)SQLite3.ColumnInt64(stmt, index);
                //}
                else if (clrType == typeof(decimal))
                {
                    // FIXME: saving decimal as double is certainly wrong!
                    return (decimal)cursor.GetDouble(index);
                }
                else if (clrType == typeof(Byte))
                {
                    return (byte)cursor.GetInt(index);
                }
                //else if (clrType == typeof(UInt16))
                //{
                //    return (ushort)SQLite3.ColumnInt(stmt, index);
                //}
                else if (clrType == typeof(Int16))
                {
                    return (short)cursor.GetInt(index);
                }
                //else if (clrType == typeof(sbyte))
                //{
                //    return (sbyte)SQLite3.ColumnInt(stmt, index);
                //}
                else if (clrType == typeof(byte[]))
                {
                    return cursor.GetBlob(index);
                }
                else if (clrType == typeof(Guid))
                {
                    var text = cursor.GetString(index);
                    return new Guid(text);
                }
                else
                {
                    throw new NotSupportedException("Don't know how to read " + clrType);
                }
            }
        }

        private bool IsRowType(Type t)
        {
            return t.IsPrimitive || t.IsEnum
                || t == typeof(DateTime) || t == typeof(string) || t == typeof(TimeSpan)
                || t == typeof(Guid) || t == typeof(byte[]);
        }
    }
}
