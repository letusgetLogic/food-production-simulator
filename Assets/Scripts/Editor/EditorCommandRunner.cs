using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Development helper: executes editor commands from a text file so setup, Play Mode tests and
    /// debugging can be scripted without clicking through the editor (used by the AI assistant, also
    /// handy for repeatable smoke tests).
    ///
    /// Queue:  &lt;project&gt;/Automation/commands.txt  - one command per line, executed top to bottom
    /// Log:    &lt;project&gt;/Automation/log.txt       - command results, console messages, compiler errors
    ///
    /// Commands:
    ///   refresh                          AssetDatabase.Refresh (recompiles changed scripts)
    ///   menu Tools/Food Production/...   execute a menu item (setup dialogs are logged instead of shown)
    ///   save                             save open scenes
    ///   play | stop                      enter / exit Play Mode
    ///   wait 5                           wait seconds (real time)
    ///   status                           play/compile state, scene, dirty flag
    ///   scene                            list root objects with position and components
    ///   dump RootName [depth]            hierarchy below an object with positions/components
    ///   call Object Component Method [arg]   invoke a method via reflection (arg: number, bool or text)
    ///   get Object Component member      log a field/property value
    ///   set Object Component member value   set a field/property (number, bool, enum or text) - test setups, e.g. a tank level
    ///   move Object x y z                teleport an object (e.g. pot onto the portioner lift)
    ///   screenshot name                  game view screenshot to Automation/name.png (Play Mode)
    ///   tests                            run all EditMode tests, results in the log
    ///   clearlog                         empty log.txt
    /// The folder is git-ignored. Remove this file before a release build (editor only, so it never ships).
    /// </summary>
    [InitializeOnLoad]
    public static class EditorCommandRunner
    {
        private const string WaitKey = "fps.automation.waitUntil";
        private const double PollInterval = 0.25;

        private static double _nextPoll;

        /// <summary>True while a command runs - setup dialogs are logged instead of shown.</summary>
        public static bool IsSilent { get; private set; }

        private static string Folder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Automation");
        private static string CommandFile => Path.Combine(Folder, "commands.txt");
        private static string LogFile => Path.Combine(Folder, "log.txt");

        static EditorCommandRunner()
        {
            EditorApplication.update += Poll;
            Application.logMessageReceived += HandleLog;
            CompilationPipeline.assemblyCompilationFinished += HandleCompilation;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        private const string RunningPlayKey = "fps.automation.play";

        /// <summary>
        /// Play Mode started by the runner keeps running while the editor is in the background
        /// (Application.runInBackground only for this session - Player Settings stay unchanged).
        /// </summary>
        private static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RunningPlayKey, false))
            {
                Application.runInBackground = true;
                Write("play mode entered (runInBackground for this session)");
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                SessionState.SetBool(RunningPlayKey, false);
            }
        }

        // ---- Queue ----

        private static void Poll()
        {
            if (EditorApplication.timeSinceStartup < _nextPoll)
            {
                return;
            }
            _nextPoll = EditorApplication.timeSinceStartup + PollInterval;

            bool busy = EditorApplication.isCompiling || EditorApplication.isUpdating;
            if (EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
            {
                return; // switching Play Mode (domain reload follows)
            }

            // While playing, script changes wait for Play Mode to end ("Recompile After Finished Playing"),
            // so "stop" must always get through - otherwise the queue would wait forever.
            if (busy && !(EditorApplication.isPlaying && PeekCommand() == "stop"))
            {
                return;
            }

            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(WaitKey, 0f))
            {
                return;
            }

            if (!File.Exists(CommandFile))
            {
                return;
            }

            List<string> lines;
            try
            {
                lines = File.ReadAllLines(CommandFile).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
            }
            catch (IOException)
            {
                return; // file is being written
            }

            if (lines.Count == 0)
            {
                return;
            }

            string command = lines[0];
            File.WriteAllLines(CommandFile, lines.Skip(1));
            Execute(command);
        }

        private static string PeekCommand()
        {
            try
            {
                return File.Exists(CommandFile)
                    ? File.ReadLines(CommandFile).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith("#"))
                    : null;
            }
            catch (IOException)
            {
                return null;
            }
        }

        private static void Execute(string command)
        {
            Write($">> {command}");
            List<string> args = Tokenize(command);
            string verb = args[0].ToLowerInvariant();

            IsSilent = true;
            try
            {
                switch (verb)
                {
                    case "refresh":
                        if (EditorApplication.isPlaying)
                        {
                            // Changed scripts only compile after Play Mode: stop first, refresh again afterwards.
                            File.WriteAllLines(CommandFile, new[] { "refresh" }.Concat(File.ReadAllLines(CommandFile)));
                            EditorApplication.isPlaying = false;
                            Write("stopping Play Mode before refresh");
                            break;
                        }
                        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                        Write("refreshed");
                        break;
                    case "menu":
                        string path = command.Substring(command.IndexOf(' ') + 1).Trim();
                        Write(EditorApplication.ExecuteMenuItem(path) ? "menu executed" : "MENU NOT FOUND: " + path);
                        break;
                    case "save":
                        Write(EditorSceneManager.SaveOpenScenes() ? "scenes saved" : "SAVE FAILED");
                        break;
                    case "play":
                        SessionState.SetBool(RunningPlayKey, true);
                        EditorApplication.isPlaying = true;
                        break;
                    case "stop":
                        EditorApplication.isPlaying = false;
                        break;
                    case "unpause":
                        EditorApplication.isPaused = false;
                        break;
                    case "wait":
                        float seconds = args.Count > 1 && float.TryParse(args[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out float s) ? s : 1f;
                        SessionState.SetFloat(WaitKey, (float)(EditorApplication.timeSinceStartup + seconds));
                        break;
                    case "status":
                        Write($"playing={EditorApplication.isPlaying} paused={EditorApplication.isPaused} compiling={EditorApplication.isCompiling} " +
                              $"scene={EditorSceneManager.GetActiveScene().path} dirty={EditorSceneManager.GetActiveScene().isDirty} " +
                              $"time={Time.time:0.0} timeScale={Time.timeScale}");
                        break;
                    case "scene":
                        DumpScene();
                        break;
                    case "dump":
                        GameObject root = Find(args.ElementAtOrDefault(1));
                        int depth = args.Count > 2 && int.TryParse(args[2], out int d) ? d : 2;
                        if (root == null) Write("NOT FOUND: " + args.ElementAtOrDefault(1));
                        else Write(Describe(root.transform, depth));
                        break;
                    case "call":
                        Call(args);
                        break;
                    case "get":
                        Get(args);
                        break;
                    case "set":
                        Set(args);
                        break;
                    case "screenshot":
                        string file = Path.Combine(Folder, (args.ElementAtOrDefault(1) ?? "shot") + ".png");
                        ScreenCapture.CaptureScreenshot(file);
                        Write("screenshot requested: " + file);
                        break;
                    case "overlap":
                    {
                        float[] o = args.Skip(1).Take(6).Select(a => float.Parse(a, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        Collider[] hits = Physics.OverlapBox(new Vector3(o[0], o[1], o[2]), new Vector3(o[3], o[4], o[5]), Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
                        Write("overlap: " + string.Join(" | ", hits.Select(h => $"{h.transform.root.name}/{h.name} ({h.GetType().Name}{(h.isTrigger ? ",trigger" : "")}) bounds={Fmt(h.bounds.min)}..{Fmt(h.bounds.max)}")));
                        break;
                    }
                    case "move":
                    {
                        // move Object x y z - teleports an object (Rigidbody velocity reset), e.g. to put a pot on a lift
                        GameObject target = Find(args.ElementAtOrDefault(1));
                        if (target == null) { Write("NOT FOUND: " + args.ElementAtOrDefault(1)); break; }
                        float[] p = args.Skip(2).Take(3).Select(a => float.Parse(a, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                        if (target.TryGetComponent(out Rigidbody body)) { body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero; body.position = new Vector3(p[0], p[1], p[2]); }
                        target.transform.position = new Vector3(p[0], p[1], p[2]);
                        Write($"moved {target.name} to {Fmt(target.transform.position)}");
                        break;
                    }
                    case "look":
                        Look(args);
                        break;
                    case "tests":
                        RunTests();
                        break;
                    case "clearlog":
                        File.WriteAllText(LogFile, string.Empty);
                        break;
                    default:
                        Write("UNKNOWN COMMAND: " + verb);
                        break;
                }
            }
            catch (Exception exception)
            {
                Write("COMMAND FAILED: " + exception);
            }
            finally
            {
                IsSilent = false;
            }
        }

        // ---- Inspection ----

        private static void DumpScene()
        {
            var builder = new StringBuilder();
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                builder.AppendLine(Line(root.transform, 0));
            }
            Write(builder.ToString());
        }

        private static string Describe(Transform transform, int depth, int indent = 0)
        {
            var builder = new StringBuilder(Line(transform, indent));
            if (depth > 0)
            {
                foreach (Transform child in transform)
                {
                    builder.AppendLine().Append(Describe(child, depth - 1, indent + 1));
                }
            }
            return builder.ToString();
        }

        private static string Line(Transform t, int indent)
        {
            string components = string.Join(",", t.GetComponents<Component>()
                .Where(c => c != null && !(c is Transform))
                .Select(c => c.GetType().Name));
            string machine = t.TryGetComponent(out Game.Production.MachineBase m) ? $" state={m.CurrentState}" + (m.HasWarning ? $" warn={m.WarningReason}" : "") + (m.CurrentState == Game.Production.MachineState.Fault ? $" fault={m.FaultReason}" : "") : "";
            return $"{new string(' ', indent * 2)}{t.name}{(t.gameObject.activeInHierarchy ? "" : " (inactive)")} pos={Fmt(t.position)} rotY={t.eulerAngles.y:0} [{components}]{machine}";
        }

        private static string Fmt(Vector3 v) =>
            string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0:0.000}, {1:0.000}, {2:0.000})", v.x, v.y, v.z);

        private static GameObject Find(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            GameObject direct = GameObject.Find(name);
            if (direct != null)
            {
                return direct;
            }

            return Resources.FindObjectsOfTypeAll<Transform>()
                .Where(t => t.gameObject.scene.IsValid() && t.name == name)
                .Select(t => t.gameObject)
                .FirstOrDefault();
        }

        private static Component FindComponent(List<string> args)
        {
            GameObject go = Find(args.ElementAtOrDefault(1));
            if (go == null)
            {
                Write("NOT FOUND: " + args.ElementAtOrDefault(1));
                return null;
            }

            string typeName = args.ElementAtOrDefault(2);
            Component component = go.GetComponents<Component>()
                .FirstOrDefault(c => c != null && (c.GetType().Name == typeName || c.GetType().FullName == typeName));
            if (component == null)
            {
                Write($"COMPONENT NOT FOUND: {typeName} on {go.name}");
            }
            return component;
        }

        private const BindingFlags AnyMember = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

        private static void Call(List<string> args)
        {
            Component component = FindComponent(args);
            if (component == null)
            {
                return;
            }

            string methodName = args.ElementAtOrDefault(3);
            string rawArg = args.ElementAtOrDefault(4);
            // out parameters (e.g. TryTiltDrum(out ProductInstance)) do not count as arguments.
            MethodInfo method = component.GetType().GetMethods(AnyMember)
                .FirstOrDefault(m => m.Name == methodName && m.GetParameters().Count(p => !p.IsOut) == (rawArg == null ? 0 : 1)
                                     || m.Name == methodName && rawArg == null && m.GetParameters().All(p => p.IsOptional || p.IsOut));
            if (method == null)
            {
                Write($"METHOD NOT FOUND: {methodName}");
                return;
            }

            bool argUsed = false;
            object[] parameters = method.GetParameters().Select(p =>
            {
                if (p.IsOut) return null;
                if (rawArg != null && !argUsed) { argUsed = true; return Convert(rawArg, p.ParameterType); }
                return p.DefaultValue;
            }).ToArray();
            object result = method.Invoke(component, parameters);
            Write($"{component.GetType().Name}.{methodName} -> {result ?? "void"}");
        }

        private static void Get(List<string> args)
        {
            Component component = FindComponent(args);
            if (component == null)
            {
                return;
            }

            string member = args.ElementAtOrDefault(3);
            Type type = component.GetType();
            object value = type.GetProperty(member, AnyMember)?.GetValue(component)
                           ?? type.GetField(member, AnyMember)?.GetValue(component);
            if (value is Vector3 vector)
            {
                value = Fmt(vector);
            }
            if (value is System.Collections.IEnumerable list && !(value is string))
            {
                value = "[" + string.Join(", ", list.Cast<object>().Select(o => o?.ToString())) + "]";
            }
            Write($"{type.Name}.{member} = {value ?? "null"}");
        }

        private static void Set(List<string> args)
        {
            Component component = FindComponent(args);
            if (component == null)
            {
                return;
            }

            string member = args.ElementAtOrDefault(3);
            string raw = args.ElementAtOrDefault(4);
            Type type = component.GetType();
            PropertyInfo property = type.GetProperty(member, AnyMember);
            FieldInfo field = property == null ? type.GetField(member, AnyMember) : null;
            if (property != null && property.CanWrite)
            {
                property.SetValue(component, Convert(raw, property.PropertyType));
            }
            else if (field != null)
            {
                field.SetValue(component, Convert(raw, field.FieldType));
            }
            else
            {
                Write($"MEMBER NOT WRITABLE: {member}");
                return;
            }
            Write($"{type.Name}.{member} := {raw}");
        }

        private static object Convert(string raw, Type type)
        {
            if (type == typeof(string)) return raw;
            if (type == typeof(bool)) return bool.Parse(raw);
            if (type.IsEnum) return Enum.Parse(type, raw);
            return System.Convert.ChangeType(raw, type, System.Globalization.CultureInfo.InvariantCulture);
        }

        // ---- Camera ----

        private const string LookCameraName = "AutomationCamera";

        /// <summary>look fromX fromY fromZ atX atY atZ - extra camera on top for screenshots; "look off" removes it.</summary>
        private static void Look(List<string> args)
        {
            GameObject existing = GameObject.Find(LookCameraName);
            if (args.ElementAtOrDefault(1) == "off")
            {
                if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
                Write("look camera removed");
                return;
            }

            float[] v = args.Skip(1).Take(6).Select(a => float.Parse(a, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            GameObject go = existing != null ? existing : new GameObject(LookCameraName, typeof(Camera));
            var from = new Vector3(v[0], v[1], v[2]);
            go.transform.position = from;
            go.transform.LookAt(new Vector3(v[3], v[4], v[5]));
            Camera camera = go.GetComponent<Camera>();
            camera.depth = 100f;
            camera.fieldOfView = 60f;
            Write("look camera at " + Fmt(from));
        }

        // ---- Tests ----

        private static void RunTests()
        {
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new TestLogger());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode }));
            Write("EditMode tests started");
        }

        private sealed class TestLogger : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result) =>
                Write($"TESTS FINISHED: passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} " +
                      $"inconclusive={result.InconclusiveCount}");

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren && result.TestStatus == TestStatus.Failed)
                {
                    Write($"TEST FAILED: {result.FullName}\n  {result.Message}\n  {FirstLines(result.StackTrace, 3)}");
                }
            }
        }

        // ---- Log ----

        private static void HandleLog(string condition, string stackTrace, LogType type)
        {
            string trace = type == LogType.Error || type == LogType.Exception || type == LogType.Assert
                ? "\n    " + FirstLines(stackTrace, 4).Replace("\n", "\n    ")
                : string.Empty;
            Write($"[{type}] {condition}{trace}");
        }

        private static void HandleCompilation(string assembly, CompilerMessage[] messages)
        {
            foreach (CompilerMessage message in messages.Where(m => m.type == CompilerMessageType.Error))
            {
                Write($"[CompileError] {message.message}");
            }
        }

        private static string FirstLines(string text, int count) =>
            string.IsNullOrEmpty(text) ? string.Empty : string.Join("\n", text.Split('\n').Take(count)).Trim();

        private static readonly object FileLock = new object();

        private static void Write(string text)
        {
            try
            {
                lock (FileLock)
                {
                    Directory.CreateDirectory(Folder);
                    File.AppendAllText(LogFile, $"{DateTime.Now:HH:mm:ss} {text}\n");
                }
            }
            catch (IOException)
            {
                // never break the editor because of the log
            }
        }

        private static List<string> Tokenize(string command)
        {
            // Splits on spaces, keeps "quoted parts" together.
            var result = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            foreach (char c in command)
            {
                if (c == '"')
                {
                    quoted = !quoted;
                }
                else if (c == ' ' && !quoted)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Clear();
                    }
                }
                else
                {
                    current.Append(c);
                }
            }

            if (current.Length > 0)
            {
                result.Add(current.ToString());
            }
            return result;
        }
    }
}
