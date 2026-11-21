using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ForeverHive {
internal sealed class Db : IDisposable {
    IntPtr handle;
    const string Dll = "winsqlite3.dll";
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open16([MarshalAs(UnmanagedType.LPWStr)] string path, out IntPtr db);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_exec(IntPtr db, byte[] sql, Callback cb, IntPtr ctx, out IntPtr error);
    [DllImport(Dll, CallingConvention=CallingConvention.Cdecl)] static extern void sqlite3_free(IntPtr mem);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] delegate int Callback(IntPtr ctx, int n, IntPtr values, IntPtr names);
    static string Utf(IntPtr p) {
        if (p==IntPtr.Zero) return null;
        int n=0; while (Marshal.ReadByte(p,n)!=0) n++;
        byte[] data=new byte[n]; Marshal.Copy(p,data,0,n); return Encoding.UTF8.GetString(data);
    }
    public static string Q(string value) {return value==null?"NULL":"'"+value.Replace("'","''").Replace("\0","")+"'";}
    public static long Now {get{return DateTimeOffset.UtcNow.ToUnixTimeSeconds();}}
    public Db(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        if(sqlite3_open16(path,out handle)!=0) throw new IOException("Could not open the market database.");
        Exec("PRAGMA busy_timeout=10000; PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;");
    }
    void Run(string sql, Callback callback) {
        IntPtr error;
        int rc=sqlite3_exec(handle,Encoding.UTF8.GetBytes(sql+"\0"),callback,IntPtr.Zero,out error);
        if(rc!=0){string msg=Utf(error); sqlite3_free(error);throw new InvalidDataException("SQLite: "+msg);}
    }
    public void Exec(string sql){Run(sql,null);}
    public DataTable Query(string sql) {
        DataTable table=new DataTable();
        Run(sql,delegate(IntPtr ctx,int n,IntPtr vals,IntPtr names){
            object[] row=new object[n];
            for(int i=0;i<n;i++){
                if(table.Columns.Count<=i)table.Columns.Add(Utf(Marshal.ReadIntPtr(names,i*IntPtr.Size)));
                row[i]=(object)Utf(Marshal.ReadIntPtr(vals,i*IntPtr.Size))??DBNull.Value;
            }
            table.Rows.Add(row);return 0;
        });return table;
    }
    public string Scalar(string sql) {var t=Query(sql);return t.Rows.Count==0?"":t.Rows[0][0].ToString();}
    public void Transaction(Action act){Exec("BEGIN IMMEDIATE");try{act();Exec("COMMIT");}catch{Exec("ROLLBACK");throw;}}
    public void Dispose(){if(handle!=IntPtr.Zero){sqlite3_close(handle);handle=IntPtr.Zero;}}
    public void Init(){
        Exec(@"CREATE TABLE IF NOT EXISTS meta(k TEXT PRIMARY KEY,v TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS item(id INTEGER PRIMARY KEY,name TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS quote(market TEXT NOT NULL,id INTEGER NOT NULL,src TEXT NOT NULL,ts INTEGER NOT NULL,ask INTEGER NOT NULL CHECK(ask>0),qty INTEGER,ref INTEGER,sold INTEGER,PRIMARY KEY(market,src,ts,id)) WITHOUT ROWID;
        CREATE INDEX IF NOT EXISTS qitem ON quote(market,id,ts DESC);
        CREATE TABLE IF NOT EXISTS daily(market TEXT,id INTEGER,day INTEGER,low INTEGER,high INTEGER,available INTEGER,PRIMARY KEY(market,id,day)) WITHOUT ROWID;
        CREATE TABLE IF NOT EXISTS inventory(character TEXT,location TEXT,id INTEGER,name TEXT,qty INTEGER,ts INTEGER,PRIMARY KEY(character,location,id));
        CREATE TABLE IF NOT EXISTS trade(seq INTEGER PRIMARY KEY,market TEXT,id INTEGER,side TEXT,qty INTEGER,unit INTEGER,fee INTEGER,ts INTEGER);
        CREATE TABLE IF NOT EXISTS watch(market TEXT,id INTEGER,cap INTEGER,alert_ts INTEGER DEFAULT 0,PRIMARY KEY(market,id));
        CREATE TABLE IF NOT EXISTS event(url TEXT PRIMARY KEY,kind TEXT,title TEXT,day TEXT,notes TEXT);
        CREATE TABLE IF NOT EXISTS source(name TEXT PRIMARY KEY,state TEXT,checked INTEGER);
        CREATE TABLE IF NOT EXISTS recipe(id INTEGER PRIMARY KEY,name TEXT,output INTEGER,units INTEGER,materials TEXT);
        PRAGMA user_version=1;");
    }
    public string Get(string key,string fallback=""){var v=Scalar("SELECT v FROM meta WHERE k="+Q(key));return v==""?fallback:v;}
    public void Set(string key,string v){Exec("INSERT OR REPLACE INTO meta VALUES("+Q(key)+","+Q(v)+")");}
    public void State(string key,string msg){Exec("INSERT OR REPLACE INTO source VALUES("+Q(key)+","+Q(msg)+","+Now+")");}
}
}
