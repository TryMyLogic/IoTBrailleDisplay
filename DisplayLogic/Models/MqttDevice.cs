namespace DisplayLogic.Models
{
    // Based off - https://developers.home-assistant.io/docs/device_registry_index/
    public record MqttDevice
    {
        public string? area_id { get; set; } // The Area which the device is placed in. Interact with area_registry and assign devices using WebSockets
        // public List<string> config_entries { get; set; } = []; // Managed internally by HA. Useful for tracking information but not required for this app.
        // public Dictionary<string, List<string?>>? config_entries_subentries { get; set; } // Managed internally by HA. Useful for tracking information but not required for this app.
        // public string[][] connections { get; set; } = []; // List of connections in key-value pairs ["type", "identifier"]. E.g ["mac", "00:11:22:33:44:55"]. Not required for this app 
        // public string? configuration_url { get; set; } // Links to the device’s web interface or configuration page, e.g Routers with Admin Portal. Primarily for advanced users and not required for this app.
        public required string default_manufacturer { get; set; } // Manufacturer fallback if manufacturer is not set
        public required string default_model { get; set; } // Model fallback if model is not set
        public required string default_name { get; set; } // Default device name fallback if name is not set

        // public string? disabled_by { get; set; } //  indicates why or how a device is disabled. Part of WS response but not in HA documentation. Unsure of possible values and it is not required.
        // public double? created_at { get; set; } // timestamp when the device was first registered. Part of WS response but not in HA documentation. Not required for this app.
        // public string? entry_type { get; set; } // Primarily used internally by HA or for filtering devices. Not required for this app.
        public string? hw_version { get; set; } // Hardware version
        public required string id { get; set; } // Unique HA generated ID
        public string[][] identifiers { get; set; } = []; // Set of (DOMAIN, identifier) tuples that identify the device in the outside world. E.g  ["mqtt", "shelly_switch_001"]
        public string? name { get; set; } // Device name. 
        public string? name_by_user { get; set; } // If null, UI defaults to name
        public required string manufacturer { get; set; } // Who created the device.
        // public double? modified_at { get; set; } // timestamp when the device was last modified. Part of WS response but not in HA documentation. Not required for this app.
        public required string model { get; set; } // Device model
        public string? model_id { get; set; } // Manufacturers unique model identifier. Display instead of model, else use model if null
        // public string? primary_config_entry { get; set; } // ID of the main configuration entry (integration) that owns or manages this device. Not required for this app.
        public string? serial_number { get; set; } // Device identifier. Unlike identifiers, it may not necessarily be unique across devices

        public string? sw_version { get; set; } // Software version
        // public List<string> labels { get; set; } = []; // User-defined or system-generated tags. Not used in this app, but useful for filtering devices in the UI if ever required.
        public string? via_device_id { get; set; } // Identifier of a device that routes messages between this device and Home Assistant.
    }
}
