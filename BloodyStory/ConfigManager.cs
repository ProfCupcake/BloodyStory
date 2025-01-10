using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BloodyStory
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
    internal class ConfigManager
    {
        private static readonly string configFilename = "bloodystory.json";

        private static ICoreAPI api;
        private static BloodyStoryModConfig _modConfig;
        public static BloodyStoryModConfig modConfig
        {
            get
            {
                if (_modConfig == null) Reload();
                return _modConfig;
            }
            set
            {
                _modConfig = value;
            }
        }

        public static void Initialise(ICoreAPI api)
        {
            ConfigManager.api = api;
        }

        internal static void Reload()
        {
            switch (api.Side)
            {
                case (EnumAppSide.Server):
                    _modConfig = api.LoadModConfig<BloodyStoryModConfig>(configFilename);
                    if (_modConfig == null)
                    {
                        _modConfig = new BloodyStoryModConfig();
                        api.StoreModConfig(_modConfig, configFilename);
                    }
                    NetManager.BroadcastConfig();
                    break;
                case (EnumAppSide.Client):
                    _modConfig = new BloodyStoryModConfig();
                    NetManager.RequestConfig();
                    break;
            }
        }
    }
}
