using System;

namespace Mongle.Data
{
    public enum DamageType { Thorn, Dirt, Tear, LowCotton, EyeButton, Fuel, Battery, Paint } // append only: saved/serialized as ints
    public enum RepairPhase { Remove, Clean, Repair, Restore, Finish }
    public enum DamageStatus { Hidden, Discovered, Available, Active, Completed, Locked }
    public enum AccessorySlot { Head, Face, Neck }
    public enum Personality { Shy, Energetic, Sleepy, Curious, Gentle, Mischievous }
    public enum ToyCategory { Plush, WindUp, Electronic }

    [Flags]
    public enum InteractionTag
    {
        None = 0, Sit = 1, Sleep = 2, Play = 4, Music = 8, Flower = 16, Social = 32, Watch = 64
    }

    /// <summary>Where the player is in a toy's journey. Derived from state by FlowResolver.</summary>
    public enum FlowPhase
    {
        Boot, CustomerArrival, CustomerIntro, Diagnosis, Repair, CompletionStamp,
        Customization, PhotoStudio, VillageAdmission, Village
    }
}
