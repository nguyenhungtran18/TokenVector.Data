using System;
using TokenVector.Data.Common;
using TokenVector.Data.Core;
using TokenVector.Data.Relational;
using Xunit;

namespace TokenVector.Data.Tests;

public class GroupByTests
{
    [Fact]
    public void TestGroupBy_SingleKeyAggregations()
    {
        var df = new DataFrame(
            Series.FromStrings("dept", new[] { "IT", "HR", "IT", "Sales", "HR", "IT" }),
            Series.FromValues("salary", new[] { 60000.0, 45000.0, 80000.0, 50000.0, 55000.0, 70000.0 }),
            Series.FromValues("age", new[] { 28, 35, 42, 29, 38, 31 })
        );

        var grouped = df.GroupBy("dept").Agg(
            Agg.Count("salary", "count"),
            Agg.Sum("salary", "total_salary"),
            Agg.Mean("salary", "avg_salary"),
            Agg.Max("salary", "max_salary"),
            Agg.Min("salary", "min_salary"),
            Agg.Mean("age", "avg_age")
        );

        Assert.Equal(3, grouped.RowCount); // IT, HR, Sales
        Assert.Equal(7, grouped.ColumnCount); // dept + 6 aggs

        // Verify IT group: salaries 60k, 80k, 70k -> sum = 210k, mean = 70k, count = 3
        var itRow = Enumerable.Range(0, grouped.RowCount).First(r => (string)grouped["dept"][r]! == "IT");
        Assert.Equal(3.0, Convert.ToDouble(grouped["count"][itRow]));
        Assert.Equal(210000.0, Convert.ToDouble(grouped["total_salary"][itRow]));
        Assert.Equal(70000.0, Convert.ToDouble(grouped["avg_salary"][itRow]));
        Assert.Equal(80000.0, Convert.ToDouble(grouped["max_salary"][itRow]));
        Assert.Equal(60000.0, Convert.ToDouble(grouped["min_salary"][itRow]));
    }

    [Fact]
    public void TestGroupBy_MultiKeyAggregations()
    {
        var df = new DataFrame(
            Series.FromStrings("dept", new[] { "IT", "IT", "HR", "HR", "IT" }),
            Series.FromStrings("level", new[] { "Junior", "Senior", "Junior", "Senior", "Junior" }),
            Series.FromValues("bonus", new[] { 1000.0, 5000.0, 800.0, 3000.0, 1500.0 })
        );

        var grouped = df.GroupBy("dept", "level").Agg(
            Agg.Sum("bonus", "total_bonus"),
            Agg.Count("bonus", "employee_count")
        );

        Assert.Equal(4, grouped.RowCount); // (IT, Junior), (IT, Senior), (HR, Junior), (HR, Senior)
        Assert.Equal(4, grouped.ColumnCount); // dept, level, total_bonus, employee_count
    }

    [Fact]
    public void TestGroupBy_WithNullValues()
    {
        var mask = new BitmapMask(4, initialValue: true);
        mask.Set(1, false); // null score at index 1

        var df = new DataFrame(
            Series.FromStrings("group", new[] { "A", "A", "B", "B" }),
            Series.FromValues("score", new[] { 10.0, 0.0, 20.0, 30.0 }, mask)
        );

        var grouped = df.GroupBy("group").Agg(
            Agg.Mean("score", "mean_score"),
            Agg.Count("score", "valid_count")
        );

        Assert.Equal(2, grouped.RowCount);
        var aRow = Enumerable.Range(0, grouped.RowCount).First(r => (string)grouped["group"][r]! == "A");
        Assert.Equal(10.0, Convert.ToDouble(grouped["mean_score"][aRow])); // 1 valid entry for A
        Assert.Equal(1.0, Convert.ToDouble(grouped["valid_count"][aRow]));
    }

    [Fact]
    public void TestGroupBy_FirstLastStdAggregations()
    {
        var df = new DataFrame(
            Series.FromStrings("cat", new[] { "X", "X", "X", "Y", "Y" }),
            Series.FromValues("val", new[] { 10.0, 20.0, 30.0, 100.0, 200.0 })
        );

        var grouped = df.GroupBy("cat").Agg(
            Agg.First("val", "first_val"),
            Agg.Last("val", "last_val"),
            Agg.Std("val", "std_val")
        );

        Assert.Equal(2, grouped.RowCount);
        var xRow = Enumerable.Range(0, grouped.RowCount).First(r => (string)grouped["cat"][r]! == "X");
        Assert.Equal(10.0, Convert.ToDouble(grouped["first_val"][xRow]));
        Assert.Equal(30.0, Convert.ToDouble(grouped["last_val"][xRow]));
        Assert.Equal(10.0, Convert.ToDouble(grouped["std_val"][xRow])); // std([10, 20, 30]) = 10.0
    }
}
