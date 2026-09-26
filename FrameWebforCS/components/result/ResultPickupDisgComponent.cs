using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.components.result
{
    internal class ResultPickupDisgComponent : ResultCombineDisgComponent
    {
        protected override bool UsesLegacyPickup => true;

        public override Dictionary<string, object> getCombineDisg()
        {
            return InputDataService.Instance.getPickupDisg();
        }
    }
}
