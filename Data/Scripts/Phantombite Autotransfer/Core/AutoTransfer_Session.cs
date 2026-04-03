using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using PhantombiteAutoTransfer.Core;
using PhantombiteAutoTransfer.Modules;

namespace PhantombiteAutoTransfer
{
    /// <summary>
    /// Haupt-Session für Phantombite AutoTransfer.
    ///
    /// Ladereihenfolge:
    /// - AutoTransfer_FileManager — Config laden
    /// - AutoTransfer_Command     — Kanal 1995009 registrieren, READY empfangen
    /// - AutoTransfer_Main        — Blöcke scannen, Zonen verwalten
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class PhantombiteAutoTransferSession : MySessionComponentBase
    {
        private ModuleManager          _moduleManager;
        private AutoTransferCommand    _commandModule;
        private AutoTransferFileManager _fileManager;
        private AutoTransferMain       _mainModule;

        private bool _isInitialized = false;
        private const string MOD_NAME = "PhantombiteAutoTransfer";

        public override void LoadData()
        {
            try
            {
                MyLog.Default.WriteLineAndConsole("[" + MOD_NAME + "] Session LoadData gestartet...");

                _moduleManager  = new ModuleManager();
                _fileManager    = new AutoTransferFileManager();
                _commandModule  = new AutoTransferCommand();
                _mainModule     = new AutoTransferMain(_commandModule, _fileManager);

                // Module verknüpfen
                _commandModule.SetMainModule(_mainModule);
                _commandModule.SetFileManager(_fileManager);
                _mainModule.SetLogger(_commandModule);

                // Reihenfolge: FileManager zuerst (Config), dann Command (Kanal), dann Main (Blöcke)
                _moduleManager.RegisterModule(_fileManager);
                _moduleManager.RegisterModule(_commandModule);
                _moduleManager.RegisterModule(_mainModule);

                _moduleManager.InitAll();

                _isInitialized = true;
                MyLog.Default.WriteLineAndConsole("[" + MOD_NAME + "] Session LoadData abgeschlossen.");
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[" + MOD_NAME + "] KRITISCHER FEHLER in LoadData:\n" + ex);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (!_isInitialized) return;
            try { _moduleManager.UpdateAll(); }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[" + MOD_NAME + "] FEHLER in UpdateBeforeSimulation:\n" + ex);
            }
        }

        public override void SaveData()
        {
            if (!_isInitialized) return;
            try { _moduleManager.SaveAll(); }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[" + MOD_NAME + "] FEHLER in SaveData:\n" + ex);
            }
        }

        protected override void UnloadData()
        {
            try
            {
                MyLog.Default.WriteLineAndConsole("[" + MOD_NAME + "] Session UnloadData gestartet...");
                _moduleManager?.CloseAll();
                _isInitialized = false;
                MyLog.Default.WriteLineAndConsole("[" + MOD_NAME + "] Session UnloadData abgeschlossen.");
            }
            catch (Exception ex)
            {
                MyLog.Default.WriteLineAndConsole("[" + MOD_NAME + "] FEHLER in UnloadData:\n" + ex);
            }
        }
    }
}