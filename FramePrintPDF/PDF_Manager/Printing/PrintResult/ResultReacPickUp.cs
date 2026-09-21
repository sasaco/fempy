using System;
using System.Collections.Generic;
using System.Text;

namespace PDF_Manager.Printing
{
    class ResultReacPickup : ResultReacCombine
    {
        public new const string KEY = "reacPickup";

        public ResultReacPickup(Dictionary<string, object> value) : base(value, ResultReacPickup.KEY)
        {

        }
    }
}
