using System.Linq;
using UnityEngine;

namespace Mongle.Data
{
    /// <summary>Static content root, loaded from Resources/ContentCatalog. Holds no runtime state.</summary>
    [CreateAssetMenu(menuName = "Mongle/Content Catalog")]
    public class ContentCatalog : ScriptableObject
    {
        public ToyDefinition toy;
        /// <summary>Toy friends who come to the hospital one by one and then move into the village, in visiting order (D-005).</summary>
        public ToyDefinition[] friends;
        public ToolDefinition[] tools;
        public FurnitureDefinition[] furniture;
        public ShopDefinition[] shops;
        public GameObject wateringCan;
        /// <summary>Gingerbread house variants (D-012, HouseLots.Variants of them, built by BackdropBuilder.Houses).</summary>
        public GameObject[] houses;
        public QuestDefinition[] quests;
        public Material[] materials; // keeps runtime-used materials/shaders in builds (dirt, foam, thread, ...)

        public ToolDefinition Tool(string id) => tools?.FirstOrDefault(t => t != null && t.id == id);
        public FurnitureDefinition Furniture(string id) => furniture?.FirstOrDefault(f => f != null && f.id == id);
        public QuestDefinition Quest(string id) => quests?.FirstOrDefault(q => q != null && q.id == id);
        public ShopDefinition Shop(string id) => shops?.FirstOrDefault(s => s != null && s.id == id);
        /// <summary>Mongi or one of the friends (null when unknown).</summary>
        public ToyDefinition ToyDef(string kindId)
        {
            if (toy != null && toy.kindId == kindId) return toy;
            return friends?.FirstOrDefault(f => f != null && f.kindId == kindId);
        }
        System.Collections.Generic.IEnumerable<ToyDefinition> AllToys => new[] { toy }.Concat(friends ?? new ToyDefinition[0]).Where(t => t != null);
        /// <summary>Every accessory any toy can wear, once each (what the accessory shop sells).</summary>
        public System.Collections.Generic.IEnumerable<AccessoryDefinition> AllAccessories => AllToys.SelectMany(t => t.accessories ?? new AccessoryDefinition[0]).Where(a => a != null).GroupBy(a => a.id).Select(g => g.First());
        // accessories are shared by every toy; damages are found by id across all toys (Mongi's tutorial ids + each friend's own, D-009)
        public AccessoryDefinition Accessory(string id) => AllToys.SelectMany(t => t.accessories ?? new AccessoryDefinition[0]).FirstOrDefault(a => a != null && a.id == id);
        public DamageDefinition Damage(string id) => AllToys.SelectMany(t => t.damageSequence ?? new DamageDefinition[0]).FirstOrDefault(d => d != null && d.id == id);
        DamageWording Wording(string kindId, DamageDefinition d) => d == null ? null : ToyDef(kindId)?.wording?.FirstOrDefault(w => w != null && w.id == d.id);
        /// <summary>Damage name as this toy says it (falls back to the shared definition).</summary>
        public string DamageName(string kindId, DamageDefinition d) { var w = Wording(kindId, d); return w != null && !string.IsNullOrEmpty(w.displayName) ? w.displayName : d?.displayName; }
        public string DamageNote(string kindId, DamageDefinition d) { var w = Wording(kindId, d); return w != null && !string.IsNullOrEmpty(w.noteText) ? w.noteText : d?.noteText; }
        public Material Mat(string name) => materials?.FirstOrDefault(m => m != null && m.name == name);

        public const string ResourcePath = "ContentCatalog";
        public static ContentCatalog Load() => Resources.Load<ContentCatalog>(ResourcePath);
    }
}
