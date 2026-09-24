using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.components.result
{
    internal class ResultPickupReacComponent : ResultCombineReacComponent
    {
        public override Dictionary<string, object> getCombineReac()
        {
            return InputDataService.Instance.getPickupReac();
        }
    }
}
