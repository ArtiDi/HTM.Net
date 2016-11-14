﻿using HTM.Net.Network.Sensor;
using HTM.Net.Util;

namespace HTM.Net.Data
{
    public class FieldMetaInfo
    {
        public FieldMetaInfo(string name, FieldMetaType type, SensorFlags special)
        {
            this.name = name;
            this.type = type;
            this.special = special;
        }

        public string name { get;  }

        public FieldMetaType type { get; }
        public SensorFlags special { get;  }
    }

    public class AggregationSettings
    {
        public double years { get; set; }
        public double months { get; set; }
        public double weeks { get; set; }
        public double days { get; set; }
        public double hours { get; set; }
        public double minutes { get; set; }
        public double seconds { get; set; }
        public double milliseconds { get; set; }
        public double microseconds { get; set; }
        public Map<string, object> fields { get; set; }


    }
}