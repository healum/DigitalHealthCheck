using System.Collections.Generic;
using DigitalHealthCheckEF;

namespace DigitalHealthCheckWeb.Model
{
    public class ActionCategory
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string Call { get; set; }
        public string Label { get; set; }
        public bool HasInterventions { get; set; }
        public List<Intervention> Items { get; set; }

        public string CustomBarrier { get; set; }

        public ActionCategory(string title, string call, string label, string name, List<Intervention> interventions,
                List<int> selectedInterventions, string customBarrier)
        {
            this.Title = title;
            this.Call = call;
            this.Name = name;
            this.Label = label;
            this.HasInterventions = interventions != null && interventions.Count > 0;
            this.Items = interventions;
            this.CustomBarrier = customBarrier;
        }
    }
}
