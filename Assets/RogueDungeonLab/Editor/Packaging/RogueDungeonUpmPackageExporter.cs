using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace RogueDungeonLab.Editor
{
    [Serializable]
    public sealed class DungeonUpmPackageSyncEntry
    {
        public string packageName = string.Empty;
        public string packagePath = string.Empty;
        public int fileCount;
        public string treeSha256 = string.Empty;
    }

    [Serializable]
    public sealed class DungeonUpmPackageSyncResult
    {
        public string version = string.Empty;
        public List<DungeonUpmPackageSyncEntry> packages =
            new List<DungeonUpmPackageSyncEntry>();

        // 세 package에 기록된 전체 파일 수를 합산합니다.
        public int CountFiles()
        {
            int total = 0;
            for (int i = 0; i < packages.Count; i++)
                total += packages[i] != null ? packages[i].fileCount : 0;
            return total;
        }
    }

    public static class RogueDungeonUpmPackageExporter
    {
        public const string PackageVersion = "0.13.0";
        public const string PackagesRoot = "UpmPackages";
        public const string CorePackageName =
            "com.dntlr2000.rogue-dungeon-lab.core";
        public const string LabPackageName =
            "com.dntlr2000.rogue-dungeon-lab.lab";
        public const string BakingPackageName =
            "com.dntlr2000.rogue-dungeon-lab.baking";

        private const string RuntimeSource = "Assets/RogueDungeonLab/Runtime";
        private const string RuntimeExamplesSource =
            "Assets/RogueDungeonLab/Examples/RuntimeBuild";
        private const string LabSource = "Assets/RogueDungeonLab/Samples/Lab";
        private const string EditorSource = "Assets/RogueDungeonLab/Editor";
        private const string BakingSource =
            "Assets/RogueDungeonLab/Editor/Baking";
        private const string PackagingSource =
            "Assets/RogueDungeonLab/Editor/Packaging";
        private const string SyncExporterFile =
            "RogueDungeonUpmPackageExporter.cs";

        // 개발 원본에서 추적 가능한 세 UPM package root를 다시 생성합니다.
        public static DungeonUpmPackageSyncResult SyncTrackedPackages()
        {
            string packagesRoot = ResolvePackagesRoot();
            Directory.CreateDirectory(packagesRoot);

            DungeonUpmPackageSyncResult result =
                new DungeonUpmPackageSyncResult
                {
                    version = PackageVersion
                };
            result.packages.Add(SyncCorePackage(packagesRoot));
            result.packages.Add(SyncLabPackage(packagesRoot));
            result.packages.Add(SyncBakingPackage(packagesRoot));
            return result;
        }

        // batchmode executeMethod에서 동기화 결과를 로그로 남깁니다.
        public static void SyncFromBatch()
        {
            DungeonUpmPackageSyncResult result = SyncTrackedPackages();
            Debug.Log(
                "Rogue Dungeon Lab UPM packages synchronized: " +
                result.packages.Count.ToString(CultureInfo.InvariantCulture) +
                " packages, " +
                result.CountFiles().ToString(CultureInfo.InvariantCulture) +
                " files, version " + result.version + ".");
        }

        // Tools 메뉴에서 package를 동기화하고 결과 폴더를 엽니다.
        [MenuItem("Tools/Rogue Dungeon Lab/R9.1 UPM 패키지 동기화")]
        private static void SyncFromMenu()
        {
            try
            {
                DungeonUpmPackageSyncResult result = SyncTrackedPackages();
                string root = ResolvePackagesRoot();
                EditorUtility.RevealInFinder(root);
                EditorUtility.DisplayDialog(
                    "R9.1 UPM 동기화 완료",
                    result.packages.Count + "개 패키지, " +
                    result.CountFiles() + "개 파일\n버전 " + result.version +
                    "\n\n" + root,
                    "확인");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "R9.1 UPM 동기화 실패",
                    exception.Message,
                    "확인");
            }
        }

        // 허용된 package ID의 저장소 기준 절대 root를 반환합니다.
        public static string GetAbsolutePackagePath(string packageName)
        {
            if (!IsKnownPackage(packageName))
            {
                throw new ArgumentException(
                    "Unknown Rogue Dungeon Lab UPM package: " + packageName,
                    nameof(packageName));
            }
            return Path.Combine(ResolvePackagesRoot(), packageName);
        }

        // Runtime과 RuntimeBuild Sample을 Core package로 복제합니다.
        private static DungeonUpmPackageSyncEntry SyncCorePackage(
            string packagesRoot)
        {
            string packageRoot = PreparePackageRoot(
                packagesRoot,
                CorePackageName);
            CopySourceTree(
                RuntimeSource,
                Path.Combine(packageRoot, "Runtime"),
                null);
            CopySourceTree(
                RuntimeExamplesSource,
                Path.Combine(packageRoot, "Samples~", "RuntimeBuild"),
                null);
            WritePackageManifest(
                packageRoot,
                BuildCoreManifest(),
                "80203fa15b7e6d343b4ab85f848f02d2");
            WritePackageDocuments(
                packageRoot,
                BuildCoreReadme(),
                "0e620528f0354a02aa528fc38b25f4d1",
                "06fac9533559470b96ca815d3e73c621",
                "7d21f69fc180494594ffbd053488df8c");
            return CreateSyncEntry(CorePackageName, packageRoot);
        }

        // 선택 HUD·카메라·임시 플레이어를 Lab package로 복제합니다.
        private static DungeonUpmPackageSyncEntry SyncLabPackage(
            string packagesRoot)
        {
            string packageRoot = PreparePackageRoot(
                packagesRoot,
                LabPackageName);
            CopySourceTree(
                LabSource,
                Path.Combine(packageRoot, "Runtime"),
                null);
            WritePackageManifest(
                packageRoot,
                BuildLabManifest(),
                "7b4966f6156f70b41862607ee92e9f0a");
            WritePackageDocuments(
                packageRoot,
                BuildLabReadme(),
                "8fd8bc0565cf475aaef4df10f31902a8",
                "8bdd69b2cd634b48950b9203d6f0f53e",
                "5880ff32d1d84eb0bc7bafc951b45675");
            return CreateSyncEntry(LabPackageName, packageRoot);
        }

        // Baker와 기존 stage 배포 도구를 Editor-only Baking package로 복제합니다.
        private static DungeonUpmPackageSyncEntry SyncBakingPackage(
            string packagesRoot)
        {
            string packageRoot = PreparePackageRoot(
                packagesRoot,
                BakingPackageName);
            string editorRoot = Path.Combine(packageRoot, "Editor");
            Directory.CreateDirectory(editorRoot);
            CopyDirectoryMeta(EditorSource, editorRoot);
            CopySourceTree(
                BakingSource,
                Path.Combine(editorRoot, "Baking"),
                null);
            CopySourceTree(
                PackagingSource,
                Path.Combine(editorRoot, "Packaging"),
                ShouldSkipPackagingFile);
            WritePackageManifest(
                packageRoot,
                BuildBakingManifest(),
                "c0cee1c6345a45942ad5c30d52f0f656");
            WritePackageDocuments(
                packageRoot,
                BuildBakingReadme(),
                "693d4adf836e49e7a4f60c66673c63eb",
                "72c061bfaaba4e40bfa846c0aabf2fe9",
                "79db532d98524de89c5cf3db580174d4");
            return CreateSyncEntry(BakingPackageName, packageRoot);
        }

        // package.json과 공통 문서의 package tree 통계를 계산합니다.
        private static DungeonUpmPackageSyncEntry CreateSyncEntry(
            string packageName,
            string packageRoot)
        {
            return new DungeonUpmPackageSyncEntry
            {
                packageName = packageName,
                packagePath = NormalizePath(packageRoot),
                fileCount = Directory.GetFiles(
                    packageRoot,
                    "*",
                    SearchOption.AllDirectories).Length,
                treeSha256 = ComputeTreeSha256(packageRoot)
            };
        }

        // 정확히 허용된 package 직계 폴더만 삭제 후 다시 만듭니다.
        private static string PreparePackageRoot(
            string packagesRoot,
            string packageName)
        {
            if (!IsKnownPackage(packageName))
                throw new InvalidOperationException("Unknown package target.");

            string canonicalRoot = Path.GetFullPath(packagesRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string packageRoot = Path.GetFullPath(
                Path.Combine(canonicalRoot, packageName));
            string parent = Path.GetDirectoryName(packageRoot);
            if (!string.Equals(
                    parent,
                    canonicalRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "UPM package target escaped the configured root: " + packageRoot);
            }

            if (Directory.Exists(packageRoot))
                Directory.Delete(packageRoot, true);
            Directory.CreateDirectory(packageRoot);
            return packageRoot;
        }

        // 원본 폴더의 파일과 폴더 meta를 상대 경로 그대로 복제합니다.
        private static void CopySourceTree(
            string sourceProjectPath,
            string destinationAbsolutePath,
            Func<string, bool> shouldSkip)
        {
            string sourceAbsolutePath = ResolveProjectPath(sourceProjectPath);
            if (!Directory.Exists(sourceAbsolutePath))
            {
                throw new DirectoryNotFoundException(
                    "UPM source folder is missing: " + sourceProjectPath);
            }

            Directory.CreateDirectory(destinationAbsolutePath);
            CopyDirectoryMeta(sourceAbsolutePath, destinationAbsolutePath);
            string[] files = Directory.GetFiles(
                sourceAbsolutePath,
                "*",
                SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            string sourcePrefix = sourceAbsolutePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            for (int i = 0; i < files.Length; i++)
            {
                string relativePath = NormalizePath(
                    files[i].Substring(sourcePrefix.Length));
                if (shouldSkip != null && shouldSkip(relativePath)) continue;
                string destinationPath = Path.Combine(
                    destinationAbsolutePath,
                    relativePath.Replace('/', Path.DirectorySeparatorChar));
                string destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);
                File.Copy(files[i], destinationPath, true);
            }
        }

        // 원본 root 폴더의 GUID를 대상 root 폴더 meta로 보존합니다.
        private static void CopyDirectoryMeta(
            string sourceDirectory,
            string destinationDirectory)
        {
            string sourceAbsoluteDirectory = Path.IsPathRooted(sourceDirectory)
                ? Path.GetFullPath(sourceDirectory)
                : ResolveProjectPath(sourceDirectory);
            string sourceMeta = sourceAbsoluteDirectory + ".meta";
            if (!File.Exists(sourceMeta))
            {
                throw new FileNotFoundException(
                    "UPM source folder meta is missing.",
                    sourceMeta);
            }
            string destinationMeta = destinationDirectory + ".meta";
            string destinationParent = Path.GetDirectoryName(destinationMeta);
            if (!string.IsNullOrEmpty(destinationParent))
                Directory.CreateDirectory(destinationParent);
            File.Copy(sourceMeta, destinationMeta, true);
        }

        // 소비자 Baking package에 개발 저장소 전용 동기화기를 넣지 않습니다.
        private static bool ShouldSkipPackagingFile(string relativePath)
        {
            return string.Equals(
                       relativePath,
                       SyncExporterFile,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       relativePath,
                       SyncExporterFile + ".meta",
                       StringComparison.Ordinal);
        }

        // package root에 UTF-8 package.json과 고정 PackageManifestImporter meta를 기록합니다.
        private static void WritePackageManifest(
            string packageRoot,
            string manifest,
            string guid)
        {
            string path = Path.Combine(packageRoot, "package.json");
            WriteUtf8File(path, manifest);
            WriteUtf8File(path + ".meta", BuildPackageManifestMeta(guid));
        }

        // package별 README와 공통 변경 이력·라이선스 안내를 고정 GUID로 기록합니다.
        private static void WritePackageDocuments(
            string packageRoot,
            string readme,
            string readmeGuid,
            string changelogGuid,
            string licenseGuid)
        {
            WriteDocument(
                packageRoot,
                "README.md",
                readme,
                readmeGuid);
            WriteDocument(
                packageRoot,
                "CHANGELOG.md",
                BuildPackageChangelog(),
                changelogGuid);
            WriteDocument(
                packageRoot,
                "LICENSE.md",
                BuildLicenseNotice(),
                licenseGuid);
        }

        // 문서 본문과 Unity TextScriptImporter meta를 함께 기록합니다.
        private static void WriteDocument(
            string packageRoot,
            string fileName,
            string contents,
            string guid)
        {
            string path = Path.Combine(packageRoot, fileName);
            WriteUtf8File(path, contents);
            WriteUtf8File(path + ".meta", BuildTextAssetMeta(guid));
        }

        // BOM 없는 UTF-8과 LF 끝줄로 생성 파일을 정규화합니다.
        private static void WriteUtf8File(string path, string contents)
        {
            string normalized = (contents ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n');
            if (!normalized.EndsWith("\n", StringComparison.Ordinal))
                normalized += "\n";
            File.WriteAllText(path, normalized, new UTF8Encoding(false));
        }

        // Runtime Core package manifest를 canonical 속성 순서로 만듭니다.
        private static string BuildCoreManifest()
        {
            return "{\n" +
                   "  \"name\": \"" + CorePackageName + "\",\n" +
                   "  \"version\": \"" + PackageVersion + "\",\n" +
                   "  \"displayName\": \"Rogue Dungeon Lab - Runtime Core\",\n" +
                   "  \"description\": \"Deterministic runtime dungeon generation, saved blueprints, baked-stage loading, and run-state persistence.\",\n" +
                   "  \"unity\": \"6000.5\",\n" +
                   "  \"license\": \"See LICENSE.md\",\n" +
                   "  \"author\": {\n" +
                   "    \"name\": \"dntlr2000\"\n" +
                   "  },\n" +
                   "  \"dependencies\": {\n" +
                   "    \"com.unity.modules.jsonserialize\": \"1.0.0\",\n" +
                   "    \"com.unity.modules.physics\": \"1.0.0\"\n" +
                   "  },\n" +
                   "  \"samples\": [\n" +
                   "    {\n" +
                   "      \"displayName\": \"RuntimeBuild Examples\",\n" +
                   "      \"description\": \"HUD-free Procedural and SavedBlueprint RuntimeBuild examples.\",\n" +
                   "      \"path\": \"Samples~/RuntimeBuild\"\n" +
                   "    }\n" +
                   "  ]\n" +
                   "}";
        }

        // 선택 Lab package manifest에 Core·Input System·직접 engine module 의존성을 선언합니다.
        private static string BuildLabManifest()
        {
            return "{\n" +
                   "  \"name\": \"" + LabPackageName + "\",\n" +
                   "  \"version\": \"" + PackageVersion + "\",\n" +
                   "  \"displayName\": \"Rogue Dungeon Lab - Lab Sample\",\n" +
                   "  \"description\": \"Optional in-game lab HUD, free camera, click interaction, and prototype player controls.\",\n" +
                   "  \"unity\": \"6000.5\",\n" +
                   "  \"license\": \"See LICENSE.md\",\n" +
                   "  \"author\": {\n" +
                   "    \"name\": \"dntlr2000\"\n" +
                   "  },\n" +
                   "  \"dependencies\": {\n" +
                   "    \"" + CorePackageName + "\": \"" + PackageVersion + "\",\n" +
                   "    \"com.unity.inputsystem\": \"1.19.0\",\n" +
                   "    \"com.unity.modules.imgui\": \"1.0.0\",\n" +
                   "    \"com.unity.modules.physics\": \"1.0.0\"\n" +
                   "  }\n" +
                   "}";
        }

        // Editor-only Baking package manifest에 Core 계약만 외부 기능 의존성으로 선언합니다.
        private static string BuildBakingManifest()
        {
            return "{\n" +
                   "  \"name\": \"" + BakingPackageName + "\",\n" +
                   "  \"version\": \"" + PackageVersion + "\",\n" +
                   "  \"displayName\": \"Rogue Dungeon Lab - Baking Tools\",\n" +
                   "  \"description\": \"Editor-only persistent mesh and prefab baking plus modular baked-stage distribution tools.\",\n" +
                   "  \"unity\": \"6000.5\",\n" +
                   "  \"license\": \"See LICENSE.md\",\n" +
                   "  \"author\": {\n" +
                   "    \"name\": \"dntlr2000\"\n" +
                   "  },\n" +
                   "  \"dependencies\": {\n" +
                   "    \"" + CorePackageName + "\": \"" + PackageVersion + "\",\n" +
                   "    \"com.unity.modules.jsonserialize\": \"1.0.0\",\n" +
                   "    \"com.unity.modules.physics\": \"1.0.0\"\n" +
                   "  }\n" +
                   "}";
        }

        // Core 설치 범위와 RuntimeBuild Sample 가져오기 방법을 설명합니다.
        private static string BuildCoreReadme()
        {
            return "# Rogue Dungeon Lab - Runtime Core\n\n" +
                   "결정적 절차 생성, 저장 Blueprint RuntimeBuild, BakedPrefab 로드와 RunState를 제공하는 제품 런타임입니다. 0.13.0부터 절차 생성과 검증·Build 사이에 결정적 `IDungeonBlueprintPostprocessor`를 적용할 수 있습니다. Lab HUD, 임시 플레이어, Input System 및 Editor Baker는 포함하지 않습니다.\n\n" +
                   "Package Manager의 Samples에서 `RuntimeBuild Examples`를 Import하면 HUD 없는 Procedural·SavedBlueprint 예제 장면을 확인할 수 있습니다. 자세한 설치 조합은 원본 저장소의 `docs/R9_1_UPM_GUIDE_KO.md`를 참고하세요.";
        }

        // Lab package가 선택 기능이며 제품 UI와 분리됨을 설명합니다.
        private static string BuildLabReadme()
        {
            return "# Rogue Dungeon Lab - Lab Sample\n\n" +
                   "실험용 인게임 HUD, 자유 카메라, 클릭 상호작용과 임시 플레이어를 제공하는 선택 package입니다. Runtime Core와 Input System을 요구합니다.\n\n" +
                   "제품 장면에서 Lab 컴포넌트를 배치하지 않으면 빌더 HUD는 표시되지 않습니다. Core-only 제품에는 이 package를 설치하지 않아도 됩니다.";
        }

        // Baking package의 제작 전용 범위와 modular stage 내보내기 경계를 설명합니다.
        private static string BuildBakingReadme()
        {
            return "# Rogue Dungeon Lab - Baking Tools\n\n" +
                   "영속 Mesh·Prefab Bake와 Baked Stage `.unitypackage` 배포 계획을 제공하는 Editor-only package입니다. Runtime Core를 요구하며 Lab Sample은 요구하지 않습니다.\n\n" +
                   "UPM 소비 프로젝트에서는 Core가 별도 package이므로 `Baked Stage 묶음` modular 내보내기를 사용하세요. Core 포함 standalone `.unitypackage`는 개발 원본의 기존 R9 배포 메뉴에서 생성합니다.";
        }

        // 세 package가 공유하는 최신 변경 이력과 이전 R9.1 기준선을 만듭니다.
        private static string BuildPackageChangelog()
        {
            return "# Changelog\n\n" +
                   "## 0.13.0\n\n" +
                   "- 절차 생성 직후 검증·Build 전에 실행되는 결정적 `IDungeonBlueprintPostprocessor` 계약 추가\n" +
                   "- RunSeed, request ID와 런타임 후처리 override를 전달하는 StageDefinition facade 추가\n" +
                   "- Core·Lab·Baking package 버전 및 결정적 UPM 동기화 갱신\n\n" +
                   "## 0.12.0\n\n" +
                   "- Runtime Core, 선택 Lab Sample, Editor-only Baking Tools의 UPM/Git URL 설치 구조 추가\n" +
                   "- 개발 원본 GUID와 assembly 경계를 보존하는 결정적 동기화 추가\n" +
                   "- 기존 R9 `.unitypackage` 배포 경로 유지";
        }

        // 저장소에 별도 라이선스가 없음을 숨기지 않고 사용 조건 확인을 안내합니다.
        private static string BuildLicenseNotice()
        {
            return "# License\n\n" +
                   "이 package 배포 구조는 별도의 사용·재배포 라이선스를 부여하지 않습니다. 사용 조건은 package를 제공한 저장소 소유자 또는 저작권자와 합의한 조건을 따릅니다.";
        }

        // 새 문서 자산에 사용할 결정적 TextScriptImporter meta를 만듭니다.
        private static string BuildTextAssetMeta(string guid)
        {
            return "fileFormatVersion: 2\n" +
                   "guid: " + guid + "\n" +
                   "TextScriptImporter:\n" +
                   "  externalObjects: {}\n" +
                   "  userData: \n" +
                   "  assetBundleName: \n" +
                   "  assetBundleVariant: ";
        }

        // package.json이 소비 project에서 새 GUID를 생성하지 않도록 importer meta를 만듭니다.
        private static string BuildPackageManifestMeta(string guid)
        {
            return "fileFormatVersion: 2\n" +
                   "guid: " + guid + "\n" +
                   "PackageManifestImporter:\n" +
                   "  externalObjects: {}\n" +
                   "  userData: \n" +
                   "  assetBundleName: \n" +
                   "  assetBundleVariant: ";
        }

        // package 파일의 상대 경로와 bytes를 순서대로 해시해 동기화 지문을 만듭니다.
        private static string ComputeTreeSha256(string packageRoot)
        {
            string[] files = Directory.GetFiles(
                packageRoot,
                "*",
                SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            string prefix = packageRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] buffer = new byte[81920];
                for (int i = 0; i < files.Length; i++)
                {
                    string relativePath = NormalizePath(
                        files[i].Substring(prefix.Length));
                    byte[] pathBytes = Encoding.UTF8.GetBytes(relativePath + "\n");
                    sha256.TransformBlock(
                        pathBytes,
                        0,
                        pathBytes.Length,
                        pathBytes,
                        0);
                    using (FileStream stream = File.OpenRead(files[i]))
                    {
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            sha256.TransformBlock(
                                buffer,
                                0,
                                read,
                                buffer,
                                0);
                        }
                    }
                }
                sha256.TransformFinalBlock(new byte[0], 0, 0);
                return ToLowerHex(sha256.Hash);
            }
        }

        // SHA-256 bytes를 소문자 16진 문자열로 변환합니다.
        private static string ToLowerHex(byte[] bytes)
        {
            if (bytes == null) return string.Empty;
            StringBuilder builder = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
            {
                builder.Append(
                    bytes[i].ToString("x2", CultureInfo.InvariantCulture));
            }
            return builder.ToString();
        }

        // 저장소 root를 기준으로 UPM 출력 절대 경로를 계산합니다.
        private static string ResolvePackagesRoot()
        {
            return ResolveProjectPath(PackagesRoot);
        }

        // Application.dataPath에서 현재 Unity project root를 안정적으로 계산합니다.
        private static string ResolveProjectPath(string projectRelativePath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            if (string.IsNullOrEmpty(projectRoot))
                throw new InvalidOperationException("Unity project root is unavailable.");
            return Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    (projectRelativePath ?? string.Empty).Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        // 삭제·조회 요청을 세 개의 고정 package ID로 제한합니다.
        private static bool IsKnownPackage(string packageName)
        {
            return string.Equals(
                       packageName,
                       CorePackageName,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       packageName,
                       LabPackageName,
                       StringComparison.Ordinal) ||
                   string.Equals(
                       packageName,
                       BakingPackageName,
                       StringComparison.Ordinal);
        }

        // 파일 시스템 경로를 package 문서와 hash에 쓰는 슬래시 표기로 통일합니다.
        private static string NormalizePath(string path)
        {
            return string.IsNullOrEmpty(path)
                ? string.Empty
                : path.Replace('\\', '/');
        }
    }
}
