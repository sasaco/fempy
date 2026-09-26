using FrameWebforCS.providers;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace FrameWebforCS.components.input
{
    internal class clsNoticePoints
    {
        public int row;
        public string? m = null;
        public List<float>? Points = null;
    }

    internal class InputNoticePointsService
    {
        private static readonly Lazy<InputNoticePointsService> _instance =
            new Lazy<InputNoticePointsService>(() => new InputNoticePointsService());

        public static InputNoticePointsService Instance => _instance.Value;

        private List<clsNoticePoints> _noticePoints;

        private InputNoticePointsService()
        {
            this.clear();
        }

        public void clear()
        {
            this._noticePoints = new List<clsNoticePoints>();
        }

        /// <summary>
        /// ファイルを読み込むとき
        /// </summary>
        /// <param name="jsonData"></param>
        public void setNoticePointsJson(JsonElement jsonData)
        {
            if (!jsonData.TryGetProperty("notice_points", out JsonElement noticePointsJson))
                return;

            var noticePoints = DataHelperModule.JsonToList<clsNoticePoints>(noticePointsJson);
            if (noticePoints != null) this._noticePoints = noticePoints;
        }

        /// <summary>
        /// ファイルに保存するとき
        /// </summary>
        public List<Dictionary<string, object?>> getNoticePointsJson()
        {
            var noticePoints = new List<Dictionary<string, object?>>();
            foreach (clsNoticePoints value in this._noticePoints)
            {
                noticePoints.Add(DataHelperModule.ClassToDictionary(value));
            }
            return noticePoints;
        }
    }
}
