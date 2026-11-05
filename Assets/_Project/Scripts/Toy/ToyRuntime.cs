using System;
using System.Collections.Generic;
using System.Linq;
using Mongle.Data;

namespace Mongle.Toy
{
    [Serializable]
    public class DamageRuntime
    {
        public string id;
        public DamageStatus status;
        public float progress; // runtime-only, never saved (D-001-9)
    }

    [Serializable]
    public class HistoryEntry
    {
        public string key;   // unique key, prevents duplicates
        public string date;  // yyyy-MM-dd
        public string text;
    }

    [Serializable]
    public class PhotoRecord
    {
        public string photoId;
        public string residentId;
        public string pose;
        public string background;
        public string imageFile; // relative file name under the photos folder
        public string takenAt;
        public List<string> accessoryIds = new List<string>();
    }

    [Serializable]
    public class FurniturePlacement
    {
        public string furnitureId;
        public string instanceId; // unique per placed piece; empty on legacy saves (then furnitureId is the id)
        public int cellX;
        public int cellY;
        public int rotation; // 0..3 quarter turns
        public string Id => string.IsNullOrEmpty(instanceId) ? furnitureId : instanceId;
    }

    /// <summary>
    /// Runtime state of the single toy (몽이). Content lives in ToyDefinition; save data is mapped by SaveMapper.
    /// All transitions are guarded so each completion event fires exactly once (02 §5).
    /// </summary>
    public class ToyRuntime
    {
        public string KindId { get; private set; }
        public string ResidentId { get; private set; }
        public readonly List<DamageRuntime> Damages = new List<DamageRuntime>();
        public readonly List<HistoryEntry> History = new List<HistoryEntry>();
        public readonly Dictionary<AccessorySlot, string> Equipped = new Dictionary<AccessorySlot, string>();
        public readonly Dictionary<AccessorySlot, string> Preview = new Dictionary<AccessorySlot, string>();
        public readonly List<FurniturePlacement> Furniture = new List<FurniturePlacement>();
        public PhotoRecord Photo;
        public bool DiagnosisComplete { get; private set; }
        public bool StampDone { get; private set; }
        public bool CustomizationConfirmed { get; private set; }
        public bool PhotoConfirmed => Photo != null && !string.IsNullOrEmpty(Photo.photoId);
        public bool Admitted { get; private set; }
        public string AdmissionDate { get; private set; }
        public Func<DateTime> Clock = () => DateTime.Now;

        public event Action<string> DamageDiscovered;
        public event Action<string> DamageStarted;
        public event Action<string> DamageCompleted;
        public event Action DiagnosisCompleted;
        public event Action AllRepairsCompleted;
        public event Action StampCompleted;
        public event Action CustomizationChanged;
        public event Action CustomizationConfirmedEvent;
        public event Action<PhotoRecord> PhotoConfirmedEvent;
        public event Action ToyAdmitted;

        public ToyRuntime(string kindId, IEnumerable<string> orderedDamageIds)
        {
            KindId = kindId;
            foreach (var id in orderedDamageIds)
                Damages.Add(new DamageRuntime { id = id, status = DamageStatus.Hidden });
        }

        public DamageRuntime Get(string id) => Damages.FirstOrDefault(d => d.id == id);
        public bool AllDiscovered => Damages.All(d => d.status != DamageStatus.Hidden);
        public bool AllRepaired => Damages.Count > 0 && Damages.All(d => d.status == DamageStatus.Completed);
        public int CompletedCount => Damages.Count(d => d.status == DamageStatus.Completed);

        /// <summary>First damage in repair order that is not completed (null when all done).</summary>
        public DamageRuntime NextRepair => Damages.FirstOrDefault(d => d.status != DamageStatus.Completed);

        // ---- Diagnosis ----
        public bool Discover(string id)
        {
            var d = Get(id);
            if (d == null || d.status != DamageStatus.Hidden) return false;
            d.status = DamageStatus.Discovered;
            DamageDiscovered?.Invoke(id);
            if (AllDiscovered) FinishDiagnosis();
            return true;
        }

        void FinishDiagnosis()
        {
            if (DiagnosisComplete) return;
            DiagnosisComplete = true;
            UnlockRepairOrder();
            DiagnosisCompleted?.Invoke();
        }

        /// <summary>First incomplete damage becomes Available, later ones Locked (repair order is data).</summary>
        void UnlockRepairOrder()
        {
            bool first = true;
            foreach (var d in Damages)
            {
                if (d.status == DamageStatus.Completed) continue;
                d.status = first ? DamageStatus.Available : DamageStatus.Locked;
                first = false;
            }
        }

