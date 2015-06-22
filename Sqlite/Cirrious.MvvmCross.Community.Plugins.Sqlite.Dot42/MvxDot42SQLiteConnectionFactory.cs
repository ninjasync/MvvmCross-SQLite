// MvxDroidSQLiteConnectionFactory.cs
// (c) Copyright Cirrious Ltd. http://www.cirrious.com
// MvvmCross is licensed using Microsoft Public License (Ms-PL)
// Contributions and inspirations noted in readme.md and license.txt
// 
// Project Lead - Stuart Lodge, @slodge, me@slodge.com
#pragma warning disable 1591

using System;
using System.IO;
using Android.App;
using Community.SQLite;

namespace Cirrious.MvvmCross.Community.Plugins.Sqlite.Dot42
{
    public class MvxDot42SQLiteConnectionFactory
        : MvxBaseSQLiteConnectionFactory
    {
        protected override string GetDefaultBasePath()
        {
            var path = Application.Context.GetDatabasePath("any").Parent;
            Directory.CreateDirectory(path);
            return path;
        }

        protected override string LocalPathCombine(string path1, string path2)
        {
            return Path.Combine(path1, path2);
        }

        protected override ISQLiteConnection CreateSQLiteConnection(string databasePath, DateTimeFormat storeDateTimeAsTicks)
        {
            return new SQLiteConnection(databasePath, storeDateTimeAsTicks);
        }
    }
}