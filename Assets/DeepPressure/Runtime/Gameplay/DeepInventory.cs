using System;
using System.Collections.Generic;
namespace DeepPressure
{
    [Serializable]
    public sealed partial class DeepInventory
    {
        readonly Dictionary<string,int> amounts = new Dictionary<string,int>(StringComparer.Ordinal);
        int heldUnits;
        public int Capacity { get; private set; } = 300;
        public int UsedCapacity { get { int total = heldUnits; foreach (int amount in amounts.Values) total += amount; return total; } }
        public int AvailableCapacity => Math.Max(0,Capacity-UsedCapacity);
        public IReadOnlyDictionary<string,int> Items => amounts;
        public int GetAmount(string id) => !string.IsNullOrEmpty(id) && amounts.TryGetValue(id,out int amount) ? amount : 0;
        public int GetAmount(DeepItemDefinition item) => item == null ? 0 : GetAmount(item.id);
        public void SetCapacity(int capacity) => Capacity = Math.Max(0,capacity);
        public bool CanAfford(DeepItemAmount[] cost,out string reason,int multiplier = 1)
        {
            if (!Aggregate(cost,multiplier,out var required,out reason)) return false;
            foreach (var entry in required) if (GetAmount(entry.Key) < entry.Value) { reason = "物料不足："+entry.Key; return false; }
            reason = string.Empty; return true;
        }
        public bool TryAdd(DeepItemDefinition item,int amount)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || amount < 0 || amount > AvailableCapacity) return false;
            amounts[item.id] = GetAmount(item.id)+amount; return true;
        }
        public bool TryConsume(DeepItemDefinition item,int amount)
        {
            if (item == null || amount < 0 || GetAmount(item) < amount) return false;
            amounts[item.id] = GetAmount(item)-amount; return true;
        }
        public bool TryReserve(DeepItemAmount[] cost,out DeepReservation reservation,out string reason,int multiplier = 1)
        {
            reservation = null;
            if (!CanAfford(cost,out reason,multiplier) || !Aggregate(cost,multiplier,out var required,out reason)) return false;
            foreach (var entry in required) { amounts[entry.Key] = GetAmount(entry.Key)-entry.Value; heldUnits += entry.Value; }
            reservation = new DeepReservation(required); return true;
        }
        public void Refund(DeepReservation reservation)
        {
            if (reservation == null || reservation.settled) return;
            foreach (var entry in reservation.items) { amounts[entry.Key] = GetAmount(entry.Key)+entry.Value; heldUnits -= entry.Value; }
            reservation.settled = true;
        }
        public void Commit(DeepReservation reservation) { if (reservation == null || reservation.settled) return; heldUnits -= reservation.Units; reservation.settled = true; }
        public bool CanComplete(DeepReservation reservation,DeepItemAmount[] output,int multiplier,out string reason)
        {
            if (reservation != null && reservation.settled) { reason = "物料预留已经结算"; return false; }
            if (!Aggregate(output,multiplier,out var products,out reason)) return false;
            long units = 0; foreach (var entry in products) units += entry.Value;
            int released = reservation != null && !reservation.settled ? reservation.Units : 0;
            if ((long)UsedCapacity-released+units > Capacity) { reason = "仓库已满，成品等待入库"; return false; }
            reason = string.Empty; return true;
        }
        public bool Complete(DeepReservation reservation,DeepItemAmount[] output,int multiplier,out string reason)
        {
            if (!CanComplete(reservation,output,multiplier,out reason) || !Aggregate(output,multiplier,out var products,out reason)) return false;
            Commit(reservation); foreach (var entry in products) amounts[entry.Key] = GetAmount(entry.Key)+entry.Value; return true;
        }
        static bool Aggregate(DeepItemAmount[] items,int multiplier,out Dictionary<string,int> result,out string reason)
        {
            result = new Dictionary<string,int>(StringComparer.Ordinal); reason = string.Empty;
            if (multiplier < 1 || multiplier > 999) { reason = "数量无效"; return false; }
            if (items == null) return true;
            foreach (var item in items)
            {
                if (item.item == null || string.IsNullOrWhiteSpace(item.item.id) || item.amount < 0) { reason = "物料配置无效"; return false; }
                long total = (result.TryGetValue(item.item.id,out int old) ? old : 0)+(long)item.amount*multiplier;
                if (total > int.MaxValue) { reason = "物料数量过大"; return false; }
                result[item.item.id] = (int)total;
            }
            return true;
        }
    }
    public sealed class DeepReservation
    {
        internal readonly Dictionary<string,int> items;
        internal bool settled;
        public bool IsSettled => settled;
        public int Units { get { int sum = 0; foreach (int count in items.Values) sum += count; return sum; } }
        internal DeepReservation(Dictionary<string,int> items) { this.items = items; }
    }
}
