using FarPoint.Win;
using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsJoint
    {
        public int? row = null;
        public string? m = null;
        public float? xi = null;
        public float? yi = null;
        public float? zi = null;
        public float? xj = null;
        public float? yj = null;
        public float? zj = null;
    }

    internal class InputJointService
    {
        private static readonly Lazy<InputJointService> _instance =
            new Lazy<InputJointService>(() => new InputJointService());

        public static InputJointService Instance => _instance.Value;

        private Dictionary<string, List<clsJoint>> _joint;

        private InputJointService()
        {
            this.clear();
        }

        public void clear()
        {
            this._joint = new Dictionary<string, List<clsJoint>>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setJointJson(JsonElement jsonData)
        {
            var joints = DataHelperModule.JsonToDict(
                jsonData,
                "joint",
                static jointJson => DataHelperModule.JsonToList<clsJoint>(jointJson));

            if (joints != null) this._joint = joints;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        public Dictionary<string, object> getJointJson()
        {
            var joints = new Dictionary<string, object>();
            foreach (KeyValuePair<string, List<clsJoint>> joint in this._joint)
            {
                var rows = new List<Dictionary<string, object?>>();
                foreach (clsJoint value in joint.Value)
                {
                    rows.Add(DataHelperModule.ClassToDictionary(value));
                }

                if (rows.Count > 0)
                    joints.Add(joint.Key, rows);
            }
            return joints;
        }
    }
}
