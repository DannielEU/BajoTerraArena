using System;
using System.IO;

namespace Bajoterra.Client.Tests
{
    /// <summary>
    /// Finds files under <c>Assets/Tests/EditMode/Fixtures</c> both inside the Unity editor (working directory is
    /// the project folder) and in the dotnet test run (fixtures are copied next to the test assembly).
    /// </summary>
    public static class FixtureLocator
    {
        private const string InProject = "Assets/Tests/EditMode/Fixtures";

        public static string Find(string fileName)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (string dir = start; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
                {
                    foreach (string candidate in new[]
                             {
                                 Path.Combine(dir, InProject, fileName),
                                 Path.Combine(dir, "Fixtures", fileName),
                                 Path.Combine(dir, "unity", "BajoTerraClient", InProject, fileName)
                             })
                    {
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }
            throw new FileNotFoundException("Fixture not found: " + fileName);
        }
    }
}