        // ---- Repair ----
        public bool BeginRepair(string id)
        {
            var d = Get(id);
            if (d == null || d.status != DamageStatus.Available) return false;
            d.status = DamageStatus.Active;
            d.progress = 0f;
            DamageStarted?.Invoke(id);
            return true;
        }

        public void CancelRepair(string id)
        {
            var d = Get(id);
            if (d == null || d.status != DamageStatus.Active) return;
            d.status = DamageStatus.Available;
            d.progress = 0f; // unfinished repair returns to its start state
        }

        public float SetProgress(string id, float value)
        {
            var d = Get(id);
            if (d == null || d.status != DamageStatus.Active) return 0f;
            d.progress = Math.Max(0f, Math.Min(1f, value));
            return d.progress;
        }

        /// <summary>Completes a damage exactly once; unlocks the next one; fires AllRepairsCompleted once.</summary>
        public bool CompleteDamage(string id, string historyText = null)
        {
            var d = Get(id);
            if (d == null || d.status != DamageStatus.Active) return false;
            d.status = DamageStatus.Completed;
            d.progress = 1f;
            AddHistory("Damage:" + id, historyText ?? id);
            var next = Damages.FirstOrDefault(x => x.status == DamageStatus.Locked);
            if (next != null) next.status = DamageStatus.Available;
            DamageCompleted?.Invoke(id);
            if (AllRepaired) AllRepairsCompleted?.Invoke();
            return true;
        }

        public bool CompleteStamp()
        {
            if (StampDone || !AllRepaired) return false;
            StampDone = true;
            AddHistory("Stamp", "수선 완료 스탬프");
            StampCompleted?.Invoke();
            return true;
        }

        // ---- Customization ----
        public bool SetPreview(AccessorySlot slot, string accessoryId)
        {
            if (!StampDone || CustomizationConfirmed) return false;
            if (string.IsNullOrEmpty(accessoryId)) { if (!Preview.Remove(slot)) return false; }
            else Preview[slot] = accessoryId;
            CustomizationChanged?.Invoke();
            return true;
        }

        public bool ConfirmCustomization()
        {
            if (!StampDone || CustomizationConfirmed) return false;
            Equipped.Clear();
            foreach (var kv in Preview) Equipped[kv.Key] = kv.Value;
            CustomizationConfirmed = true;
            foreach (var kv in Equipped) AddHistory("Accessory:" + kv.Value, "선물: " + kv.Value);
            CustomizationConfirmedEvent?.Invoke();
            return true;
        }

        /// <summary>Accessories as they should be displayed now (preview while editing, equipped after).</summary>
        public IReadOnlyDictionary<AccessorySlot, string> Visible => CustomizationConfirmed ? Equipped : Preview;

        // ---- Photo ----
        public bool ConfirmPhoto(PhotoRecord photo)
        {
            if (!CustomizationConfirmed || PhotoConfirmed || photo == null) return false;
            Photo = photo;
            AddHistory("Photo:First", "첫 사진: " + photo.pose);
            PhotoConfirmedEvent?.Invoke(photo);
            return true;
        }

        // ---- Admission ----
        /// <summary>The individual (resident) id is created once when the toy arrives; kind id stays shared.</summary>
        public void EnsureResidentId(string id) { if (string.IsNullOrEmpty(ResidentId)) ResidentId = id; }

        public bool Admit(string residentId, IEnumerable<FurniturePlacement> defaultLayout)
        {
            if (Admitted || !PhotoConfirmed) return false;
            Admitted = true;
            if (string.IsNullOrEmpty(ResidentId)) ResidentId = residentId;
            AdmissionDate = Clock().ToString("yyyy-MM-dd");
            Furniture.Clear();
            if (defaultLayout != null) Furniture.AddRange(defaultLayout);
            AddHistory("Admitted", AdmissionDate + " 입주");
            ToyAdmitted?.Invoke();
            return true;
        }

        public bool AddHistory(string key, string text)
        {
            if (History.Any(h => h.key == key)) return false;
            History.Add(new HistoryEntry { key = key, date = Clock().ToString("yyyy-MM-dd"), text = text });
            return true;
        }

        // ---- Restore (SaveMapper only) ----
        internal void RestoreFlags(bool diagnosisComplete, bool stamp, bool customizationConfirmed, bool admitted, string residentId, string admissionDate)
        {
            DiagnosisComplete = diagnosisComplete;
            StampDone = stamp;
            CustomizationConfirmed = customizationConfirmed;
            Admitted = admitted;
            ResidentId = residentId;
            AdmissionDate = admissionDate;
        }

        internal void RestoreAfterDamages()
        {
            if (DiagnosisComplete) UnlockRepairOrder();
        }
    }
}
