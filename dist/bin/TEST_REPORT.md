# TokenVector.Data - Automated Test & Quality Assurance Report

[ 🇬🇧 English ](TEST_REPORT.md) | [ 🇻🇳 Tiếng Việt ](TEST_REPORT_VI.md)

**Execution Date:** 2026-09-13  
**Framework:** .NET 8.0 LTS (`net8.0`)  
**Runner:** xUnit v2.5.6 (64-bit)  
**Configuration:** Release (`-c Release`)  
**Test Results:** **51 / 51 Passed (100% Green, 0 Failed, 0 Skipped)**  
**Total Execution Time:** 47 ms

---

## 1. Test Summary by Category

| Test Suite File | Category | Tests | Status |
| :--- | :--- | :--- | :--- |
| `ColumnAndSeriesTests.cs` | Bitboards, Columns, String UTF-8, ChunkedArray, Series Math & Casting | 12 | ✅ 12/12 Passed |
| `DataFrameBasicTests.cs` | 2D DataFrame Creation, Slicing, Mask Indexers, Sorting, Describe, Drop/Fill Nulls | 7 | ✅ 7/7 Passed |
| `FilterAndComputeTests.cs` | SIMD VectorMath, FilterEngine Parallelism, Window Functions (Rolling/Shift/Diff/Rank) | 8 | ✅ 8/8 Passed |
| `GroupByTests.cs` | Multi-Column GroupBy, Aggregations (Sum, Mean, Count, Std, First, Last), Null handling | 4 | ✅ 4/4 Passed |
| `JoinAndReshapeTests.cs` | Radix Hash Joins (Inner, Left, Right, FullOuter, Cross), AsOfJoin, Pivot, Melt, Concat | 9 | ✅ 9/9 Passed |
| `CsvAndJsonIOTests.cs` | Delimited CSV parsing, RFC 4180 quotes, NDJSON, JSON arrays, Type Inference | 7 | ✅ 7/7 Passed |
| `ArrowAndMemMapTests.cs` | Apache Arrow IPC Feather streaming, OutOfCore Memory-Mapped Files | 2 | ✅ 2/2 Passed |
| `NumericsInteropTests.cs` | Zero-Copy conversion between DataFrame/Series and NDArray/Tensor | 4 | ✅ 4/4 Passed |
| **Total** | **Full Library Test Suite** | **51** | **✅ 51/51 Passed (100%)** |

---

## 2. Detailed Test Cases

```text
[PASS] ColumnAndSeriesTests.TestBitmapMask_BasicOperations
[PASS] ColumnAndSeriesTests.TestBitmapMask_BitwiseLogic
[PASS] ColumnAndSeriesTests.TestBitmapMask_SliceAndFromIndices
[PASS] ColumnAndSeriesTests.TestColumn_TypedAllocationAndNulls
[PASS] ColumnAndSeriesTests.TestColumn_TakeAndFilter
[PASS] ColumnAndSeriesTests.TestColumn_CastOperations
[PASS] ColumnAndSeriesTests.TestStringColumn_ArrowLayoutAndNulls
[PASS] ColumnAndSeriesTests.TestStringColumn_CastToNumeric
[PASS] ColumnAndSeriesTests.TestChunkedArray_FlattenAndIndexing
[PASS] ColumnAndSeriesTests.TestSeries_ArithmeticAndAggregations
[PASS] ColumnAndSeriesTests.TestSeries_MathUnaryOperations
[PASS] ColumnAndSeriesTests.TestSeries_FillNullAndDropNull
[PASS] DataFrameBasicTests.TestDataFrame_CreationAndShapes
[PASS] DataFrameBasicTests.TestDataFrame_IndexingAndSlicing
[PASS] DataFrameBasicTests.TestDataFrame_MaskIndexerAndSelection
[PASS] DataFrameBasicTests.TestDataFrame_MutationAndRenaming
[PASS] DataFrameBasicTests.TestDataFrame_Sorting
[PASS] DataFrameBasicTests.TestDataFrame_DropNullAndFillNull
[PASS] DataFrameBasicTests.TestDataFrame_Describe
[PASS] FilterAndComputeTests.TestVectorMath_ArithmeticOperations
[PASS] FilterAndComputeTests.TestVectorMath_UnaryAndTrig
[PASS] FilterAndComputeTests.TestVectorMath_Comparisons
[PASS] FilterAndComputeTests.TestFilterEngine_ParallelPredicate
[PASS] FilterAndComputeTests.TestFilterEngine_CombineMasks
[PASS] FilterAndComputeTests.TestAggregations_VarianceStdAndQuantile
[PASS] FilterAndComputeTests.TestWindowFunctions_RollingStatistics
[PASS] FilterAndComputeTests.TestWindowFunctions_ShiftDiffCumSumRank
[PASS] GroupByTests.TestGroupBy_SingleKeyAggregations
[PASS] GroupByTests.TestGroupBy_MultiKeyAggregations
[PASS] GroupByTests.TestGroupBy_WithNullValues
[PASS] GroupByTests.TestGroupBy_FirstLastStdAggregations
[PASS] JoinAndReshapeTests.TestJoin_InnerJoin
[PASS] JoinAndReshapeTests.TestJoin_LeftJoin
[PASS] JoinAndReshapeTests.TestJoin_RightAndFullOuter
[PASS] JoinAndReshapeTests.TestJoin_CrossJoin
[PASS] JoinAndReshapeTests.TestJoin_AsOfJoin_TimeMatching
[PASS] JoinAndReshapeTests.TestJoin_AsOfJoin_ForwardAndNearest
[PASS] JoinAndReshapeTests.TestReshape_PivotAndMelt
[PASS] JoinAndReshapeTests.TestReshape_ConcatVerticalAndHorizontal
[PASS] CsvAndJsonIOTests.TestCsvReader_BasicAndQuotedFields
[PASS] CsvAndJsonIOTests.TestCsvReader_SemicolonAndTabDelimiters
[PASS] CsvAndJsonIOTests.TestCsvReader_NoHeader
[PASS] CsvAndJsonIOTests.TestCsvWriter_RoundTrip
[PASS] CsvAndJsonIOTests.TestJsonReader_NDJson
[PASS] CsvAndJsonIOTests.TestJsonReader_JsonArray
[PASS] ArrowAndMemMapTests.TestArrowIpc_StreamRoundTrip
[PASS] ArrowAndMemMapTests.TestOutOfCoreDataFrame_PersistAndBatchRead
[PASS] NumericsInteropTests.TestSeriesToNDArray_ZeroCopy
[PASS] NumericsInteropTests.TestDataFrameToNDArray_2DMatrix
[PASS] NumericsInteropTests.TestSeriesAndDataFrameToTensor_AutogradDifferentiable
[PASS] NumericsInteropTests.TestFromNDArrayAndFromTensor_ToDataFrame
```
