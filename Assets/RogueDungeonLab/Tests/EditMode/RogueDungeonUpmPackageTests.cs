using System;
using System.IO;
using NUnit.Framework;
using RogueDungeonLab.Editor;
using UnityEngine;

namespace RogueDungeonLab.Tests
{
    public sealed class RogueDungeonUpmPackageTests
    {
        [Serializable]
        private sealed class PackageSample
        {
            public string displayName = string.Empty;
            public string path = string.Empty;
        }

        [Serializable]
        private sealed class PackageHeader
        {
            public string name = string.Empty;
            public string version = string.Empty;
            public string unity = string.Empty;
            public PackageSample[] samples = new PackageSample[0];
        }

        // 각 테스트가 canonical 동기화 결과를 기준으로 검사하도록 package tree를 준비합니다.
        [OneTimeSetUp]
        public void SyncPackagesBeforeTests()
        {
            RogueDungeonUpmPackageExporter.SyncTrackedPackages();
        }

        // 세 manifest의 ID·버전·Unity·직접 기능 의존성과 Core Sample 계약을 확인합니다.
        [Test]
        public void Manifests_DeclareIndependentPackageBoundaries()
        {
            string coreJson = ReadPackageManifest(
                RogueDungeonUpmPackageExporter.CorePackageName);
            string labJson = ReadPackageManifest(
                RogueDungeonUpmPackageExporter.LabPackageName);
            string bakingJson = ReadPackageManifest(
                RogueDungeonUpmPackageExporter.BakingPackageName);

            PackageHeader core = JsonUtility.FromJson<PackageHeader>(coreJson);
            PackageHeader lab = JsonUtility.FromJson<PackageHeader>(labJson);
            PackageHeader baking = JsonUtility.FromJson<PackageHeader>(bakingJson);

            Assert.That(
                DungeonDistributionExporter.RuntimeCoreUpmPackageVersion,
                Is.EqualTo(RogueDungeonUpmPackageExporter.PackageVersion));
            AssertHeader(
                core,
                RogueDungeonUpmPackageExporter.CorePackageName);
            AssertHeader(
                lab,
                RogueDungeonUpmPackageExporter.LabPackageName);
            AssertHeader(
                baking,
                RogueDungeonUpmPackageExporter.BakingPackageName);
            Assert.That(core.samples, Has.Length.EqualTo(1));
            Assert.That(core.samples[0].path, Is.EqualTo("Samples~/RuntimeBuild"));
            StringAssert.DoesNotContain("com.unity.inputsystem", coreJson);
            StringAssert.DoesNotContain(
                RogueDungeonUpmPackageExporter.LabPackageName,
                coreJson);
            StringAssert.Contains(
                "\"com.unity.inputsystem\": \"1.19.0\"",
                labJson);
            StringAssert.Contains(
                "\"" + RogueDungeonUpmPackageExporter.CorePackageName +
                "\": \"" + RogueDungeonUpmPackageExporter.PackageVersion +
                "\"",
                labJson);
            StringAssert.Contains(
                "\"" + RogueDungeonUpmPackageExporter.CorePackageName +
                "\": \"" + RogueDungeonUpmPackageExporter.PackageVersion +
                "\"",
                bakingJson);
            StringAssert.DoesNotContain("com.unity.inputsystem", bakingJson);
        }

        // Runtime·Sample·Lab·Baking 원본과 meta가 package 사본에서 byte 단위로 같은지 확인합니다.
        [Test]
        public void SyncedTrees_PreserveCanonicalSourceBytesAndGuids()
        {
            string coreRoot = GetPackageRoot(
                RogueDungeonUpmPackageExporter.CorePackageName);
            string labRoot = GetPackageRoot(
                RogueDungeonUpmPackageExporter.LabPackageName);
            string bakingRoot = GetPackageRoot(
                RogueDungeonUpmPackageExporter.BakingPackageName);

            AssertTreeMatches(
                "Assets/RogueDungeonLab/Runtime",
                Path.Combine(coreRoot, "Runtime"),
                null);
            AssertTreeMatches(
                "Assets/RogueDungeonLab/Examples/RuntimeBuild",
                Path.Combine(coreRoot, "Samples~", "RuntimeBuild"),
                null);
            AssertTreeMatches(
                "Assets/RogueDungeonLab/Samples/Lab",
                Path.Combine(labRoot, "Runtime"),
                null);
            AssertTreeMatches(
                "Assets/RogueDungeonLab/Editor/Baking",
                Path.Combine(bakingRoot, "Editor", "Baking"),
                null);
            AssertTreeMatches(
                "Assets/RogueDungeonLab/Editor/Packaging",
                Path.Combine(bakingRoot, "Editor", "Packaging"),
                IsSyncExporterFile);
            Assert.That(
                File.Exists(
                    Path.Combine(
                        bakingRoot,
                        "Editor",
                        "Packaging",
                        "RogueDungeonUpmPackageExporter.cs")),
                Is.False);
        }

