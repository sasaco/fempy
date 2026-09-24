using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;

namespace FrameWebforCS.components.result
{
    internal class ResultPickupFsecComponent: ResultCombineFsecComponent
    {
        public override Dictionary<string, object> getCombineFsec()
        {
            return InputDataService.Instance.getPickupFsec();
        }
    }
}
