using System;
using System.Reflection;

class Smoke
{
    static int Main()
    {
        var asm = Assembly.LoadFrom("TokenVector.Data.dll");
        Console.WriteLine("asm=" + asm.GetName().Name);
        var t = asm.GetType("TKVApp");
        Console.WriteLine("type TKVApp=" + (t != null));
        var m = t.GetMethod("csv_read_string");
        Console.WriteLine("csv_read_string=" + (m != null));
        var mex = t.GetMethod("csv_read_ex");
        Console.WriteLine("csv_read_ex=" + (mex != null) + " params=" + mex.GetParameters().Length);
        var mdp = t.GetMethod("series_dt_parse");
        Console.WriteLine("series_dt_parse=" + (mdp != null));
        var menc = t.GetMethod("csv_read_chunks_file_enc");
        Console.WriteLine("csv_read_chunks_file_enc=" + (menc != null) + " params=" + (menc != null ? menc.GetParameters().Length : 0));
        var df = m.Invoke(null, new object[] { "id,name\n1,an\n", ",", 1, "" });
        Console.WriteLine("df=" + (df != null));
        Console.WriteLine("SMOKE OK");
        return 0;
    }
}
