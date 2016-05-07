using System;
using System.Diagnostics;

namespace Cirrious.MvvmCross.Community.Plugins.Sqlite
{
    internal class ExecutionTimer
    {
        private readonly ISQLiteConnection _conn;
        private long _totalMilliseconds;

        public ExecutionTimer(ISQLiteConnection conn)
        {
            _conn = conn;
        }

        public IDisposable Time(string msg = null)
        {
            if (!_conn.TimeExecution || _conn.TraceFunc == null)
                return null;
            return new Timer(this, msg);
        }

        internal class Timer : IDisposable
        {
            private readonly ExecutionTimer _t;
            private readonly string _msg;
            private readonly Stopwatch _watch = new Stopwatch();
        
            public Timer(ExecutionTimer t, string msg)
            {
                _t = t;
                _msg = msg;
                _watch.Start();
            }

            public void Dispose()
            {
                _watch.Stop();
                _t._totalMilliseconds += _watch.ElapsedMilliseconds;
                _t._conn.TraceFunc(string.Format("Finished {0}in {1} ms ({2:0.0} s total)",
                                    _msg==null?"": _msg + " ",  _watch.ElapsedMilliseconds, 
                                    _t._totalMilliseconds / 1000.0));

            }
        }
    }

    
}
