using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;

namespace RogueDungeonLab.Tests
{
    public sealed class DungeonRuntimeBuildBaselineTests
    {
        private const int WarmupCount = 3;
        private const int SampleCount = 15;

        // R6 전 비교 기준으로 Balanced RuntimeBuild의 p50/p95 시간과 현재 스레드 managed allocation을 기록합니다.
        [Test]
        public void RuntimeBuild_BalancedBaselineCapturesTimingAndManagedAllocation()
        {
            RogueDungeonSettings settings =
                ScriptableObject.CreateInstance<RogueDungeonSettings>();
            GameObject parent = new GameObject("R5.2 RuntimeBuild Baseline");
            try
            {
                settings.ApplyPreset(DungeonPreset.Balanced);
                const int seed = 73125;
                for (int i = 0; i < WarmupCount; i++)
                {
                    DungeonStageLoader.LoadProcedural(
                        parent.transform,
                        settings,
                        seed,
                        settings,
                        "R5.2-baseline-warmup");
                    DungeonStageLoader.ClearGenerated(parent.transform);
                }

                List<double> milliseconds = new List<double>(SampleCount);
                List<long> managedAllocations = new List<long>(SampleCount);
                List<long> monoUsedDeltas = new List<long>(SampleCount);
                bool threadAllocationCounterSupported =
                    SupportsThreadAllocationCounter();
                string expectedHash = string.Empty;
                for (int i = 0; i < SampleCount; i++)
                {
                    long allocationStart = GC.GetAllocatedBytesForCurrentThread();
                    long monoUsedStart = Profiler.GetMonoUsedSizeLong();
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    DungeonStageInstance instance = DungeonStageLoader.LoadProcedural(
                        parent.transform,
                        settings,
                        seed,
                        settings,
                        "R5.2-baseline-sample");
                    stopwatch.Stop();
                    long allocated =
                        GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                    long monoUsed =
                        Profiler.GetMonoUsedSizeLong() - monoUsedStart;

                    if (i == 0) expectedHash = instance.Blueprint.blueprintHash;
                    Assert.That(instance.Blueprint.blueprintHash, Is.EqualTo(expectedHash));
                    milliseconds.Add(stopwatch.Elapsed.TotalMilliseconds);
                    managedAllocations.Add(Math.Max(0L, allocated));
                    monoUsedDeltas.Add(Math.Max(0L, monoUsed));
                    DungeonStageLoader.ClearGenerated(parent.transform);
                }

                milliseconds.Sort();
                managedAllocations.Sort();
                monoUsedDeltas.Sort();
                double p50Milliseconds = Percentile(milliseconds, 0.50f);
                double p95Milliseconds = Percentile(milliseconds, 0.95f);
                long p50Allocation = Percentile(managedAllocations, 0.50f);
                long p95Allocation = Percentile(managedAllocations, 0.95f);
                long p50MonoUsed = Percentile(monoUsedDeltas, 0.50f);
                long p95MonoUsed = Percentile(monoUsedDeltas, 0.95f);

                Assert.That(p50Milliseconds, Is.GreaterThan(0d));
                Assert.That(p95Milliseconds, Is.GreaterThanOrEqualTo(p50Milliseconds));
                Assert.That(p50Allocation, Is.GreaterThanOrEqualTo(0L));
                Assert.That(p95Allocation, Is.GreaterThanOrEqualTo(p50Allocation));
                Assert.That(p50MonoUsed, Is.GreaterThanOrEqualTo(0L));
                Assert.That(p95MonoUsed, Is.GreaterThanOrEqualTo(p50MonoUsed));
                TestContext.Out.WriteLine(
                    "R5.2 RuntimeBuild Balanced seed 73125 / samples {0}: " +
                    "time p50={1:0.000} ms, p95={2:0.000} ms; " +
                    "thread allocation p50={3:N0} B, p95={4:N0} B, supported={5}; " +
                    "mono used delta p50={6:N0} B, p95={7:N0} B",
                    SampleCount,
                    p50Milliseconds,
                    p95Milliseconds,
                    p50Allocation,
                    p95Allocation,
                    threadAllocationCounterSupported,
                    p50MonoUsed,
                    p95MonoUsed);
            }
            finally
            {
                DungeonStageLoader.ClearGenerated(parent.transform);
                UnityEngine.Object.DestroyImmediate(parent);
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        // 현재 Unity Mono가 스레드별 managed allocation 누적 API를 실제 값으로 제공하는지 작은 할당으로 확인합니다.
        private static bool SupportsThreadAllocationCounter()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            byte[] probe = new byte[4096];
            probe[0] = 1;
            long after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(probe);
            return after > before;
        }

        // 정렬된 실수 표본에서 nearest-rank percentile을 반환합니다.
        private static double Percentile(IReadOnlyList<double> sorted, float percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt(sorted.Count * percentile) - 1,
                0,
                sorted.Count - 1);
            return sorted[index];
        }

        // 정렬된 정수 표본에서 nearest-rank percentile을 반환합니다.
        private static long Percentile(IReadOnlyList<long> sorted, float percentile)
        {
            int index = Mathf.Clamp(
                Mathf.CeilToInt(sorted.Count * percentile) - 1,
                0,
                sorted.Count - 1);
            return sorted[index];
        }
    }
}
