using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Android.Database;
using Android.Database.Sqlite;

namespace Community.SQLite
{
    internal static class DatabaseFactory
    {
        private class Entry
        {
            public readonly string FileName;
            public readonly SQLiteDatabase Db;
            public int Usage;

            public Entry(SQLiteDatabase db, string fileName)
            {
                Db = db;
                FileName = fileName;
            }
        }

        static readonly List<Entry> Connections = new List<Entry>();

        public static SQLiteDatabase Open(string name, SQLiteOpenFlags openFlags)
        {
            
            lock (Connections)
            {
                Entry e = Connections.FirstOrDefault(entry => entry.FileName == name);
                if (e == null)
                {
                    // Note that,since we only use a single SQLiteDatabase
                    // these flags are only evaluated upon the first opening.
                    // We therefore ignore the isWrite flag.
                    bool isCreate = (openFlags & SQLiteOpenFlags.Create) != 0;
                    //bool isWrite = (openFlags & SQLiteOpenFlags.ReadWrite) != 0;
                    //bool isSharedCache = (openFlags & SQLiteOpenFlags.SharedCache) != 0;
                    //bool isPrivateCache = (openFlags & SQLiteOpenFlags.PrivateCache) != 0;

                    try
                    {
                        Debug.WriteLine("opening database {0}", name);
                        var db = SQLiteDatabase.OpenDatabase(name, null, isCreate ? SQLiteDatabase.CREATE_IF_NECESSARY : SQLiteDatabase.OPEN_READWRITE);
                        Connections.Add(e = new Entry(db, name));
                    }
                    catch (SQLException ex)
                    {
                        throw SQLiteException.New(ex);
                    }
                }

                e.Usage += 1;

                Debug.WriteLine("usage of {0} is now {1}", e.FileName, e.Usage);

                return e.Db;
            }
        }

        public static void Close(SQLiteDatabase db)
        {
            lock (Connections)
            {
                Entry e = Connections.FirstOrDefault(entry => ReferenceEquals(entry.Db, db));
                
                if (e == null || e.Usage == 0) 
                    throw new Exception("database already closed");

                e.Usage -= 1;
                Debug.WriteLine("reduced usage count of database {0} to {1}", e.FileName, e.Usage);

                // Apparently, Android doesn't want us to clean up for some reason.
                // If we close the database, we will not be able to query agains the reopened 
                // database, and get a "java.lang.IllegalStateException: Cannot perform this operation because the connection pool has been closed."
                // For me, this doesn't make sense. But what you gonna do? 
                // Se also.
                // http://touchlabblog.tumblr.com/post/24474750219/single-sqlite-connection
                // http://stackoverflow.com/questions/23293572/android-cannot-perform-this-operation-because-the-connection-pool-has-been-clos
                //
                // but see also:
                // http://stackoverflow.com/questions/1483629/exception-attempt-to-acquire-a-reference-on-a-close-sqliteclosable
                // http://darutk-oboegaki.blogspot.de/2011/03/sqlitedatabase-is-closed-automatically.html
                //if (e.Usage == 0)
                //{
                //    try
                //    {
                //        Debug.WriteLine("releasing database {0}", e.FileName);
                //        //e.Db.Close();
                //        //e.Db.ReleaseReference();
                //    }
                //    catch (SQLException ex)
                //    {
                //        throw SQLiteException.New(ex);
                //    }
                //    finally
                //    {
                //        Connections.Remove(e);    
                //    }
                //}
                //else
                //{
                //    Debug.WriteLine("reduced usage count of database {0} to {1}", e.FileName, e.Usage);
                //}
            }
        }
    }
}
