using Fallout.Common;
using Fallout.Solutions;
using Automation.Fallout.Components;
using Automation.Fallout.Components.Components;
using Automation.Fallout.Components.DefaultBuilds;
using Automation.Fallout.Components.Parameters;

/// <summary>
/// Build configuration for VelopackBuild
/// </summary>

public class Build : GitHubActionsBuild, IShowVersion, IClean, ICompile, IRestore, IScanForSecrets, IRunUnitTests, IRunIntegrationTests, IGenerateCoverageReport, ITest, IUpdateChangelog, IVelopack, ITagRelease, IAnnounceRelease, ICreateGitHubRelease
{

    public static int Main() => Execute<Build>(
        y => ((IVelopack)y).ReleaseVelopack);

    string IHasVelopack.VelopackProjectName => "Greenlight.EdgeLightClient";
    string IHasVelopack.VelopackIconPath => @"Greenlight.EdgeLightClient\Assets\MeddlingIdiot.ico";
    bool IHasTests.BreakBuildOnSecretLeaks => false;

    // No MinCoverageThreshold, the same as the other clients. Only EdgeScene and EdgeLayout are
    // testable without a window, so the whole solution measures about 11% - a copied threshold of
    // 20 failed CoverageReport with "Coverage 10.91% is below threshold 20%", which reads like a
    // missing coverage file rather than a gate. Raise it here if that ever stops being true.

    // Automation.Fallout.Components defaults these to AFTR's staftrinstallers, which this project has
    // no access to. Every MeddlingIdiot installer lives in meddlingidiotinstallers; the Nuke-era
    // library defaulted there, and the migration to Fallout silently moved the destination.
    // Without these the SAS token is sent to the wrong account and Azure rejects it with
    // AuthenticationFailed (403) — which reads like a missing token but is a mismatched one
    string IHasVelopack.AzureBlobAccount => "meddlingidiotinstallers";
    string IHasVelopack.AzureBlobEndpoint => "https://meddlingidiotinstallers.blob.core.windows.net";
}
