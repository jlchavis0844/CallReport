using System;

namespace ContactReport {
    public class Call {
        public int id { get; set; }
        public int user_id { get; set; }
        public DateTime made_at { get; set; }
        public DateTime updated_at { get; set; }
        public string name { get; set; }
        public string resource_type { get; set; }
        public string outcome { get; set; }
        public int duration { get; set; }
        public bool incoming { get; set; }
        public bool missed { get; set; }

        public Call(int id, int user_id, DateTime made_at, DateTime updated_at, string name, string resource_type,
           string outcome, int duration, bool incoming, bool missed) {
            this.id = id;
            this.user_id = user_id;
            this.made_at = made_at;
            this.updated_at = updated_at;
            this.name = name;
            this.resource_type = resource_type;
            this.outcome = outcome;
            this.duration = duration;
            this.incoming = incoming;
            this.missed = missed;
        }
    }
}