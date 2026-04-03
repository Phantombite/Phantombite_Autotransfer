using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using PhantombiteAutoTransfer.Core;

namespace PhantombiteAutoTransfer.Modules
{
    /// <summary>
    /// AutoTransfer_Command
    ///
    /// Registriert AutoTransfer beim PhantomBite Core ueber Messaging.
    /// Empfaengt Commands vom Core und leitet sie an AutoTransfer_Main weiter.
    /// Stellt Log-API und Hilfsmethoden fuer alle AutoTransfer-Module bereit.
    ///
    /// Kanaele:
    ///   1995000  → Core empfaengt Registrierung + CMDRESULT
    ///   1995009  → AutoTransfer empfaengt READY / LOGLEVEL / CMD
    ///   1995999  → Log-Kanal
    /// </summary>
    public class AutoTransferCommand : IModule
    {
        public string ModuleName { get { return "AutoTransfer_Command"; } }

        private const long   CORE_CHANNEL         = 1995000L;
        private const long   AUTOTRANSFER_CHANNEL  = 1995009L;
        private const long   LOG_CHANNEL           = 1995999L;
        private const string MOD_NAME              = "Phantombite_AutoTransfer";
        private const string SENDER                = "[AT]";

        private bool _initialized = false;

        // ── Log-Level (vom Core gesetzt) ──────────────────────────────────────
        private enum LogLevel { Normal = 0, Debug = 1, Trace = 2 }
        private LogLevel _logLevel = LogLevel.Normal;

        // ── Referenz auf Main (wird von Session gesetzt) ───────────────────────
        private AutoTransferMain _mainModule;
        private AutoTransferFileManager _fileManager;

        public void SetMainModule(AutoTransferMain main)           { _mainModule   = main; }
        public void SetFileManager(AutoTransferFileManager fm)     { _fileManager  = fm;   }

        // ── IModule ───────────────────────────────────────────────────────────

        public void Init()
        {
            if (_initialized) return;
            MyAPIGateway.Utilities.RegisterMessageHandler(AUTOTRANSFER_CHANNEL, OnMessageReceived);
            _initialized = true;
            MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command: Initialized — warte auf Core READY");
        }

        public void Update()   { }
        public void SaveData() { }

        public void Close()
        {
            if (!_initialized) return;
            if (MyAPIGateway.Utilities != null)
                MyAPIGateway.Utilities.UnregisterMessageHandler(AUTOTRANSFER_CHANNEL, OnMessageReceived);
            _initialized = false;
        }

        // ── Nachrichten vom Core ──────────────────────────────────────────────

