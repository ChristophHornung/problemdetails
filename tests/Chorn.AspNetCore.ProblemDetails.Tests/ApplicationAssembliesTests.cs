namespace Chorn.AspNetCore.ProblemDetails.Tests;

using System.Reflection;
using Chorn.AspNetCore.ProblemDetails.Tests.App;

/// <summary>
/// Verifies the walk that stands in when there is no dependency file to read - a single file or trimmed
/// application, or a host that writes none.
/// </summary>
public class ApplicationAssembliesTests
{
	[Test]
	public async Task ReferenceWalk_ReachesThePackageTheApplicationUses()
	{
		List<string> reachable = ApplicationAssemblies
			.FromAssemblyReferences(typeof(AppProblems).Assembly).ToList();

		await Assert.That(reachable.Contains("Chorn.AspNetCore.ProblemDetails", StringComparer.OrdinalIgnoreCase))
			.IsTrue();
	}

	[Test]
	public async Task ReferenceWalk_LeavesTheFrameworkAlone()
	{
		List<string> reachable = ApplicationAssemblies
			.FromAssemblyReferences(Assembly.GetExecutingAssembly()).ToList();

		// Loading every framework assembly to ask whether it uses this package would be a waste of a startup.
		await Assert.That(reachable.Any(name =>
			name.StartsWith("System.", StringComparison.OrdinalIgnoreCase))).IsFalse();
		await Assert.That(reachable.Any(name =>
			name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))).IsFalse();
	}

	[Test]
	public async Task ReferenceWalk_DoesNotSeeALibraryTheApplicationNeverUses()
	{
		// The gap the dependency file closes: a project reference whose types the application does not touch
		// leaves no assembly reference behind to follow.
		List<string> reachable = ApplicationAssemblies
			.FromAssemblyReferences(typeof(AppProblems).Assembly).ToList();

		await Assert.That(reachable.Contains("Chorn.AspNetCore.ProblemDetails.Tests.Domain",
			StringComparer.OrdinalIgnoreCase)).IsFalse();
	}
}
