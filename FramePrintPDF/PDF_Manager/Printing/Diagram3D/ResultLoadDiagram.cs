using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PDF_Manager.Printing.Diagram3D
{
    class ResultGraphical : PrintBase3dDiagram
    {
        public const string KEY = "PrintScreenData";       
        List<GraphicalOutput> load = new List<GraphicalOutput>();
        public ResultGraphical(Dictionary<string, object> value)
        {
            if (!value.ContainsKey(KEY))
                return;
            JArray target = JArray.FromObject(value[KEY]);
            for (int i =0; i< target.Count(); i++)
            {
                JToken item = target[i];
                var graphic = new GraphicalOutput();
                graphic.Mode = item["mode"].ToString();
                graphic.Title = item["title1"].ToString();                
                graphic.Result = JsonConvert.DeserializeObject<List<Result>>(item["result"].ToString());
                load.Add(graphic);
            }

        }         
        protected override List<GraphicalOutput> Load() => load;
       
    }
    class GraphicalOutput
    {
        public string Mode { get; set; }
        public List<Result> Result { get; set; }
        public string Title { get; set; }
    }
    public class Result
    {
        public bool Judge { get; set; }
        public string src { get; set; }
        public string title { get; set; }
        public string type { get; set; }
        public string max_three { get; set; }
        public string min_three { get; set; }
        public string disgSubInfo1 { get; set; }
        public string disgSubInfo2 { get; set; }
    }
}
