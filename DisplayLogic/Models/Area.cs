using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DisplayLogic.Models
{
    public class Area
    {
        public string area_id { get; set; } = string.Empty; // Unique ID, e.g., master_bedroom
        public string? floor_id { get; set; }               // Optional floor grouping
        public string? icon { get; set; }                   // Optional icon name for UI
        public string name { get; set; } = string.Empty;    // User-defined name
    }
}
