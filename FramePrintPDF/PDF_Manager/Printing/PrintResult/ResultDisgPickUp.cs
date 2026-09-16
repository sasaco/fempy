using System;
using System.Collections.Generic;
using System.Text;

namespace PDF_Manager.Printing
{
    internal class ResultDisgPickup : ResultDisgCombine
    {
        public new const string KEY = "disgPickup";

        public ResultDisgPickup(Dictionary<string, object> value) : base(value, ResultDisgPickup.KEY)
        {

        }
    }
}
