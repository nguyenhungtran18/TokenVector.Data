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
        // v1.7: pandas-parity + JSON native phai co mat trong DLL
        string[] newFns = new string[] { "json_read_string", "json_write_string", "jsonl_read_string", "json_read_object", "merge_ordered", "df_query", "df_eval", "series_mad", "series_factorize", "series_str_slice_replace", "series_str_fullmatch", "series_str_extractall", "series_str_cat", "groupby_nth", "groupby_head", "groupby_tail", "stack", "unstack", "dt_utc_offset", "dt_tz_convert" };
        int okNew = 0;
        foreach (var fn in newFns)
        {
            bool have = t.GetMethod(fn) != null;
            if (have) okNew++;
            else Console.WriteLine("MISSING: " + fn);
        }
        Console.WriteLine("newFns=" + okNew + "/" + newFns.Length);
        if (okNew != newFns.Length) { Console.WriteLine("SMOKE FAIL"); return 1; }
        Console.WriteLine("SMOKE OK");
        return 0;
    }
}
