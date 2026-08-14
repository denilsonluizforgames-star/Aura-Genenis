using System;

namespace AURA.Core.Configuration
{
    /// <summary>
    /// Tracks which optional capability modules the user has chosen to
    /// download and apply, persisted to config/modules.json.
    /// </summary>
    public class ModulesConfiguration
    {
        public ModuleFlags Modules { get; set; }

        public ModulesConfiguration()
        {
            Modules = new ModuleFlags();
        }
    }

    /// <summary>
    /// Flags persistentes dos módulos. Módulos do núcleo (browser/modules) não
    /// aparecem aqui: são sempre ativos por definição.
    /// </summary>
    public class ModuleFlags
    {
        // Módulos baixáveis do app
        public bool System { get; set; }
        public bool AI { get; set; }
        public bool Memory { get; set; }
        public bool Executors { get; set; }
        public bool Terminal { get; set; }
        public bool Cells { get; set; }
        public bool Logs { get; set; }

        // Futuros / Windows
        public bool Windows { get; set; }
        public bool Automation { get; set; }
        public bool Plugins { get; set; }

        public bool IsEnabled(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            switch (id.ToLowerInvariant())
            {
                case "system": return System;
                case "ai": return AI;
                case "memory": return Memory;
                case "executors": return Executors;
                case "terminal": return Terminal;
                case "cells": return Cells;
                case "logs": return Logs;
                case "windows": return Windows;
                case "automation": return Automation;
                case "plugins": return Plugins;
                default: return false;
            }
        }

        public void SetEnabled(string id, bool value)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            switch (id.ToLowerInvariant())
            {
                case "system": System = value; break;
                case "ai": AI = value; break;
                case "memory": Memory = value; break;
                case "executors": Executors = value; break;
                case "terminal": Terminal = value; break;
                case "cells": Cells = value; break;
                case "logs": Logs = value; break;
                case "windows": Windows = value; break;
                case "automation": Automation = value; break;
                case "plugins": Plugins = value; break;
            }
        }
    }
}
