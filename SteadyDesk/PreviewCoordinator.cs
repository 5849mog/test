using System.Diagnostics;

namespace SteadyDesk;

internal sealed record PreviewRenderRequest(
    AppConfig Config,
    int Width,
    int Height,
    DateTime Moment,
    string ScenarioLabel);

internal sealed record PreviewRenderResult(
    Image Image,
    string Summary);

internal sealed class PreviewCoordinator : IDisposable
{
    private readonly System.Windows.Forms.Timer _debounceTimer;
    private readonly Func<PreviewRenderRequest> _requestFactory;
    private readonly Action<PreviewRenderResult> _onRendered;
    private readonly Action<string> _onDeferred;
    private bool _rendering;
    private bool _renderAgain;
    private bool _disposed;

    public PreviewCoordinator(
        Func<PreviewRenderRequest> requestFactory,
        Action<PreviewRenderResult> onRendered,
        Action<string> onDeferred,
        int debounceMilliseconds = 250)
    {
        _requestFactory = requestFactory;
        _onRendered = onRendered;
        _onDeferred = onDeferred;
        _debounceTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Clamp(debounceMilliseconds, 100, 1000)
        };
        _debounceTimer.Tick += (_, _) =>
        {
            _debounceTimer.Stop();
            RefreshNow();
        };
    }

    public void RequestRefresh()
    {
        if (_disposed)
        {
            return;
        }

        _debounceTimer.Stop();
        _debounceTimer.Start();
    }

    public void RefreshNow()
    {
        if (_disposed)
        {
            return;
        }

        _debounceTimer.Stop();
        if (_rendering)
        {
            _renderAgain = true;
            return;
        }

        _rendering = true;
        try
        {
            var request = _requestFactory();
            var stopwatch = Stopwatch.StartNew();
            WallpaperRenderer.Render(
                request.Config,
                AppStorage.PreviewWallpaperPath,
                request.Width,
                request.Height,
                request.Moment);

            using var stream = File.OpenRead(AppStorage.PreviewWallpaperPath);
            using var temporary = Image.FromStream(stream);
            var image = new Bitmap(temporary);
            stopwatch.Stop();

            var summary = request.ScenarioLabel
                + " · " + request.Width + "×" + request.Height
                + " · " + request.Moment.ToString("MM-dd HH:mm")
                + " · " + stopwatch.ElapsedMilliseconds + " ms";
            try
            {
                _onRendered(new PreviewRenderResult(image, summary));
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }
        catch (Exception exception)
        {
            _onDeferred("预览等待有效输入：" + exception.Message);
        }
        finally
        {
            _rendering = false;
            if (_renderAgain)
            {
                _renderAgain = false;
                RequestRefresh();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounceTimer.Stop();
        _debounceTimer.Dispose();
    }
}
