using System.IO;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Gaode.Station01.Desktop.Tests;

[TestClass]
public sealed class DesktopRuntimeLogTests
{
    [TestMethod]
    public void DiagnosticFileIsBoundedAndDoesNotPersistTestToken()
    {
        var keys = new[] { "GAODE_MODE", "GAODE_TEST_PREPARED_LOAD_PATH", "GAODE_TEST_OPERATOR_TOKEN" };
        var old = keys.ToDictionary(x => x, Environment.GetEnvironmentVariable);
        var root = Path.Combine(Path.GetTempPath(), "gaode-desktop-diagnostics-" + Guid.NewGuid().ToString("N"));
        try
        {
            Environment.SetEnvironmentVariable(keys[0], "Test");
            Environment.SetEnvironmentVariable(keys[1], Path.Combine(root, "prepared.json"));
            Environment.SetEnvironmentVariable(keys[2], "diagnostic-test-secret");
            var type = typeof(HostRuntime).Assembly.GetType("Gaode.Station01.Desktop.DesktopRuntimeLog", true)!;
            var write = type.GetMethod("Write", BindingFlags.NonPublic | BindingFlags.Static)!;
            write.Invoke(null, ["Test", new { error = "diagnostic-test-secret", requestId = "request-1" }]);
            var path = Path.Combine(root, "logs", "desktop-runtime.jsonl");
            var first = File.ReadAllText(path);
            StringAssert.Contains(first, "request-1");
            Assert.IsFalse(first.Contains("diagnostic-test-secret", StringComparison.Ordinal));
            using var parsed = JsonDocument.Parse(first);
            Assert.AreEqual("station01-desktop/1", parsed.RootElement.GetProperty("schema").GetString());
            File.WriteAllText(path, new string('x', 2 * 1024 * 1024));
            write.Invoke(null, ["AfterRotation", new { runId = "run-1" }]);
            Assert.IsTrue(File.Exists(path + ".previous"));
            Assert.IsTrue(new FileInfo(path).Length < 4096);
        }
        finally
        {
            foreach (var (key, value) in old) Environment.SetEnvironmentVariable(key, value);
            var logs = Path.Combine(root, "logs");
            File.Delete(Path.Combine(logs, "desktop-runtime.jsonl"));
            File.Delete(Path.Combine(logs, "desktop-runtime.jsonl.previous"));
            if (Directory.Exists(logs)) Directory.Delete(logs);
            if (Directory.Exists(root)) Directory.Delete(root);
        }
    }
}