        [Test]
        public void LabPackage_ContainsPortableArenaEditorWithoutBakingDependency()
        {
            string labRoot = GetPackageRoot(RogueDungeonUpmPackageExporter.LabPackageName);
            string source = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Assets/RogueDungeonLab/Editor/FpsArenaWindow.cs");
            AssertFilesEqual(source, Path.Combine(labRoot, "Editor", "FpsArenaWindow.cs"));
            AssertFilesEqual(source + ".meta", Path.Combine(labRoot, "Editor", "FpsArenaWindow.cs.meta"));
            string assembly = File.ReadAllText(Path.Combine(labRoot,"Editor","RogueDungeonLab.Arena.Editor.asmdef"));
            StringAssert.Contains("RogueDungeonLab.Runtime",assembly); StringAssert.Contains("RogueDungeonLab.Samples",assembly); StringAssert.Contains("Editor",assembly);
            StringAssert.DoesNotContain("RogueDungeonLab.Editor.Baking",assembly); StringAssert.DoesNotContain("RogueDungeonLab.Editor.Packaging",assembly);
        }

        // 같은 개발 원본을 연속 동기화해 package 파일 수와 tree hash가 같음을 확인합니다.
        [Test]
        public void SyncTrackedPackages_IsDeterministicAcrossRepeatedRuns()
        {
            DungeonUpmPackageSyncResult first =
                RogueDungeonUpmPackageExporter.SyncTrackedPackages();
            DungeonUpmPackageSyncResult second =
                RogueDungeonUpmPackageExporter.SyncTrackedPackages();

            Assert.That(first.version, Is.EqualTo(second.version));
            Assert.That(first.packages.Count, Is.EqualTo(3));
            Assert.That(second.packages.Count, Is.EqualTo(3));
            for (int i = 0; i < first.packages.Count; i++)
            {
                Assert.That(
                    first.packages[i].packageName,
                    Is.EqualTo(second.packages[i].packageName));
                Assert.That(
                    first.packages[i].fileCount,
                    Is.EqualTo(second.packages[i].fileCount));
                Assert.That(
                    first.packages[i].treeSha256,
                    Is.EqualTo(second.packages[i].treeSha256));
                Assert.That(first.packages[i].treeSha256, Has.Length.EqualTo(64));
            }
        }

        // 삭제 가능한 package root 조회가 세 고정 ID 밖의 경로를 거부하는지 확인합니다.
        [Test]
        public void GetAbsolutePackagePath_RejectsUnknownPackage()
        {
            Assert.Throws<ArgumentException>(
                delegate
                {
                    RogueDungeonUpmPackageExporter.GetAbsolutePackagePath(
                        "../unexpected");
                });
        }

        // package manifest를 BOM 없는 텍스트로 읽습니다.
        private static string ReadPackageManifest(string packageName)
        {
            return File.ReadAllText(
                Path.Combine(GetPackageRoot(packageName), "package.json"));
        }

        // 알려진 package ID를 테스트용 절대 root로 변환합니다.
        private static string GetPackageRoot(string packageName)
        {
            return RogueDungeonUpmPackageExporter.GetAbsolutePackagePath(
                packageName);
        }

        // 공통 package header가 R9.1 버전과 Unity 6000.5 요구사항을 지키는지 확인합니다.
        private static void AssertHeader(
            PackageHeader header,
            string expectedName)
        {
            Assert.That(header, Is.Not.Null);
            Assert.That(header.name, Is.EqualTo(expectedName));
            Assert.That(
                header.version,
                Is.EqualTo(RogueDungeonUpmPackageExporter.PackageVersion));
            Assert.That(header.unity, Is.EqualTo("6000.5"));
        }

        // 원본 root meta와 모든 비제외 파일이 대상 tree에서 동일한지 확인합니다.
        private static void AssertTreeMatches(
            string sourceProjectPath,
            string destinationAbsolutePath,
            Func<string, bool> shouldSkip)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);
            string sourceAbsolutePath = Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    sourceProjectPath.Replace('/', Path.DirectorySeparatorChar)));
            AssertFilesEqual(
                sourceAbsolutePath + ".meta",
                destinationAbsolutePath + ".meta");

            string[] sourceFiles = Directory.GetFiles(
                sourceAbsolutePath,
                "*",
                SearchOption.AllDirectories);
            Array.Sort(sourceFiles, StringComparer.Ordinal);
            string prefix = sourceAbsolutePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            int expectedFileCount = 0;
            for (int i = 0; i < sourceFiles.Length; i++)
            {
                string relativePath = sourceFiles[i]
                    .Substring(prefix.Length)
                    .Replace('\\', '/');
                if (shouldSkip != null && shouldSkip(relativePath)) continue;
                expectedFileCount++;
                string destinationPath = Path.Combine(
                    destinationAbsolutePath,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                AssertFilesEqual(sourceFiles[i], destinationPath);
            }

            Assert.That(
                Directory.GetFiles(
                    destinationAbsolutePath,
                    "*",
                    SearchOption.AllDirectories).Length,
                Is.EqualTo(expectedFileCount));
        }

        // 두 파일의 존재와 전체 bytes가 같은지 확인합니다.
        private static void AssertFilesEqual(
            string expectedPath,
            string actualPath)
        {
            Assert.That(File.Exists(expectedPath), Is.True, expectedPath);
            Assert.That(File.Exists(actualPath), Is.True, actualPath);
            CollectionAssert.AreEqual(
                File.ReadAllBytes(expectedPath),
                File.ReadAllBytes(actualPath),
                actualPath);
        }

        // 소비자 package에서 제외할 개발 저장소 전용 동기화기 파일을 식별합니다.
        private static bool IsSyncExporterFile(string relativePath)
        {
            return string.Equals(
                       relativePath,
                       "RogueDungeonUpmPackageExporter.cs",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       relativePath,
                       "RogueDungeonUpmPackageExporter.cs.meta",
                       StringComparison.Ordinal);
        }
    }
}
