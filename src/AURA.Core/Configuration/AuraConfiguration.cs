namespace AURA.Core.Configuration
{
    /// <summary>
    /// Root application settings, persisted to config/settings.json.
    /// </summary>
    public class AuraConfiguration
    {
        public bool Internet { get; set; }

        public bool FirstRunCompleted { get; set; }

        public string Theme { get; set; }

        public AuraConfiguration()
        {
            Internet = true;
            FirstRunCompleted = false;
            Theme = "Light";
        }
    }
}
