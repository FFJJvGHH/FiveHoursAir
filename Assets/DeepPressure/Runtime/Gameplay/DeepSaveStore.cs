using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
namespace DeepPressure
{
    public static class DeepSaveStore
    {
        [Serializable] sealed class Envelope
        { public string format = "DeepPressure.Save"; public int version = 1; public string checksum,payload; }
        public static readonly string[] Slots = { "quick", "manual1", "manual2", "manual3", "autosave" };
        public static string SlotTitle(string slot)
        {
            switch(slot) { case "quick": return "快速存档"; case "manual1": return "手动存档 1"; case "manual2": return "手动存档 2"; case "manual3": return "手动存档 3"; case "autosave": return "自动存档"; default: return slot; }
        }
        public static string Encode(DeepSaveData data)
        {
            string payload = JsonUtility.ToJson(data);
            return JsonUtility.ToJson(new Envelope { payload = payload, checksum = Hash(payload) },true);
        }
        public static bool Decode(string json,out DeepSaveData data,out string reason)
        {
            data = null;
            try
            {
                if (string.IsNullOrWhiteSpace(json) || json.Length > 32*1024*1024) throw new InvalidDataException("存档为空或超出大小限制");
                var envelope = JsonUtility.FromJson<Envelope>(json);
                if (envelope == null || envelope.format != "DeepPressure.Save" || envelope.version != 1) throw new InvalidDataException("不支持的存档格式或版本");
                if (string.IsNullOrEmpty(envelope.payload) || !string.Equals(envelope.checksum,Hash(envelope.payload),StringComparison.Ordinal)) throw new InvalidDataException("存档校验失败，文件可能损坏");
                data = JsonUtility.FromJson<DeepSaveData>(envelope.payload);
                if (data == null || data.version != 1) throw new InvalidDataException("不支持的世界数据版本");
                reason = string.Empty; return true;
            }
            catch (Exception exception) { data = null; reason = exception.Message; return false; }
        }
        public static bool Write(string directory,string slot,DeepSaveData data,out string reason)
        {
            string temporary = null;
            try
            {
                string path = PathFor(directory,slot); Directory.CreateDirectory(directory);
                temporary = path+".tmp";
                byte[] bytes = new UTF8Encoding(false).GetBytes(Encode(data));
                using (var stream = new FileStream(temporary,FileMode.Create,FileAccess.Write,FileShare.None)) { stream.Write(bytes,0,bytes.Length); stream.Flush(true); }
                if (!ReadFile(temporary,out _,out string validation)) throw new InvalidDataException(validation);
                if (File.Exists(path))
                {
                    bool healthy = ReadFile(path,out _,out _);
                    try { File.Replace(temporary,path,healthy ? path+".bak" : null); }
                    catch (PlatformNotSupportedException)
                    {
                        // The previous valid generation survives an interrupted rename on these platforms.
                        if (healthy) File.Copy(path,path+".bak",true);
                        File.Delete(path); File.Move(temporary,path);
                    }
                }
                else File.Move(temporary,path);
                reason = SlotTitle(slot)+"已保存"; return true;
            }
            catch (Exception exception) { reason = "保存失败："+exception.Message; return false; }
            finally { if (temporary != null && File.Exists(temporary)) { try { File.Delete(temporary); } catch (IOException) {} } }
        }
        public static bool Read(string directory,string slot,out DeepSaveData data,out string reason,out bool recovered)
        {
            data = null; recovered = false;
            try
            {
                string path = PathFor(directory,slot);
                if (ReadFile(path,out data,out reason)) return true;
                string original = reason;
                if (ReadFile(path+".bak",out data,out _)) { recovered = true; reason = "已从上一份完整备份恢复"; return true; }
                reason = "读取失败："+original; return false;
            }
            catch (Exception exception) { reason = "读取失败："+exception.Message; return false; }
        }
        public static DeepSaveSlotInfo[] List(string directory)
        {
            var results = new DeepSaveSlotInfo[Slots.Length];
            for (int i = 0; i < Slots.Length; i++)
            {
                string slot = Slots[i], path = PathFor(directory,slot);
                var item = new DeepSaveSlotInfo { slotId = slot,title = SlotTitle(slot),exists = File.Exists(path) || File.Exists(path+".bak") };
                if (item.exists)
                {
                    item.isValid = Read(directory,slot,out var data,out item.detail,out item.recoveredFromBackup);
                    if (item.isValid) { item.savedUtc = data.savedUtc; item.simulationTime = data.simulationTime; }
                }
                results[i] = item;
            }
            return results;
        }
        static string PathFor(string directory,string slot)
        {
            if (Array.IndexOf(Slots,slot) < 0) throw new ArgumentException("无效存档槽");
            return Path.Combine(directory,slot+".json");
        }
        static bool ReadFile(string path,out DeepSaveData data,out string reason)
        {
            data = null;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) { reason = "此存档槽尚未保存"; return false; }
                if (info.Length > 32*1024*1024) { reason = "存档超过大小限制"; return false; }
                return Decode(File.ReadAllText(path,Encoding.UTF8),out data,out reason);
            }
            catch (Exception exception) { reason = exception.Message; return false; }
        }
        static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-",string.Empty); }
    }
}
