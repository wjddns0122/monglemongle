using UnityEngine;

namespace Mongle.Data
{
    /// <summary>Static content for one damage (02_REPAIR_SYSTEM §13 contract). Never holds runtime progress.</summary>
    [CreateAssetMenu(menuName = "Mongle/Damage Definition")]
    public class DamageDefinition : ScriptableObject
    {
        public string id;
        public DamageType type;
        public RepairPhase phase;
        public string displayName;      // shown only after discovery
        public string noteText;         // discovery note, discovery-only text
        public string requiredToolId;
        public string miniGameKey;
        public string damagePointName;  // DP_* / FP_* node in toy model
        public string cameraAnchorName; // CA_* node used for the discovery focus shot
        public string repairAnchorName;  // CA_* node used for the repair close-up (falls back to cameraAnchorName)
        public string lookPointName;     // optional: close-up looks at this node instead of damagePointName
        public float interactionRadius = 0.05f; // generous select radius in meters
        public int hintPriority;
        public int rewardCoins;         // D-002: paid once when this repair is completed

        [Header("D-009 site damage (empty site = Mongi's legacy nodes, C-001)")]
        public string site;             // <site> of the DP_/DN_/CA_/SP_/FP_ node names
        public string boneName;         // bone the damage rides on (threads, rings, dirt)
        public string visualName;       // DMG_* shown while hurt (thorn, tear, loose eye, open hole, fuel cap, old battery)
        public string visualDoneName;   // DMG_* shown once repaired (new battery)
        public string openName;         // DMG_* shown only while working (open fuel port)
        public string shapeKey;         // body shape key: EyeGone_<site> / Deflated_<site>
        public string grabPointName;    // GP_THORN_<site>: tip of the thorn (where the tweezers grab)
        public string normalPointName;  // DN_<site>: outward = DN - DP
        public string stitchPrefix;     // SP_<site>_ (01..NN are appended)
        public string limpBone;         // bone that hangs limp while this tear is open
        public string workPointName;    // FP_<site>: cotton inlet, fuel port, battery slot
        public float spread = 0.06f;    // dirt/paint scatter radius
        public int blobCount = 26;
        public Color stainColor = new Color(0.36f, 0.25f, 0.15f); // dirt/bare wood colour, or the liquid poured in (Fuel)
        public Color cleanColor = new Color(0.95f, 0.88f, 0.72f); // what a cleaned spot fades to (paint colour for Paint)

        public bool HasSite => !string.IsNullOrEmpty(site);
    }
}
