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
        // v1.8: dtype hep + sort_index + groupby iteration
        string[] v18Fns = new string[] { "series_astype", "series_to_narrow", "df_sort_index_cols", "df_sort_index_rows_identity", "groupby_group_keys", "groupby_group", "dt_is_narrow", "dt_parse_spec" };
        int ok18 = 0;
        foreach (var fn in v18Fns)
        {
            bool have = t.GetMethod(fn) != null;
            if (have) ok18++;
            else Console.WriteLine("MISSING18: " + fn);
        }
        Console.WriteLine("v18Fns=" + ok18 + "/" + v18Fns.Length);
        // v1.9: pandas 100% closure (missing-data/index/merge/csv/json extras)
        string[] v19Fns = new string[] { "df_empty", "df_notna", "df_fillna_rows", "df_dropna_rows_any", "df_ffill_cols", "series_fillna", "series_shift", "groupby_rolling", "groupby_resample", "df_set_index", "df_reindex", "merge_on_index", "csv_write_ex", "json_write_values", "json_write_split", "json_write_index" };
        int ok19 = 0;
        foreach (var fn in v19Fns)
        {
            bool have = t.GetMethod(fn) != null;
            if (have) ok19++;
            else Console.WriteLine("MISSING19: " + fn);
        }
        Console.WriteLine("v19Fns=" + ok19 + "/" + v19Fns.Length);
        // v2.1: SQL engine + Excel SpreadsheetML + Parquet ledger
        string[] v21Fns = new string[] { "sql_query", "sql_query2", "sql_count", "excel_write_string", "excel_write_file", "excel_read_string", "excel_read_file", "parquet_blocked_reason", "parquet_write_file", "parquet_read_file" };
        int ok21 = 0;
        foreach (var fn in v21Fns)
        {
            bool have = t.GetMethod(fn) != null;
            if (have) ok21++;
            else Console.WriteLine("MISSING21: " + fn);
        }
        Console.WriteLine("v21Fns=" + ok21 + "/" + v21Fns.Length);
        if (okNew != newFns.Length || ok18 != v18Fns.Length || ok19 != v19Fns.Length || ok21 != v21Fns.Length) { Console.WriteLine("SMOKE FAIL"); return 1; }
        Console.WriteLine("SMOKE OK");
        return 0;
    }
}
