using System;
using System.Collections.Generic;
using System.Text;
using VRage.Utils;

namespace PhantombiteAutoTransfer.Core
{
    public class ModuleManager
    {
        private readonly List<IModule> _modules = new List<IModule>();
        private readonly Dictionary<string, int> _crashCounts = new Dictionary<string, int>();

        public void RegisterModule(IModule module)
        {
            _modules.Add(module);
            _crashCounts[module.ModuleName] = 0;
            MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] ModuleManager: Registered '" + module.ModuleName + "'");
        }

        public void InitAll()
        {
            MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] ModuleManager: Initializing " + _modules.Count + " modules...");
            foreach (var module in _modules)
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    module.Init();
                    sw.Stop();
                    MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] ModuleManager: '" + module.ModuleName + "' initialized in " + sw.Elapsed.TotalMilliseconds.ToString("F2") + "ms");
                }
                catch (Exception ex)
                {
                    _crashCounts[module.ModuleName]++;
                    MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] ModuleManager: ERROR initializing '" + module.ModuleName + "':\n" + ex);
                }
            }
        }

        public void UpdateAll()
        {
            foreach (var module in _modules)
            {
                try { module.Update(); }
                catch (Exception ex)
                {
                    _crashCounts[module.ModuleName]++;
                    MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] ModuleManager: ERROR in Update '" + module.ModuleName + "':\n" + ex);
                }
            }
        }

        public void SaveAll()
        {
            foreach (var module in _modules)
            {
                try { module.SaveData(); }
                catch (Exception ex)
                {
                    MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] ModuleManager: ERROR in SaveData '" + module.ModuleName + "':\n" + ex);
                }
            }
        }

        public void CloseAll()
        {
            for (int i = _modules.Count - 1; i >= 0; i--)
            {
                try { _modules[i].Close(); }
                catch (Exception ex)
                {
                    MyLog.Default.WriteLineAndConsole("[PhantombiteAutoTransfer] ModuleManager: ERROR in Close '" + _modules[i].ModuleName + "':\n" + ex);
                }
            }
            _modules.Clear();
        }

        public string GetStatus()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PhantombiteAutoTransfer] ModuleManager Status:");
            foreach (var module in _modules)
                sb.AppendLine("  - " + module.ModuleName + ": ACTIVE (Crashes: " + _crashCounts[module.ModuleName] + ")");
            return sb.ToString();
        }
    }
}
