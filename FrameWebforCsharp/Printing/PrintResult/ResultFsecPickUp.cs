using System;
using System.Collections.Generic;
using System.Text;

namespace PDF_Manager.Printing
{
    class ResultFsecPickup : ResultFsecCombine
    {
        public new const string KEY = "fsecPickup";

        public ResultFsecPickup(Dictionary<string, object> value) : base(value, ResultFsecPickup.KEY)
        {

        }
    }
}
