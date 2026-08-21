using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyTitle("Android Display Voice Bridge")]
[assembly: AssemblyDescription("Use an Android smart-display voice remote as Windows text input")]
[assembly: AssemblyProduct("Android Display Voice Bridge")]
[assembly: AssemblyCopyright("Copyright (c) 2026 contributors")]
[assembly: AssemblyVersion("0.1.0.0")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

namespace AndroidDisplayVoiceBridge
{
    internal static class Program
    {
        private const string AppName = "Android Display Voice Bridge";
        private const string MutexName = "Local\\AndroidDisplayVoiceBridge.SingleInstance";

        private static readonly object Sync = new object();
        private static readonly Dictionary<string, DeviceRuntime> Devices =
            new Dictionary<string, DeviceRuntime>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<AdbStreamWorker> Workers = new List<AdbStreamWorker>();

        private static AppConfig config;
        private static string appDirectory;
        private static string configPath;
        private static string adbPath;
        private static string logPath;
        private static NotifyIcon tray;
        private static SynchronizationContext uiContext;
        private static bool enabled = true;
        private static bool exiting;

        [STAThread]
        private static void Main(string[] args)
        {
            if (HasArgument(args, "--self-test"))
            {
                Environment.ExitCode = RunSelfTest();
                return;
            }

            string validationPath = GetArgumentValue(args, "--validate-config");
            if (!String.IsNullOrEmpty(validationPath))
            {
                Environment.ExitCode = ValidateConfigFile(validationPath);
                return;
            }

            try
            {
                appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                configPath = GetArgumentValue(args, "--config");
                if (String.IsNullOrWhiteSpace(configPath))
                    configPath = Path.Combine(appDirectory, "config.json");
                else
                    configPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(configPath));

                config = LoadConfig(configPath);
                ValidateConfig(config, true);
                adbPath = ResolveAdbPath(config.AdbPath, appDirectory);

                string logDirectory = ResolveDirectory(config.LogDirectory, appDirectory);
                Directory.CreateDirectory(logDirectory);
                logPath = Path.Combine(logDirectory, "bridge.log");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "启动失败：" + ex.Message + Environment.NewLine + Environment.NewLine +
                    "请检查 config.json，或先运行 scripts\\diagnose-device.ps1。",
                    AppName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.ExitCode = 2;
                return;
            }

            bool created;
            using (var mutex = new Mutex(true, MutexName, out created))
            {
                if (!created)
                {
                    MessageBox.Show("程序已经在运行。请查看任务栏通知区域。", AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    uiContext = SynchronizationContext.Current ??
                        new WindowsFormsSynchronizationContext();
                    CreateTrayIcon();
                    InitializeDevices();
                    StartWorkers();
                    Log("Bridge started; config=" + configPath + "; devices=" + Devices.Count);
                    Application.Run();
                }
                catch (Exception ex)
                {
                    Log("Fatal error: " + ex);
                    MessageBox.Show("运行失败：" + ex.Message, AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.ExitCode = 3;
                }
                finally
                {
                    StopWorkers();
                }
            }
        }

        private static bool HasArgument(string[] args, string name)
        {
            foreach (string value in args)
                if (String.Equals(value, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetArgumentValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length - 1; i++)
                if (String.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            return null;
        }

        private static AppConfig LoadConfig(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException("找不到配置文件", path);

            string json = File.ReadAllText(path, Encoding.UTF8);
            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
            AppConfig value = serializer.Deserialize<AppConfig>(json);
            if (value == null) throw new InvalidDataException("配置文件内容为空");
            value.ApplyDefaults();
            return value;
        }

        private static int ValidateConfigFile(string path)
        {
            try
            {
                AppConfig value = LoadConfig(Path.GetFullPath(path));
                ValidateConfig(value, false);
                return 0;
            }
            catch
            {
                return 4;
            }
        }

        private static void ValidateConfig(AppConfig value, bool requireEnabledDevice)
        {
            if (value.Behavior.ArmTimeoutSeconds < 1 || value.Behavior.ArmTimeoutSeconds > 300)
                throw new InvalidDataException("behavior.armTimeoutSeconds 必须在 1 到 300 之间");
            if (value.Behavior.FinalTimeoutSeconds < 1 || value.Behavior.FinalTimeoutSeconds > 300)
                throw new InvalidDataException("behavior.finalTimeoutSeconds 必须在 1 到 300 之间");
            if (value.Behavior.ReconnectDelayMilliseconds < 500 ||
                value.Behavior.ReconnectDelayMilliseconds > 60000)
                throw new InvalidDataException("behavior.reconnectDelayMilliseconds 必须在 500 到 60000 之间");
            if (value.Behavior.DuplicateSuppressionMilliseconds < 0 ||
                value.Behavior.DuplicateSuppressionMilliseconds > 30000)
                throw new InvalidDataException("behavior.duplicateSuppressionMilliseconds 必须在 0 到 30000 之间");

            var serials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int enabledCount = 0;
            foreach (DeviceConfig device in value.Devices)
            {
                if (device == null) throw new InvalidDataException("devices 中存在空项目");
                device.ApplyDefaults();

                if (!IsSafeSerial(device.Serial))
                    throw new InvalidDataException("设备 " + device.Name + " 的 serial 格式不安全或无效");
                if (!serials.Add(device.Serial))
                    throw new InvalidDataException("设备 serial 重复：" + device.Serial);
                if (!Regex.IsMatch(device.InputDevice, @"^/dev/input/event[0-9]+$"))
                    throw new InvalidDataException("设备 " + device.Name + " 的 inputDevice 必须类似 /dev/input/event6");
                if (!Regex.IsMatch(device.KeyToken, @"^[A-Z0-9_]+$"))
                    throw new InvalidDataException("设备 " + device.Name + " 的 keyToken 格式无效");
                if (!Regex.IsMatch(device.LogcatFilter, @"^[A-Za-z0-9_=:.*?+\-]+$"))
                    throw new InvalidDataException("设备 " + device.Name + " 的 logcatFilter 格式无效");

                try
                {
                    new Regex(device.FinalAsrPattern,
                        RegexOptions.CultureInvariant | RegexOptions.Compiled);
                }
                catch (Exception ex)
                {
                    throw new InvalidDataException("设备 " + device.Name + " 的 finalAsrPattern 无效：" + ex.Message);
                }

                bool needsPackage = device.ManageVoicePackage ||
                    device.StopAssistantAfterFinal || device.StartServiceFallback;
                if (needsPackage && !IsSafePackageName(device.VoicePackage))
                    throw new InvalidDataException("设备 " + device.Name + " 的 voicePackage 格式无效");
                if (device.StartServiceFallback)
                {
                    if (!Regex.IsMatch(device.ServiceComponent, @"^[A-Za-z0-9_./]+$"))
                        throw new InvalidDataException("设备 " + device.Name + " 的 serviceComponent 格式无效");
                    if (!IsSafePackageName(device.KeyDownAction) || !IsSafePackageName(device.KeyUpAction))
                        throw new InvalidDataException("设备 " + device.Name + " 的语音按键 action 格式无效");
                    if (device.RemoteType < 0 || device.RemoteType > 1000)
                        throw new InvalidDataException("设备 " + device.Name + " 的 remoteType 超出范围");
                }

                if (device.Enabled) enabledCount++;
            }

            if (requireEnabledDevice && enabledCount == 0)
                throw new InvalidDataException("没有启用的显示器。请修改 devices[].enabled 和 serial");
        }

        private static bool IsSafeSerial(string value)
        {
            return !String.IsNullOrWhiteSpace(value) &&
                Regex.IsMatch(value, @"^[A-Za-z0-9._:%\[\]\-]+$");
        }

        private static bool IsSafePackageName(string value)
        {
            return !String.IsNullOrWhiteSpace(value) &&
                Regex.IsMatch(value, @"^[A-Za-z0-9_.]+$");
        }

        private static string ResolveAdbPath(string configuredPath, string baseDirectory)
        {
            string value = Environment.ExpandEnvironmentVariables(configuredPath ?? String.Empty);
            if (String.IsNullOrWhiteSpace(value)) value = "adb.exe";

            if (Path.IsPathRooted(value) && File.Exists(value))
                return Path.GetFullPath(value);

            string relative = Path.GetFullPath(Path.Combine(baseDirectory, value));
            if (File.Exists(relative)) return relative;

            string fileName = Path.GetFileName(value);
            string pathValue = Environment.GetEnvironmentVariable("PATH") ?? String.Empty;
            foreach (string entry in pathValue.Split(Path.PathSeparator))
            {
                if (String.IsNullOrWhiteSpace(entry)) continue;
                string candidate;
                try { candidate = Path.Combine(entry.Trim(), fileName); }
                catch { continue; }
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
            }

            throw new FileNotFoundException(
                "找不到 adb.exe。请运行安装脚本，或在 config.json 中设置 adbPath", value);
        }

        private static string ResolveDirectory(string configuredPath, string baseDirectory)
        {
            string value = Environment.ExpandEnvironmentVariables(configuredPath ?? String.Empty);
            if (String.IsNullOrWhiteSpace(value))
                value = Path.Combine(Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData), "AndroidDisplayVoiceBridge");
            if (!Path.IsPathRooted(value)) value = Path.Combine(baseDirectory, value);
            return Path.GetFullPath(value);
        }

        private static void CreateTrayIcon()
        {
            var menu = new ContextMenuStrip();
            var toggle = new ToolStripMenuItem("暂停遥控器语音输入");
            toggle.Click += delegate
            {
                enabled = !enabled;
                toggle.Text = enabled ? "暂停遥控器语音输入" : "恢复遥控器语音输入";
                tray.Text = enabled ? "显示器遥控器语音输入：运行中" : "显示器遥控器语音输入：已暂停";
                if (enabled)
                    SetManagedPackagesEnabled(true, false);
                else
                    DisablePackagesConfiguredForInactive(false);
                Log(enabled ? "Bridge resumed" : "Bridge paused");
            };
            menu.Items.Add(toggle);
            menu.Items.Add("重新连接显示器", null, delegate { ReconnectAllDevices(); });
            menu.Items.Add("打开配置文件", null, delegate { OpenWithNotepad(configPath); });
            menu.Items.Add("打开日志", null, delegate { OpenWithNotepad(logPath); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, delegate
            {
                exiting = true;
                tray.Visible = false;
                Application.Exit();
            });

            tray = new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Text = "显示器遥控器语音输入：运行中",
                ContextMenuStrip = menu,
                Visible = true
            };

            if (config.Behavior.ShowNotifications)
            {
                tray.ShowBalloonTip(3500, "遥控器语音输入已启动",
                    "按住遥控器麦克风键说话，松开后文字会写入 Windows 当前光标。",
                    ToolTipIcon.Info);
            }
        }

        private static void OpenWithNotepad(string path)
        {
            try { Process.Start("notepad.exe", QuoteArgument(path)); }
            catch (Exception ex) { Log("Could not open file: " + ex.Message); }
        }

        private static void InitializeDevices()
        {
            lock (Sync)
            {
                foreach (DeviceConfig device in config.Devices)
                {
                    if (!device.Enabled) continue;
                    Devices[device.Serial] = new DeviceRuntime(device);
                }
            }

            ReconnectAllDevices();
            SetManagedPackagesEnabled(true, false);
        }

        private static void ReconnectAllDevices()
        {
            foreach (DeviceRuntime runtime in SnapshotDevices())
            {
                if (!runtime.Config.AutoConnect || runtime.Config.Serial.IndexOf(':') < 0) continue;
                DeviceRuntime copy = runtime;
                ThreadPool.QueueUserWorkItem(delegate { ConnectNetworkDevice(copy); });
            }
        }

        private static void ConnectNetworkDevice(DeviceRuntime runtime)
        {
            int code = RunAdbGlobal("connect " + QuoteArgument(runtime.Config.Serial), 8000);
            Log(runtime.DisplayName + " adb connect exit=" + code);
            if (code == 0 && runtime.Config.KeepAndroidAwakeWhilePowered &&
                RunAdb(runtime.Config.Serial, "get-state", 3000) == 0)
            {
                int stayOnCode = RunAdb(runtime.Config.Serial,
                    "shell settings put global stay_on_while_plugged_in 7", 3000);
                int adbEnabledCode = RunAdb(runtime.Config.Serial,
                    "shell settings put global adb_enabled 1", 3000);
                Log(runtime.DisplayName + " keep-awake=" + stayOnCode +
                    "; adb-enabled=" + adbEnabledCode);
            }
        }

        private static void SetManagedPackagesEnabled(bool shouldEnable, bool synchronous)
        {
            foreach (DeviceRuntime runtime in SnapshotDevices())
            {
                if (!runtime.Config.ManageVoicePackage) continue;
                DeviceRuntime copy = runtime;
                WaitCallback action = delegate
                {
                    string command = shouldEnable
                        ? "shell pm enable --user 0 " + copy.Config.VoicePackage
                        : "shell pm disable-user --user 0 " + copy.Config.VoicePackage;
                    int code = RunAdb(copy.Config.Serial, command, 5000);
                    Log(copy.DisplayName + (shouldEnable ? " voice package enabled" :
                        " voice package disabled") + " exit=" + code);
                };

                if (synchronous) action(null);
                else ThreadPool.QueueUserWorkItem(action);
            }
        }

        private static void DisablePackagesConfiguredForInactive(bool synchronous)
        {
            foreach (DeviceRuntime runtime in SnapshotDevices())
            {
                if (!runtime.Config.ManageVoicePackage ||
                    !runtime.Config.DisableVoicePackageWhenInactive) continue;
                DeviceRuntime copy = runtime;
                WaitCallback action = delegate
                {
                    int code = RunAdb(copy.Config.Serial,
                        "shell pm disable-user --user 0 " + copy.Config.VoicePackage, 5000);
                    Log(copy.DisplayName + " voice package disabled while inactive exit=" + code);
                };
                if (synchronous) action(null);
                else ThreadPool.QueueUserWorkItem(action);
            }
        }

        private static List<DeviceRuntime> SnapshotDevices()
        {
            lock (Sync) return new List<DeviceRuntime>(Devices.Values);
        }

        private static void StartWorkers()
        {
            foreach (DeviceRuntime runtime in SnapshotDevices())
            {
                string inputArguments = "shell getevent -lt " + runtime.Config.InputDevice;
                string asrArguments = "logcat -v raw -T 1 " +
                    QuoteArgument(runtime.Config.LogcatFilter) + " " + QuoteArgument("*:S");

                var input = new AdbStreamWorker(runtime, inputArguments,
                    HandleInputLine, "input");
                var asr = new AdbStreamWorker(runtime, asrArguments,
                    HandleAsrLine, "asr");
                Workers.Add(input);
                Workers.Add(asr);
                input.Start();
                asr.Start();
            }
        }

        private static void StopWorkers()
        {
            if (exiting && Workers.Count == 0) return;
            exiting = true;
            foreach (AdbStreamWorker worker in Workers) worker.Stop();
            DisablePackagesConfiguredForInactive(true);
            if (tray != null) tray.Dispose();
            Workers.Clear();
            Log("Bridge stopped");
        }

        internal static bool IsExiting { get { return exiting; } }
        internal static string AdbExecutable { get { return adbPath; } }
        internal static int ReconnectDelayMilliseconds
        {
            get { return config.Behavior.ReconnectDelayMilliseconds; }
        }

        internal static void HandleInputLine(DeviceRuntime runtime, string line)
        {
            if (line.IndexOf("EV_KEY", StringComparison.OrdinalIgnoreCase) < 0 ||
                line.IndexOf(runtime.Config.KeyToken, StringComparison.OrdinalIgnoreCase) < 0)
                return;

            bool isDown = line.IndexOf("DOWN", StringComparison.OrdinalIgnoreCase) >= 0;
            bool isUp = line.IndexOf("UP", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isDown && !isUp) return;

            lock (Sync)
            {
                if (!enabled) return;
                DeviceState state = runtime.State;

                if (isDown)
                {
                    if (state.Pressed) return;
                    state.Pressed = true;
                    state.ArmedUntilUtc = DateTime.UtcNow.AddSeconds(
                        config.Behavior.ArmTimeoutSeconds);
                    state.ServiceFallbackStarted = false;
                    Log(runtime.DisplayName + " microphone DOWN; ASR armed");
                    if (runtime.Config.StartServiceFallback)
                        ScheduleServiceFallback(runtime, state);
                }
                else
                {
                    if (!state.Pressed) return;
                    state.Pressed = false;
                    state.ArmedUntilUtc = DateTime.UtcNow.AddSeconds(
                        config.Behavior.FinalTimeoutSeconds);
                    Log(runtime.DisplayName + " microphone UP; waiting for final ASR");
                    if (state.ServiceFallbackStarted)
                        SendVoiceServiceAction(runtime, runtime.Config.KeyUpAction, false);
                }
            }
        }

        private static void ScheduleServiceFallback(DeviceRuntime runtime, DeviceState expectedState)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                Thread.Sleep(350);
                lock (Sync)
                {
                    if (!enabled || exiting || !Object.ReferenceEquals(runtime.State, expectedState) ||
                        !expectedState.Pressed) return;
                }

                string pid = RunAdbCapture(runtime.Config.Serial,
                    "shell pidof " + runtime.Config.VoicePackage, 2500).Trim();
                if (pid.Length != 0) return;

                lock (Sync)
                {
                    if (!enabled || !expectedState.Pressed) return;
                    expectedState.ServiceFallbackStarted = true;
                }
                Log(runtime.DisplayName + " starting ASR service fallback");
                SendVoiceServiceAction(runtime, runtime.Config.KeyDownAction, true);
            });
        }

        private static void SendVoiceServiceAction(DeviceRuntime runtime, string action,
            bool includeRemoteType)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string command = "shell am startservice -n " + runtime.Config.ServiceComponent +
                    " -a " + action;
                if (includeRemoteType) command += " --ei rc " + runtime.Config.RemoteType;
                int code = RunAdb(runtime.Config.Serial, command, 4000);
                Log(runtime.DisplayName + " service action " + action + " exit=" + code);
            });
        }

        internal static void HandleAsrLine(DeviceRuntime runtime, string line)
        {
            Match match = runtime.FinalAsrPattern.Match(line);
            if (!match.Success) return;

            string text = GetMatchValue(match, "text", 1).Trim();
            string dialogId = GetMatchValue(match, "id", 2).Trim();
            if (text.Length == 0) return;
            if (dialogId.Length == 0) dialogId = line.GetHashCode().ToString("X8");

            lock (Sync)
            {
                if (!enabled) return;
                DeviceState state = runtime.State;
                if (!state.Pressed && DateTime.UtcNow > state.ArmedUntilUtc)
                {
                    Log(runtime.DisplayName + " ignored unarmed final ASR id=" + dialogId);
                    return;
                }
                if (!state.SeenDialogs.Add(dialogId)) return;
                state.DialogOrder.Enqueue(dialogId);
                while (state.DialogOrder.Count > 128)
                    state.SeenDialogs.Remove(state.DialogOrder.Dequeue());

                double elapsed = (DateTime.UtcNow - state.LastFinalUtc).TotalMilliseconds;
                if (String.Equals(state.LastText, text, StringComparison.Ordinal) && elapsed >= 0 &&
                    elapsed < config.Behavior.DuplicateSuppressionMilliseconds)
                {
                    Log(runtime.DisplayName + " ignored duplicate final ASR id=" + dialogId);
                    return;
                }
                state.LastText = text;
                state.LastFinalUtc = DateTime.UtcNow;
                state.ArmedUntilUtc = DateTime.MinValue;
            }

            InjectUnicodeText(text);
            string detail = config.Behavior.LogRecognizedText
                ? "; text=" + text
                : "; characters=" + text.Length;
            Log(runtime.DisplayName + " injected final ASR id=" + dialogId + detail);
            ShowRecognizedText(text);
            if (runtime.Config.StopAssistantAfterFinal) StopAssistantImmediately(runtime);
        }

        private static string GetMatchValue(Match match, string groupName, int fallbackIndex)
        {
            Group named = match.Groups[groupName];
            if (named != null && named.Success) return named.Value;
            if (match.Groups.Count > fallbackIndex && match.Groups[fallbackIndex].Success)
                return match.Groups[fallbackIndex].Value;
            return String.Empty;
        }

        private static void ShowRecognizedText(string text)
        {
            if (!config.Behavior.ShowNotifications || tray == null) return;
            try
            {
                uiContext.Post(delegate
                {
                    try
                    {
                        string preview = text.Length > 80 ? text.Substring(0, 80) + "…" : text;
                        tray.ShowBalloonTip(1800, "已输入", preview, ToolTipIcon.Info);
                    }
                    catch { }
                }, null);
            }
            catch { }
        }

        private static void StopAssistantImmediately(DeviceRuntime runtime)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                int code = RunAdb(runtime.Config.Serial,
                    "shell am force-stop " + runtime.Config.VoicePackage, 4000);
                Log(runtime.DisplayName + " assistant stopped after ASR exit=" + code);
            });
        }

        private static void InjectUnicodeText(string value)
        {
            if (String.IsNullOrEmpty(value)) return;

            var inputs = new List<INPUT>(value.Length * 2);
            foreach (char character in value)
            {
                inputs.Add(CreateUnicodeInput(character, false));
                inputs.Add(CreateUnicodeInput(character, true));
            }
            INPUT[] array = inputs.ToArray();
            uint sent = SendInput((uint)array.Length, array, Marshal.SizeOf(typeof(INPUT)));
            if (sent != array.Length)
                Log("SendInput incomplete: " + sent + "/" + array.Length +
                    "; win32=" + Marshal.GetLastWin32Error());
        }

        private static INPUT CreateUnicodeInput(char character, bool keyUp)
        {
            var input = new INPUT();
            input.type = INPUT_KEYBOARD;
            input.U.ki.wVk = 0;
            input.U.ki.wScan = character;
            input.U.ki.dwFlags = KEYEVENTF_UNICODE | (keyUp ? KEYEVENTF_KEYUP : 0);
            return input;
        }

        internal static void WorkerDisconnected(DeviceRuntime runtime, string kind)
        {
            Log(runtime.DisplayName + " " + kind + " listener disconnected");
        }

        internal static void TryReconnect(DeviceRuntime runtime)
        {
            if (runtime.Config.AutoConnect && runtime.Config.Serial.IndexOf(':') >= 0)
                ConnectNetworkDevice(runtime);
        }

        internal static void Log(string message)
        {
            if (String.IsNullOrEmpty(logPath)) return;
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(logPath,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ") + message +
                        Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        private static int RunAdb(string serial, string arguments, int timeoutMilliseconds)
        {
            return RunProcess(CreateAdbStartInfo(serial, arguments, false), timeoutMilliseconds);
        }

        private static int RunAdbGlobal(string arguments, int timeoutMilliseconds)
        {
            return RunProcess(CreateAdbStartInfo(null, arguments, false), timeoutMilliseconds);
        }

        private static int RunProcess(ProcessStartInfo info, int timeoutMilliseconds)
        {
            Process process = null;
            try
            {
                process = Process.Start(info);
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); } catch { }
                    return -2;
                }
                return process.ExitCode;
            }
            catch (Exception ex)
            {
                Log("adb error: " + ex.Message);
                return -1;
            }
            finally
            {
                if (process != null) process.Dispose();
            }
        }

        private static string RunAdbCapture(string serial, string arguments,
            int timeoutMilliseconds)
        {
            Process process = null;
            try
            {
                ProcessStartInfo info = CreateAdbStartInfo(serial, arguments, true);
                process = Process.Start(info);
                if (!process.WaitForExit(timeoutMilliseconds))
                {
                    try { process.Kill(); } catch { }
                    return String.Empty;
                }
                return process.StandardOutput.ReadToEnd();
            }
            catch { return String.Empty; }
            finally
            {
                if (process != null) process.Dispose();
            }
        }

        internal static ProcessStartInfo CreateAdbStartInfo(string serial, string arguments,
            bool redirectOutput)
        {
            string prefix = String.IsNullOrEmpty(serial)
                ? String.Empty
                : "-s " + QuoteArgument(serial) + " ";
            var info = new ProcessStartInfo
            {
                FileName = adbPath,
                Arguments = prefix + arguments,
                UseShellExecute = false,
                RedirectStandardOutput = redirectOutput,
                RedirectStandardError = redirectOutput,
                CreateNoWindow = true
            };
            if (redirectOutput)
            {
                info.StandardOutputEncoding = Encoding.UTF8;
                info.StandardErrorEncoding = Encoding.UTF8;
            }
            return info;
        }

        private static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private static int RunSelfTest()
        {
            try
            {
                var pattern = new Regex(
                    @"onAsrResult:\s*(?<text>.*?),\s*isFinal\s*=\s*true,.*?dialogId\s*=\s*(?<id>[^\s]+)",
                    RegexOptions.CultureInvariant);
                Match match = pattern.Match(
                    "onAsrResult: 第二次测试成功, isFinal = true, isNlpRequest = false, dialogId = test-001");
                if (!match.Success || match.Groups["text"].Value != "第二次测试成功" ||
                    match.Groups["id"].Value != "test-001") return 11;

                int expectedInputSize = IntPtr.Size == 8 ? 40 : 28;
                if (Marshal.SizeOf(typeof(INPUT)) != expectedInputSize) return 12;
                return 0;
            }
            catch
            {
                return 10;
            }
        }

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_UNICODE = 0x0004;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint inputCount, INPUT[] inputs, int inputSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint message;
            public ushort parameterLow;
            public ushort parameterHigh;
        }

        internal sealed class DeviceRuntime
        {
            public readonly DeviceConfig Config;
            public readonly DeviceState State = new DeviceState();
            public readonly Regex FinalAsrPattern;

            public DeviceRuntime(DeviceConfig configValue)
            {
                Config = configValue;
                FinalAsrPattern = new Regex(configValue.FinalAsrPattern,
                    RegexOptions.Compiled | RegexOptions.CultureInvariant);
            }

            public string DisplayName
            {
                get { return Config.Name + " [" + Config.Serial + "]"; }
            }
        }

        internal sealed class DeviceState
        {
            public bool Pressed;
            public bool ServiceFallbackStarted;
            public DateTime ArmedUntilUtc = DateTime.MinValue;
            public DateTime LastFinalUtc = DateTime.MinValue;
            public string LastText = String.Empty;
            public readonly HashSet<string> SeenDialogs =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public readonly Queue<string> DialogOrder = new Queue<string>();
        }

        private sealed class AdbStreamWorker
        {
            private readonly DeviceRuntime runtime;
            private readonly string arguments;
            private readonly Action<DeviceRuntime, string> lineHandler;
            private readonly string kind;
            private readonly Thread thread;
            private Process process;
            private volatile bool stopped;

            public AdbStreamWorker(DeviceRuntime runtimeValue, string argumentValue,
                Action<DeviceRuntime, string> handler, string workerKind)
            {
                runtime = runtimeValue;
                arguments = argumentValue;
                lineHandler = handler;
                kind = workerKind;
                thread = new Thread(Run)
                {
                    IsBackground = true,
                    Name = "ADB-" + kind + "-" + runtime.Config.Serial
                };
            }

            public void Start() { thread.Start(); }

            public void Stop()
            {
                stopped = true;
                try { if (process != null && !process.HasExited) process.Kill(); }
                catch { }
            }

            private void Run()
            {
                while (!stopped && !Program.IsExiting)
                {
                    try
                    {
                        if (kind == "input") Program.TryReconnect(runtime);
                        ProcessStartInfo info = Program.CreateAdbStartInfo(
                            runtime.Config.Serial, arguments, true);
                        process = Process.Start(info);
                        process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
                        {
                            if (!String.IsNullOrEmpty(e.Data) &&
                                e.Data.IndexOf("daemon started", StringComparison.OrdinalIgnoreCase) < 0)
                                Program.Log(runtime.DisplayName + " " + kind + " stderr: " + e.Data);
                        };
                        process.BeginErrorReadLine();
                        string line;
                        while (!stopped && (line = process.StandardOutput.ReadLine()) != null)
                            lineHandler(runtime, line);
                        try { process.WaitForExit(); } catch { }
                    }
                    catch (Exception ex)
                    {
                        Program.Log(runtime.DisplayName + " " + kind +
                            " listener error: " + ex.Message);
                    }
                    finally
                    {
                        Program.WorkerDisconnected(runtime, kind);
                        try { if (process != null) process.Dispose(); } catch { }
                        process = null;
                    }

                    if (!stopped && !Program.IsExiting)
                        Thread.Sleep(Program.ReconnectDelayMilliseconds);
                }
            }
        }
    }

    public sealed class AppConfig
    {
        public string AdbPath { get; set; }
        public string LogDirectory { get; set; }
        public BehaviorConfig Behavior { get; set; }
        public List<DeviceConfig> Devices { get; set; }

        public AppConfig()
        {
            AdbPath = "platform-tools\\adb.exe";
            LogDirectory = "%LOCALAPPDATA%\\AndroidDisplayVoiceBridge";
            Behavior = new BehaviorConfig();
            Devices = new List<DeviceConfig>();
        }

        public void ApplyDefaults()
        {
            if (String.IsNullOrWhiteSpace(AdbPath)) AdbPath = "platform-tools\\adb.exe";
            if (String.IsNullOrWhiteSpace(LogDirectory))
                LogDirectory = "%LOCALAPPDATA%\\AndroidDisplayVoiceBridge";
            if (Behavior == null) Behavior = new BehaviorConfig();
            Behavior.ApplyDefaults();
            if (Devices == null) Devices = new List<DeviceConfig>();
            foreach (DeviceConfig device in Devices)
                if (device != null) device.ApplyDefaults();
        }
    }

    public sealed class BehaviorConfig
    {
        public bool ShowNotifications { get; set; }
        public bool LogRecognizedText { get; set; }
        public int ArmTimeoutSeconds { get; set; }
        public int FinalTimeoutSeconds { get; set; }
        public int ReconnectDelayMilliseconds { get; set; }
        public int DuplicateSuppressionMilliseconds { get; set; }

        public BehaviorConfig()
        {
            ShowNotifications = true;
            LogRecognizedText = false;
            ArmTimeoutSeconds = 30;
            FinalTimeoutSeconds = 15;
            ReconnectDelayMilliseconds = 2500;
            DuplicateSuppressionMilliseconds = 1500;
        }

        public void ApplyDefaults()
        {
            if (ArmTimeoutSeconds == 0) ArmTimeoutSeconds = 30;
            if (FinalTimeoutSeconds == 0) FinalTimeoutSeconds = 15;
            if (ReconnectDelayMilliseconds == 0) ReconnectDelayMilliseconds = 2500;
            if (DuplicateSuppressionMilliseconds < 0)
                DuplicateSuppressionMilliseconds = 1500;
        }
    }

    public sealed class DeviceConfig
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Serial { get; set; }
        public bool AutoConnect { get; set; }
        public bool KeepAndroidAwakeWhilePowered { get; set; }
        public string InputDevice { get; set; }
        public string KeyToken { get; set; }
        public string LogcatFilter { get; set; }
        public string FinalAsrPattern { get; set; }
        public string VoicePackage { get; set; }
        public bool ManageVoicePackage { get; set; }
        public bool DisableVoicePackageWhenInactive { get; set; }
        public bool StopAssistantAfterFinal { get; set; }
        public bool StartServiceFallback { get; set; }
        public string ServiceComponent { get; set; }
        public string KeyDownAction { get; set; }
        public string KeyUpAction { get; set; }
        public int RemoteType { get; set; }

        public DeviceConfig()
        {
            Name = "Android display";
            Enabled = false;
            Serial = "192.168.1.100:5555";
            AutoConnect = true;
            KeepAndroidAwakeWhilePowered = false;
            InputDevice = "/dev/input/event0";
            KeyToken = "KEY_VOICECOMMAND";
            LogcatFilter = "*:I";
            FinalAsrPattern = @"(?<text>.+)";
            VoicePackage = String.Empty;
            ManageVoicePackage = false;
            DisableVoicePackageWhenInactive = false;
            StopAssistantAfterFinal = false;
            StartServiceFallback = false;
            ServiceComponent = String.Empty;
            KeyDownAction = String.Empty;
            KeyUpAction = String.Empty;
            RemoteType = 0;
        }

        public void ApplyDefaults()
        {
            if (String.IsNullOrWhiteSpace(Name)) Name = "Android display";
            if (String.IsNullOrWhiteSpace(InputDevice)) InputDevice = "/dev/input/event0";
            if (String.IsNullOrWhiteSpace(KeyToken)) KeyToken = "KEY_VOICECOMMAND";
            if (String.IsNullOrWhiteSpace(LogcatFilter)) LogcatFilter = "*:I";
            if (String.IsNullOrWhiteSpace(FinalAsrPattern)) FinalAsrPattern = @"(?<text>.+)";
        }
    }
}
