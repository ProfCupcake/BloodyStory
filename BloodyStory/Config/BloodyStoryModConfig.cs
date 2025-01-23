using ProtoBuf;

namespace BloodyStory.Config
{
    [ProtoContract(ImplicitFields = ImplicitFields.AllFields)]
    public class BloodyStoryModConfig // TODO: proper config documentation
    {
        public double baseRegen = 0.02f; // hp regen per second
        public double bonusRegen = 0.0016f; // added regen per point of additional max health; max bonus is this * 12.5

        public double regenBoostRate = 0.5f; // additional hp regen per second for the duration of the food boost
        public double regenBoostQuotient = 100f; // quotient for the amount of hp added per point of satiety increase for regen boost

        public double regenBedMultiplier = 8f; // multiplier for regen when lying in bed

        public int regenSitDelay = 5000; // delay before the sit boost is applied, in ms
        public double regenSitMultiplier = 3f; // multiplier for regen when sitting

        public float bleedDamageMultiplier = 1f; // multiplier for damage taken as bleed
        public float directDamageMultiplier = 0f; // multiplier for damage allowed through after bleed is applied

        public double bleedHealRate = 0.15f; // natural bleeding reduction
        public double bleedQuotient = 12f; // quotient for hp loss to bleed
        public double sneakMultiplier = 8f; // multiplier for bleed quotient applied when sneaking

        public double bleedCautMultiplier = 1f; // multiplier for how much bleed is reduced by fire damage

        public double bloodParticleMultiplier = 1f; // multiplier for the quantity of blood particles produced
        public double bloodParticleDelay = 0.05f;

        public float bandageMultiplier = 1f; // multiplier for the amount of bleed reduction when using bandages/poultice

        public float maxSatietyMultiplier = 1.2f; // multiplier for regen rate at maximum hunger saturation
        public float minSatietyMultiplier = 0f; // multiplier for regen rate at minimum hunger saturation

        public float satietyConsumption = 1f; // hunger saturation consumed per point of hp restored (sans bonus)

        public float timeDilation = 1.0f; // to adjust simulated second speed, for if game speed is changed
    }
}
