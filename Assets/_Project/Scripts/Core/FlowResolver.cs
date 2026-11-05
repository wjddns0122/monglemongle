using Mongle.Data;
using Mongle.Toy;

namespace Mongle.Core
{
    /// <summary>Pure derivation of the current phase from toy state (used after load and between scenes).</summary>
    public static class FlowResolver
    {
        public static FlowPhase Resolve(ToyRuntime t)
        {
            if (t == null) return FlowPhase.CustomerArrival;
            if (t.Admitted) return FlowPhase.Village;
            if (t.PhotoConfirmed) return FlowPhase.VillageAdmission;
            if (t.CustomizationConfirmed) return FlowPhase.PhotoStudio;
            if (t.StampDone) return FlowPhase.Customization;
            if (t.AllRepaired) return FlowPhase.CompletionStamp;
            if (t.DiagnosisComplete) return FlowPhase.Repair;
            return FlowPhase.CustomerArrival;
        }

        /// <summary>A hospital patient (D-005): same repair journey as Mongi; after the dress-up and a photo it moves into the village (no admission step).</summary>
        public static FlowPhase ResolvePatient(ToyRuntime t)
        {
            if (t == null || t.PhotoConfirmed) return FlowPhase.Village;
            if (t.CustomizationConfirmed) return FlowPhase.PhotoStudio; // friends get a photo for the album too
            if (t.StampDone) return FlowPhase.Customization;
            if (t.AllRepaired) return FlowPhase.CompletionStamp;
            if (t.DiagnosisComplete) return FlowPhase.Repair;
            return FlowPhase.CustomerArrival;
        }

        public static string SceneFor(FlowPhase p)
        {
            switch (p)
            {
                case FlowPhase.PhotoStudio: return SceneNames.PhotoStudio;
                case FlowPhase.VillageAdmission:
                case FlowPhase.Village: return SceneNames.Village;
                default: return SceneNames.RepairShop;
            }
        }
    }

    public static class SceneNames
    {
        public const string Bootstrap = "Bootstrap";
        public const string RepairShop = "RepairShop";
        public const string PhotoStudio = "PhotoStudio";
        public const string Village = "Village";
    }
}
