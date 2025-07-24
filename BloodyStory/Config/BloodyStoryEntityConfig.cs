namespace BloodyStory.Config
{
    public class BloodyStoryEntityConfig
    {
        public bool ConfigEnabled = false; // if true, applies this config. if false, this config is ignored and standard config is used

        public bool BleedEnabled = true;

        public double baseRegen = 0.02f;

        public float bleedDamageMultiplier_blunt = 1f; 
        public float directDamageMultiplier_blunt = 0f; 

        public float bleedDamageMultiplier_slash = 1f; 
        public float directDamageMultiplier_slash = 0f;

        public float bleedDamageMultiplier_pierce = 1f; 
        public float directDamageMultiplier_pierce = 0f;

        public float bleedDamageMultiplier_poison = 1f; 
        public float directDamageMultiplier_poison = 0f;

        public double bleedHealRate = 0.15f; 
        public double bleedQuotient = 12f; 

        public double bleedCautMultiplier = 1f; 

        public double bloodParticleMultiplier = 1f; 
        public double bloodParticleDelay = 0.05f; 
    }
}
