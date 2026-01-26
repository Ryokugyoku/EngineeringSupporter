using Velopack;
using Velopack.Sources;
using Microsoft.Extensions.Logging;

namespace EngineeringSupporter.Business;

public class UpdateService
{
    private readonly ILogger<UpdateService> _logger;
    private const string RepoUrl = "https://github.com/ryokugyoku/EngineeringSupporter"; // ユーザーのリポジトリに合わせて変更

    public UpdateService(ILogger<UpdateService> logger)
    {
        _logger = logger;
    }

    public async Task CheckAndApplyUpdatesAsync()
    {
        try
        {
            // GitHub Releasesをソースとして指定
            var mgr = new UpdateManager(new GithubSource(RepoUrl, null, false));

            // アップデートがあるか確認
            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null)
            {
                _logger.LogInformation("No updates found.");
                return;
            }

            _logger.LogInformation("New version found: {Version}", newVersion.TargetFullRelease.Version);

            // アップデートをダウンロード
            await mgr.DownloadUpdatesAsync(newVersion);

            // アップデートを適用して再起動
            // ユーザーに確認を促すUIを挟むのが理想的ですが、ここでは直接適用
            mgr.ApplyUpdatesAndRestart(newVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates.");
        }
    }
}
