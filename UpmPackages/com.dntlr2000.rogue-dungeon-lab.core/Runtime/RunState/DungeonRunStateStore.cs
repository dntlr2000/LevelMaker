using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace RogueDungeonLab
{
    public interface IDungeonRunStateStore
    {
        // 지정 슬롯에 현재 RunState를 저장하며 기존 정상 슬롯은 교체 완료 전까지 보존합니다.
        void Save(string slotId, DungeonRunState state);

        // 지정 슬롯이 존재하면 검증된 RunState 복사본을 반환합니다.
        bool TryLoad(string slotId, out DungeonRunState state);

        // 지정 슬롯을 삭제하고 실제 삭제 여부를 반환합니다.
        bool Delete(string slotId);

        // 지정 슬롯 파일 또는 메모리 항목이 존재하는지 확인합니다.
        bool Exists(string slotId);
    }

    public sealed class DungeonRunStateStoreException : InvalidOperationException
    {
        public DungeonValidationReport ValidationReport { get; private set; }

        // 저장소 오류와 안정적인 검증 코드를 호출자에게 함께 전달합니다.
        public DungeonRunStateStoreException(
            string message,
            DungeonValidationReport validationReport,
            Exception innerException = null)
            : base(message, innerException)
        {
            ValidationReport =
                validationReport ?? new DungeonValidationReport();
        }
    }

    public static class DungeonRunStateSlot
    {
        public const int MaximumLength = 64;

        // 경로 탈출을 막기 위해 ASCII 영숫자·하이픈·밑줄만 허용한 슬롯 ID를 반환합니다.
        public static string Normalize(string slotId)
        {
            string value = slotId != null
                ? slotId.Trim()
                : string.Empty;
            if (value.Length == 0 || value.Length > MaximumLength)
                throw CreateInvalidSlotException();
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                bool isAsciiLetter =
                    character >= 'A' && character <= 'Z' ||
                    character >= 'a' && character <= 'z';
                bool isDigit =
                    character >= '0' && character <= '9';
                if (!isAsciiLetter &&
                    !isDigit &&
                    character != '-' &&
                    character != '_')
                {
                    throw CreateInvalidSlotException();
                }
            }
            return value;
        }

        // 잘못된 슬롯 ID를 코드 기반 저장소 예외로 변환합니다.
        private static DungeonRunStateStoreException
            CreateInvalidSlotException()
        {
            DungeonValidationReport report =
                new DungeonValidationReport();
            report.Add(
                DungeonRunStateValidationCodes.InvalidSlotId,
                DungeonValidationSeverity.Error,
                "RunState slot ID must use 1-64 ASCII letters, digits, '-' or '_'.");
            return new DungeonRunStateStoreException(
                "RunState slot ID is invalid.",
                report);
        }
    }

    public sealed class MemoryDungeonRunStateStore :
        IDungeonRunStateStore
    {
        private readonly Dictionary<string, DungeonRunState> _states =
            new Dictionary<string, DungeonRunState>(
                StringComparer.Ordinal);

        // 검증·정규화한 RunState 복사본을 메모리 슬롯에 저장합니다.
        public void Save(string slotId, DungeonRunState state)
        {
            string normalized = DungeonRunStateSlot.Normalize(slotId);
            _states[normalized] = PrepareForSave(state);
        }

        // 메모리 슬롯이 존재하면 외부 변경과 분리된 검증 복사본을 반환합니다.
        public bool TryLoad(
            string slotId,
            out DungeonRunState state)
        {
            string normalized = DungeonRunStateSlot.Normalize(slotId);
            DungeonRunState stored;
            if (!_states.TryGetValue(normalized, out stored))
            {
                state = null;
                return false;
            }
            ValidateLoaded(stored);
            state = stored.DeepClone();
            return true;
        }

        // 메모리에서 지정 슬롯을 제거합니다.
        public bool Delete(string slotId)
        {
            return _states.Remove(
                DungeonRunStateSlot.Normalize(slotId));
        }

        // 메모리에 지정 슬롯이 존재하는지 확인합니다.
        public bool Exists(string slotId)
        {
            return _states.ContainsKey(
                DungeonRunStateSlot.Normalize(slotId));
        }

        // 저장 시각과 canonical hash를 갱신한 유효한 복사본을 만듭니다.
        internal static DungeonRunState PrepareForSave(
            DungeonRunState state)
        {
            if (state == null)
                throw CreateStoreException(
                    DungeonRunStateValidationCodes.NullState,
                    "RunState is null.");
            DungeonRunState copy = state.DeepClone();
            copy.savedUtcTicks = DateTime.UtcNow.Ticks;
            copy.RefreshHash();
            DungeonValidationReport validation =
                DungeonRunStateValidator.Validate(copy);
            if (!validation.IsValid)
                throw new DungeonRunStateStoreException(
                    "RunState cannot be saved.",
                    validation);
            return copy;
        }

        // 저장소에서 읽은 상태의 hash와 구조를 검증합니다.
        internal static void ValidateLoaded(DungeonRunState state)
        {
            DungeonValidationReport validation =
                DungeonRunStateValidator.Validate(state);
            if (!validation.IsValid)
                throw new DungeonRunStateStoreException(
                    "Stored RunState is invalid.",
                    validation);
        }

        // 단일 코드의 저장소 예외를 생성합니다.
        internal static DungeonRunStateStoreException
            CreateStoreException(
                string code,
                string message,
                Exception innerException = null)
        {
            DungeonValidationReport report =
                new DungeonValidationReport();
            report.Add(
                code,
                DungeonValidationSeverity.Error,
                message);
            return new DungeonRunStateStoreException(
                message,
                report,
                innerException);
        }
    }

    public sealed class JsonFileDungeonRunStateStore :
        IDungeonRunStateStore
    {
        private static readonly Encoding Utf8WithoutBom =
            new UTF8Encoding(false);
        private readonly string _rootDirectory;

        public string RootDirectory
        {
            get { return _rootDirectory; }
        }

        // 지정 루트 또는 persistentDataPath 아래에 JSON 슬롯 저장소를 구성합니다.
        public JsonFileDungeonRunStateStore(
            string rootDirectory = null)
        {
            _rootDirectory = !string.IsNullOrWhiteSpace(rootDirectory)
                ? Path.GetFullPath(rootDirectory)
                : Path.Combine(
                    Application.persistentDataPath,
                    "RogueDungeonLab",
                    "RunStates");
        }

        // 임시 파일을 디스크에 기록한 뒤 File.Replace 또는 rename으로 슬롯을 교체합니다.
        public void Save(string slotId, DungeonRunState state)
        {
            string path = GetSlotPath(slotId);
            DungeonRunState copy =
                MemoryDungeonRunStateStore.PrepareForSave(state);
            string json =
                DungeonRunStateSerialization.ToJson(copy, true);
            string temporaryPath =
                path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backupPath = path + ".bak";
            try
            {
                Directory.CreateDirectory(_rootDirectory);
                WriteDurableText(temporaryPath, json);
                if (File.Exists(path))
                {
                    if (File.Exists(backupPath))
                        File.Delete(backupPath);
                    File.Replace(
                        temporaryPath,
                        path,
                        backupPath,
                        true);
                    TryDeleteBackup(backupPath);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            catch (DungeonRunStateStoreException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw MemoryDungeonRunStateStore.CreateStoreException(
                    DungeonRunStateValidationCodes.StoreFailure,
                    "RunState slot could not be saved.",
                    exception);
            }
            finally
            {
                TryDeleteTemporary(temporaryPath);
            }
        }

        // JSON 슬롯을 읽고 parse·구조·canonical hash 검증이 통과한 상태만 반환합니다.
        public bool TryLoad(
            string slotId,
            out DungeonRunState state)
        {
            string path = GetSlotPath(slotId);
            if (!File.Exists(path))
            {
                state = null;
                return false;
            }
            try
            {
                string json = File.ReadAllText(
                    path,
                    Utf8WithoutBom);
                DungeonRunState loaded =
                    DungeonRunStateSerialization.FromJson(json);
                MemoryDungeonRunStateStore.ValidateLoaded(loaded);
                state = loaded;
                return true;
            }
            catch (DungeonRunStateStoreException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw MemoryDungeonRunStateStore.CreateStoreException(
                    DungeonRunStateValidationCodes.StoreFailure,
                    "RunState slot could not be loaded.",
                    exception);
            }
        }

        // 지정 JSON 슬롯과 남은 백업 파일을 제거합니다.
        public bool Delete(string slotId)
        {
            string path = GetSlotPath(slotId);
            try
            {
                bool existed = File.Exists(path);
                if (existed) File.Delete(path);
                string backupPath = path + ".bak";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                return existed;
            }
            catch (Exception exception)
            {
                throw MemoryDungeonRunStateStore.CreateStoreException(
                    DungeonRunStateValidationCodes.StoreFailure,
                    "RunState slot could not be deleted.",
                    exception);
            }
        }

        // 정규화한 슬롯의 JSON 파일 존재 여부를 확인합니다.
        public bool Exists(string slotId)
        {
            return File.Exists(GetSlotPath(slotId));
        }

        // 정규화한 슬롯 ID를 저장 루트 바로 아래의 JSON 경로로 변환합니다.
        public string GetSlotPath(string slotId)
        {
            string normalized =
                DungeonRunStateSlot.Normalize(slotId);
            return Path.Combine(
                _rootDirectory,
                normalized + ".json");
        }

        // UTF-8 JSON을 flush-to-disk한 뒤 스트림을 닫습니다.
        private static void WriteDurableText(
            string path,
            string value)
        {
            using (FileStream stream = new FileStream(
                       path,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            using (StreamWriter writer =
                   new StreamWriter(stream, Utf8WithoutBom))
            {
                writer.Write(value);
                writer.Flush();
                stream.Flush(true);
            }
        }

        // 성공한 교체 뒤 남은 백업은 저장 성공을 되돌리지 않도록 최선 노력으로 정리합니다.
        private static void TryDeleteBackup(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        // 실패하거나 완료한 쓰기의 남은 임시 파일을 최선 노력으로 정리합니다.
        private static void TryDeleteTemporary(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
