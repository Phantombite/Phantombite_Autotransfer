using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using VRage.Utils;
using PhantombiteAutoTransfer.Core;

namespace PhantombiteAutoTransfer.Modules
{
    /// <summary>
    /// AutoTransfer_FileManager
    ///
    /// Verwaltet AutoTransfer_Config.ini im WorldStorage.
    /// Enthält die KeepList — Items die beim Transfer IN im Spieler-Inventar bleiben.
    ///
    /// Storage: WorldStorage (gleiches Verzeichnis wie Economy Config)
    /// Server-only.
    /// </summary>
    public class AutoTransferFileManager : IModule
    {
        public string ModuleName { get { return "AutoTransfer_FileManager"; } }

        private const string CONFIG_FILE = "AutoTransfer_Config.ini";
        private const string MOD_TAG     = "[PhantombiteAutoTransfer]";

        private bool _initialized = false;

        // ── IModule ───────────────────────────────────────────────────────────

        public void Init()
        {
            if (!MyAPIGateway.Multiplayer.IsServer) return;

            try
            {
                if (!FileExists(CONFIG_FILE))
                    DeployConfig();

                _initialized = true;
                MyLog.Default.WriteLineAndConsole(MOD_TAG + " AutoTransfer_FileManager: Initialized");
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole(MOD_TAG + " AutoTransfer_FileManager ERROR in Init:\n" + ex);
            }
        }

        public void Update()   { }
        public void SaveData() { }

        public void Close()
        {
            _initialized = false;
        }

        // ── Config Deploy ─────────────────────────────────────────────────────

        private void DeployConfig()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# ==============================================================================");
            sb.AppendLine("# AutoTransfer Config - Phantombite AutoTransfer");
            sb.AppendLine("# ==============================================================================");
            sb.AppendLine();
            sb.AppendLine("[AutoTransfer_KeepList]");
            sb.AppendLine("# Items die IMMER im Spieler-Inventar bleiben beim AutoTransfer IN");
            sb.AppendLine("# Format: SubtypeId=Mindestmenge");
            sb.AppendLine();
            sb.AppendLine("# --- Werkzeuge ---");
            sb.AppendLine("WelderItem=1");
            sb.AppendLine("Welder2Item=1");
            sb.AppendLine("Welder3Item=1");
            sb.AppendLine("Welder4Item=1");
            sb.AppendLine("AngleGrinderItem=1");
            sb.AppendLine("AngleGrinder2Item=1");
            sb.AppendLine("AngleGrinder3Item=1");
            sb.AppendLine("AngleGrinder4Item=1");
            sb.AppendLine("HandDrillItem=1");
            sb.AppendLine("HandDrill2Item=1");
            sb.AppendLine("HandDrill3Item=1");
            sb.AppendLine("HandDrill4Item=1");
            sb.AppendLine("FlareGunItem=1");
            sb.AppendLine();
            sb.AppendLine("# --- Handfeuerwaffen ---");
            sb.AppendLine("SemiAutoPistolItem=1");
            sb.AppendLine("FullAutoPistolItem=1");
            sb.AppendLine("ElitePistolItem=1");
            sb.AppendLine("AutomaticRifleItem=1");
            sb.AppendLine("PreciseAutomaticRifleItem=1");
            sb.AppendLine("RapidFireAutomaticRifleItem=1");
            sb.AppendLine("UltimateAutomaticRifleItem=1");
            sb.AppendLine("BasicHandHeldLauncherItem=1");
            sb.AppendLine("AdvancedHandHeldLauncherItem=1");
            sb.AppendLine();
            sb.AppendLine("# --- Munition (10x) ---");
            sb.AppendLine("SemiAutoPistolMagazine=10");
            sb.AppendLine("FullAutoPistolMagazine=10");
            sb.AppendLine("ElitePistolMagazine=10");
            sb.AppendLine("AutomaticRifleGun_Mag_20rd=10");
            sb.AppendLine("PreciseAutomaticRifleGun_Mag_5rd=10");
            sb.AppendLine("RapidFireAutomaticRifleGun_Mag_50rd=10");
            sb.AppendLine("UltimateAutomaticRifleGun_Mag_30rd=10");
            sb.AppendLine("FlareClip=10");
            sb.AppendLine();
            sb.AppendLine("# --- Raketen (5x) ---");
            sb.AppendLine("Missile200mm=5");
            sb.AppendLine();
            sb.AppendLine("# --- Consumables ---");
            sb.AppendLine("Medkit=2");
            sb.AppendLine("Powerkit=1");
            sb.AppendLine("RadiationKit=1");

            WriteFile(CONFIG_FILE, sb.ToString());
            MyLog.Default.WriteLineAndConsole(MOD_TAG + " AutoTransfer_FileManager: Config erstellt: " + CONFIG_FILE);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Liest die KeepList aus der Config.
        /// Key = SubtypeId, Value = Mindestmenge
        /// </summary>
        public Dictionary<string, int> GetKeepList()
        {
            var result = new Dictionary<string, int>();

            try
            {
                if (!MyAPIGateway.Multiplayer.IsServer) return result;

                string content = ReadFile(CONFIG_FILE);
                if (string.IsNullOrWhiteSpace(content)) return result;

                bool inSection = false;

                foreach (var rawLine in content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string trimmed = rawLine.Trim();
                    if (trimmed.StartsWith("#") || string.IsNullOrWhiteSpace(trimmed)) continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        inSection = (trimmed == "[AutoTransfer_KeepList]");
                        continue;
                    }

                    if (!inSection) continue;

                    int eq = trimmed.IndexOf('=');
                    if (eq <= 0) continue;

                    string key = trimmed.Substring(0, eq).Trim();
                    string val = trimmed.Substring(eq + 1).Trim();

                    int amount;
                    if (int.TryParse(val, out amount) && amount > 0)
                        result[key] = amount;
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole(MOD_TAG + " AutoTransfer_FileManager ERROR in GetKeepList:\n" + ex);
            }

            return result;
        }

        /// <summary>Gibt den rohen Config-Inhalt zurück.</summary>
        public string GetConfigRaw()
        {
            try
            {
                if (!FileExists(CONFIG_FILE)) DeployConfig();
                return ReadFile(CONFIG_FILE) ?? "";
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole(MOD_TAG + " AutoTransfer_FileManager ERROR in GetConfigRaw:\n" + ex);
                return "";
            }
        }

        // ── Low-Level File I/O ────────────────────────────────────────────────

        public string ReadFile(string filename)
        {
            try
            {
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(filename, typeof(AutoTransferFileManager)))
                {
                    using (var reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(filename, typeof(AutoTransferFileManager)))
                        return reader.ReadToEnd();
                }
                return null;
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole(MOD_TAG + " AutoTransfer_FileManager ERROR reading '" + filename + "': " + ex.Message);
                return null;
            }
        }

        public bool WriteFile(string filename, string content)
        {
            try
            {
                using (var writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(filename, typeof(AutoTransferFileManager)))
                {
                    writer.Write(content);
                }
                return true;
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole(MOD_TAG + " AutoTransfer_FileManager ERROR writing '" + filename + "': " + ex.Message);
                return false;
            }
        }

        public bool FileExists(string filename)
        {
            try
            {
                return MyAPIGateway.Utilities.FileExistsInWorldStorage(filename, typeof(AutoTransferFileManager));
            }
            catch
            {
                return false;
            }
        }
    }
}