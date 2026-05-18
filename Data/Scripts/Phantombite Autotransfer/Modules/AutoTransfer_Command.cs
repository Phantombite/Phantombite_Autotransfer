using System;
using System.Collections.Generic;
using System.Text;
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
        private const ushort NOTIFY_PACKET_ID      = 19509;   // Netzwerkpaket: Server → Client
        private const string MOD_NAME              = "Phantombite_AutoTransfer";
        private const string SENDER                = "[AT]";
        private const string VERSION               = "1.2.0";

        private bool _initialized = false;

        // ── Log-Level (vom Core gesetzt) ──────────────────────────────────────
        private enum LogLevel { Normal = 0, Debug = 1, Trace = 2 }
        private LogLevel _logLevel = LogLevel.Normal;

        // ── Performance-Level (vom Core gesetzt) ─────────────────────────────
        /// <summary>0=voll, 1=2x Intervall, 2=nur aktive Zonen, 3=reduzierter Batch</summary>
        public int PerfLevel { get; private set; } = 0;

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
            // Netzwerkpaket-Handler — läuft auf Client UND Server
            MyAPIGateway.Multiplayer.RegisterMessageHandler(NOTIFY_PACKET_ID, OnNotifyPacketReceived);
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
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(NOTIFY_PACKET_ID, OnNotifyPacketReceived);
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
                    int lvl;
                    if (int.TryParse(msg.Substring(9), out lvl))
                        _logLevel = (LogLevel)Math.Min(lvl, 2);
                    else
                    {
                        string s = msg.Substring(9).ToLower();
                        _logLevel = s == "trace" ? LogLevel.Trace : s == "debug" ? LogLevel.Debug : LogLevel.Normal;
                    }
                    Log("AutoTransfer_Command", "LOGLEVEL gesetzt: " + (int)_logLevel, 1);
                    return;
                }

                if (msg.StartsWith("PERFLEVEL|"))
                {
                    int lvl;
                    if (int.TryParse(msg.Substring(10), out lvl))
                    {
                        PerfLevel = Math.Max(0, Math.Min(3, lvl));
                        Log("AutoTransfer_Command", "PERFLEVEL gesetzt: " + PerfLevel, 1);
                        MyAPIGateway.Utilities.SendModMessage(CORE_CHANNEL, "PERFACK|autotrans|" + PerfLevel);
                    }
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
                    + "|" + VERSION
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

                Log("AutoTransfer_Command", "Command empfangen: " + command + " — SteamId: " + steamId, 1);

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
                string status     = executed ? "ok" : "error";
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
        /// Auf Dedicated Server: Netzwerkpaket an den Client.
        /// Auf lokaler Maschine (SP / Listen-Server-Host): ShowMessage direkt.
        /// </summary>
        public void SendMessage(IMyPlayer player, string message)
        {
            try
            {
                if (player == null) return;

                // Lokaler Spieler (SP oder Listen-Server-Host) → direkt anzeigen
                var localPlayer = MyAPIGateway.Session.LocalHumanPlayer;
                if (localPlayer != null && localPlayer.SteamUserId == player.SteamUserId)
                {
                    MyAPIGateway.Utilities.ShowMessage(SENDER, message);
                    return;
                }

                // Dedicated Server → Netzwerkpaket an Remote-Client
                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    MyAPIGateway.Multiplayer.SendMessageTo(NOTIFY_PACKET_ID, data, player.SteamUserId);
                }
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command ERROR in SendMessage: " + ex.Message);
            }
        }

        /// <summary>
        /// Empfängt Netzwerkpaket vom Server — läuft auf dem Client.
        /// Zeigt die Nachricht im Chat an.
        /// </summary>
        private void OnNotifyPacketReceived(byte[] data)
        {
            try
            {
                if (data == null || data.Length == 0) return;
                string message = Encoding.UTF8.GetString(data);
                if (message.StartsWith("NOTIFY:"))
                    MyAPIGateway.Utilities.ShowNotification(message.Substring(7), 3000, MyFontEnum.Green);
                else
                    MyAPIGateway.Utilities.ShowMessage(SENDER, message);
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] AutoTransfer_Command ERROR in OnNotifyPacketReceived: " + ex.Message);
            }
        }

        /// <summary>
        /// Sendet eine HUD-Notification an einen Spieler (kurze Einblendung oben).
        /// </summary>
        public void SendNotification(IMyPlayer player, string message)
        {
            try
            {
                if (player == null) return;

                var localPlayer = MyAPIGateway.Session.LocalHumanPlayer;
                if (localPlayer != null && localPlayer.SteamUserId == player.SteamUserId)
                {
                    MyAPIGateway.Utilities.ShowNotification(message, 3000, MyFontEnum.Green);
                    return;
                }

                if (MyAPIGateway.Multiplayer.IsServer)
                {
                    byte[] data = Encoding.UTF8.GetBytes("NOTIFY:" + message);
                    MyAPIGateway.Multiplayer.SendMessageTo(NOTIFY_PACKET_ID, data, player.SteamUserId);
                }
            }
            catch { }
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

        public void Warn(string module, string message)
        {
            MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] [WARN] [" + module + "] " + message);
            SendLog("WARN", module, message);
        }

        public void Error(string module, string message)
        {
            MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] [ERROR] [" + module + "] " + message);
            SendLog("ERROR", module, message);
        }

        public void Log(string module, string message, int level = 0)
        {
            if (level > 0 && (int)_logLevel < level) return;
            MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] [" + level + "] [" + module + "] " + message);
            SendLog(level.ToString(), module, message);
        }

        // ── HEAVY Signale an Core ─────────────────────────────────────────────
        public void HeavyStart(string op) { try { MyAPIGateway.Utilities.SendModMessage(CORE_CHANNEL, "HEAVY_START|autotrans|" + op); } catch { } }
        public void HeavyEnd(string op)   { try { MyAPIGateway.Utilities.SendModMessage(CORE_CHANNEL, "HEAVY_END|autotrans|" + op);   } catch { } }

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