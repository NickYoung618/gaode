namespace Gaode.Testing;

public static class ApprovedTestRoot
{
    public static string Resolve(string workspace)
    {
        var configured = Environment.GetEnvironmentVariable("GAODE_TEST_ROOT");
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(workspace, "artifacts", "station01", "test-stores")
            : Path.GetFullPath(configured);
        Directory.CreateDirectory(root);
        if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("测试根不得为链接");
        return root;
    }
}
