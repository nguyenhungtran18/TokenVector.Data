using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;
using TokenVector.Numerics.Engine;

// KernelLab: do SIMDKernels cua TokenVector.Numerics (AVX2, TFM net8.0) thay cho
// numpy kernel - de tra loi: "TVM kernel C-cap" co danh bai pandas khong?
// Moi do: REP pass, best-of-3, cung quy uoc voi bench_tkv_parallel (5M x 20).

internal static class Program
{
    private const int N = 5_000_000;
    private const int Rep = 20;
    private static readonly double[] A = new double[N];
    private static readonly double[] B = new double[N];
    private static readonly double[] Dst = new double[N];
    private static double _sink;

    private static void Main()
    {
        Console.WriteLine($"Avx2={Avx2.IsSupported} Fma={Fma.IsSupported} cores={Environment.ProcessorCount}");
        for (int i = 0; i < N; i++)
        {
            A[i] = (i % 10000) * 0.001;
            B[i] = (i % 10000) * 0.001;
        }

        // warm-up + JIT
        SIMDKernels.AddContiguous<double>(A.AsSpan(), B.AsSpan(), Dst.AsSpan());
        SimdSum(A);
        SerArith();
        ParArith(8);

        Best("K1 add AVX2  1T  per-pass ms", () => SIMDKernels.AddContiguous<double>(A.AsSpan(), B.AsSpan(), Dst.AsSpan()), 1);
        Best("K1 add AVX2  8T  per-pass ms", () => ParArith(8), 8);
        Best("K2 sum  AVX2  1T  per-pass ms", () => _sink = SimdSum(A), 1);
        Best("K2 sum  AVX2  8T  per-pass ms", () => ParSum(8), 8);
        Best("K3 ser-loop  1T  per-pass ms", SerArith, 1);

        Console.WriteLine($"sink={_sink}");
    }

    private static double SimdSum(double[] src)
    {
        var v = Vector256<double>.Zero;
        var span = src.AsSpan();
        int count = Vector256<double>.Count;
        int i = 0;
        for (; i <= span.Length - count; i += count)
            v = Avx.Add(v, Vector256.LoadUnsafe(ref span[i]));
        double sum = 0;
        for (; i < span.Length; i++) sum += span[i];
        sum += v.GetElement(0) + v.GetElement(1) + v.GetElement(2) + v.GetElement(3);
        return sum;
    }

    private static void SerArith()
    {
        for (int i = 0; i < N; i++) Dst[i] = A[i] * 2.5 + B[i];
    }

    // Parallel: chunk-per-worker, nhung van ghi vao Dst (memory traffic giong engine that)
    private static void ParArith(int threads)
    {
        int chunk = N / threads;
        Parallel.For(0, threads, k =>
        {
            int lo = k * chunk;
            int hi = (k == threads - 1) ? N : lo + chunk;
            SIMDKernels.AddContiguous<double>(
                A.AsSpan(lo, hi - lo),
                B.AsSpan(lo, hi - lo),
                Dst.AsSpan(lo, hi - lo));
            for (int i = lo; i < hi; i++) Dst[i] = A[i] * 2.5 + B[i];
        });
    }

    private static void ParSum(int threads)
    {
        int chunk = N / threads;
        Parallel.For(0, threads, k =>
        {
            int lo = k * chunk;
            int hi = (k == threads - 1) ? N : lo + chunk;
            _sink += SimdSum(A.AsSpan(lo, hi - lo).ToArray());
        });
    }

    private static void Best(string name, Action action, int threads)
    {
        double best = double.MaxValue;
        for (int run = 0; run < 3; run++)
        {
            var sw = Stopwatch.StartNew();
            for (int rep = 0; rep < Rep; rep++) action();
            sw.Stop();
            double perPass = sw.Elapsed.TotalMilliseconds / Rep;
            if (perPass < best) best = perPass;
        }
        Console.WriteLine($"{name}  = {best:F3}   (threads~{threads}, {Rep} passes averaged)");
    }
}