        private void OnMessageReceived(object data)
        {
            try
            {
                string msg = data as string;
                if (string.IsNullOrEmpty(msg)) return;

                if (msg == "READY")
                {
                    MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command: Core READY empfangen");
                    RegisterWithCore();
                    return;
                }

                if (msg.StartsWith("LOGLEVEL|"))
                {
                    string levelStr = msg.Substring(9).ToLower();
                    _logLevel = levelStr == "trace" ? LogLevel.Trace
                              : levelStr == "debug" ? LogLevel.Debug
                              : LogLevel.Normal;
                    MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command: LogLevel gesetzt: " + _logLevel);
                    return;
                }

                if (msg.StartsWith("CMD|"))
                {
                    OnCommandReceived(msg);
                    return;
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command ERROR in OnMessageReceived: " + ex.Message);
            }
        }

        // ── Registrierung beim Core ───────────────────────────────────────────

        private void RegisterWithCore()
        {
            try
            {
                string msg = "REGISTER"
                    + "|autotrans"
                    + "|AutoTransfer Ladezonen"
                    + "|" + AUTOTRANSFER_CHANNEL
                    + "|in:0:Transfer starten (Spieler/Schiff -> Container)"
                    + "|out:0:Transfer starten (Container -> Spieler/Schiff)"
                    + "|stop:0:Transfer stoppen"
                    + "|list:0:Container-Inhalt anzeigen"
                    + "|scan:1:Zonen neu einlesen"
                    + "|report:1:Zonenstatus anzeigen"
                    + "|log:1:Transfer-Log ein/ausschalten";

                MyAPIGateway.Utilities.SendModMessage(CORE_CHANNEL, msg);
                MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command: Registrierung an Core gesendet");
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command ERROR bei Registrierung: " + ex.Message);
            }
        }

        // ── Command empfangen und ausfuehren ──────────────────────────────────

        private void OnCommandReceived(string msg)
        {
            try
            {
                // Format: CMD|command|arg1|arg2|...|STEAM:steamId
                string[] parts = msg.Split('|');
                if (parts.Length < 2) return;

                string command = parts[1].ToLower();

                // SteamId aus letztem Argument extrahieren
                ulong steamId = 0;
                int   argEnd  = parts.Length;
                if (parts[parts.Length - 1].StartsWith("STEAM:"))
                {
                    ulong.TryParse(parts[parts.Length - 1].Substring(6), out steamId);
                    argEnd = parts.Length - 1;
                }

                // Args nach dem Command sammeln (Index 2 bis argEnd)
                string[] args = new string[argEnd - 2];
                Array.Copy(parts, 2, args, 0, args.Length);

                Debug("AutoTransfer_Command", "Command empfangen: " + command + " — SteamId: " + steamId);

                IMyPlayer player = FindPlayer(steamId);

                bool   executed  = false;
                string resultMsg = "";

                if (_mainModule == null)
                {
                    resultMsg = "AutoTransfer: Main-Modul nicht bereit.";
                }
                else
                {
                    // Alle args inkl. command zusammenbauen wie HandleCommand es erwartet
                    // HandleCommand erwartet args[0] = command, args[1..] = sub-args
                    string[] handleArgs = new string[args.Length + 1];
                    handleArgs[0] = command;
                    Array.Copy(args, 0, handleArgs, 1, args.Length);

                    resultMsg = _mainModule.HandleCommand(player, handleArgs);
                    executed  = !string.IsNullOrEmpty(resultMsg)
                                && !resultMsg.StartsWith("Fehler:")
                                && !resultMsg.StartsWith("Usage:")
                                && !resultMsg.StartsWith("Unbekannter");
                }

                // CMDRESULT zurueck an Core
                string argsJoined = string.Join("|", args);
                string status     = executed ? "ok" : "fail";
                string result     = "CMDRESULT|autotrans|" + command + "|" + argsJoined + "|" + steamId + "|" + status + "|" + resultMsg;
                MyAPIGateway.Utilities.SendModMessage(CORE_CHANNEL, result);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command ERROR in OnCommandReceived: " + ex.Message);
            }
        }

        // ── Public Hilfsmethoden (fuer Main + FileManager) ────────────────────

        /// <summary>
        /// Sendet eine Nachricht an einen Spieler im Chat.
        /// </summary>
        public void SendMessage(IMyPlayer player, string message)
        {
            try
            {
                if (player == null) return;
                MyAPIGateway.Utilities.ShowMessage(SENDER, message);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command ERROR in SendMessage: " + ex.Message);
            }
        }

        /// <summary>
        /// Prueft ob der Spieler Server-Admin ist.
        /// </summary>
        public bool IsAdmin(IMyPlayer player)
        {
            if (player == null) return false;
            return player.PromoteLevel >= MyPromoteLevel.Admin || MyAPIGateway.Multiplayer.IsServer;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private IMyPlayer FindPlayer(ulong steamId)
        {
            try
            {
                var players = new List<IMyPlayer>();
                MyAPIGateway.Players.GetPlayers(players);
                foreach (var p in players)
                    if (p.SteamUserId == steamId) return p;
            }
            catch { }
            return null;
        }

        // ── Log API (fuer alle AutoTransfer-Module) ───────────────────────────

        public void Warn(string module, string message)   { SendLog("WARN",  module, message); }
        public void Error(string module, string message)  { SendLog("ERROR", module, message); }

        public void Info(string module, string message)
        {
            if (_logLevel < LogLevel.Debug) return;
            SendLog("INFO", module, message);
        }

        public void Debug(string module, string message)
        {
            if (_logLevel < LogLevel.Debug) return;
            SendLog("DEBUG", module, message);
        }

        public void Trace(string module, string message)
        {
            if (_logLevel < LogLevel.Trace) return;
            SendLog("TRACE", module, message);
        }

        private void SendLog(string level, string module, string message)
        {
            try
            {
                MyAPIGateway.Utilities.SendModMessage(LOG_CHANNEL,
                    "LOG|" + MOD_NAME + "|" + level + "|" + module + "|" + message);
            }
            catch { }
        }
    }
}