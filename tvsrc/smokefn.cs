using System;
using System.Collections.Generic;
using System.Reflection;

class SmokeFn
{
    static int Main()
    {
        var asm = Assembly.LoadFrom("TokenVector.Data.dll");
        var t = asm.GetType("TKVApp");
        int fails = 0;
        // sql_query tren DataFrame that
        var ids = t.GetMethod("make_series_i64").Invoke(null, new object[] { "id", new List<long> { 1, 2, 3 } });
        var cats = t.GetMethod("make_series_str").Invoke(null, new object[] { "cat", new List<string> { "a", "b", "a" } });
        var vals = t.GetMethod("make_series_f64").Invoke(null, new object[] { "v", new List<double> { 10.0, 20.0, 30.0 } });
        var seriesType = asm.GetType("Series");
        var listType = typeof(List<>).MakeGenericType(seriesType);
        var cols = Activator.CreateInstance(listType);
        listType.GetMethod("Add").Invoke(cols, new object[] { ids });
        listType.GetMethod("Add").Invoke(cols, new object[] { cats });
        listType.GetMethod("Add").Invoke(cols, new object[] { vals });
        var df = t.GetMethod("make_df").Invoke(null, new object[] { cols });
        var r = t.GetMethod("sql_query").Invoke(null, new object[] { df, "SELECT cat, SUM(v) AS total FROM df GROUP BY cat ORDER BY total DESC" });
        int nrows = (int)r.GetType().GetMethod("row_count").Invoke(r, null);
        Console.WriteLine("sql rows=" + nrows);
        if (nrows != 2) fails++;
        // col_by_name la method cua DataFrame tra Series; lay get_f64(0)
        var mcol = r.GetType().GetMethod("col_by_name").Invoke(r, new object[] { "total" });
        double v0 = (double)mcol.GetType().GetMethod("get_f64").Invoke(mcol, new object[] { 0 });
        Console.WriteLine("sql total0=" + v0);
        if (v0 != 40.0) fails++;
        // excel round-trip
        string xml = (string)t.GetMethod("excel_write_string").Invoke(null, new object[] { df, "S1" });
        Console.WriteLine("xml bytes=" + xml.Length + " hasTable=" + xml.Contains("<Table>"));
        if (!xml.Contains("<Table>")) fails++;
        var back = t.GetMethod("excel_read_string").Invoke(null, new object[] { xml });
        int br = (int)back.GetType().GetMethod("row_count").Invoke(back, null);
        Console.WriteLine("excel back rows=" + br);
        if (br != 3) fails++;
        // parquet ledger
        string reason = (string)t.GetMethod("parquet_blocked_reason").Invoke(null, null);
        Console.WriteLine("parquet reason=" + reason.Substring(0, 40) + "...");
        if (!reason.Contains("blocked-by-compiler")) fails++;
        Console.WriteLine(fails == 0 ? "SMOKEFN OK" : "SMOKEFN FAIL");
        return fails;
    }
}
