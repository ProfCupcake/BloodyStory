using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace BloodyStory
{
    public delegate void OnBleedoutDelegate(out bool shouldDie, DamageSource lastHit);
}
