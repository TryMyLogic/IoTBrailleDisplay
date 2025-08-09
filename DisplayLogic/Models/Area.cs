namespace DisplayLogic.Models
{
    // Very little documentation available for area_registry on - https://developers.home-assistant.io/docs/area_registry_index . As such, the class
    // is based off the WebSocket result payload. 
    public class Area
    {
        public string area_id { get; set; } = string.Empty; // Unique ID of the area
        public string? floor_id { get; set; } // Optional ID of the floor this area is on. Not used in this app.
        // public string? humidity_entity_id { get; set; } // Optional entity ID for humidity sensor in this area. Not used in this app.
        public string? icon { get; set; } // Icon to represent the area in the UI. 
        public string name { get; set; } = string.Empty; // Area name
        // public string? picture { get; set; } // Optional background image for the area. Not used in this app.
        // public string? temperature_entity_id { get; set; } // Optional entity ID for temperature sensor in this area. Not used in this app.
        // public double created_at { get; set; } // timestamp when the area was first registered. Part of WS response but not in HA documentation. Not required for this app.
        // public double modified_at { get; set; } // timestamp when the area was last modified. Part of WS response but not in HA documentation. Not required for this app.
        // public List<string> aliases { get; set; } = []; // List of alternative names for the area. Not used in this app, but useful for filtering areas in the UI if ever required.
        // public List<string> labels { get; set; } = []; // User-defined or system-generated tags. Not used in this app, but useful for filtering devices in the UI if ever required.
    }
}
