using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using LedgerNest.Desktop.Tests.Fixtures;
using LedgerNest.Desktop.Views;

namespace LedgerNest.Desktop.Tests.UI;

[Collection("AXAML UI")]
public sealed class PriceCalendarTests(HeadlessFixture headless)
{
    [Theory]
    [InlineData(900, false)]
    [InlineData(1200, true)]
    public Task CalendarFitsColumnAndUpdatesExistingDate(int width, bool dark) => headless.Run(() =>
    {
        var model = new ProductPriceMasterViewModel(null, () => "admin", () => "Admin");
        model.EffectiveDate = new DateTimeOffset(2026, 10, 31, 13, 40, 0, TimeSpan.FromHours(5.5));
        var view = new ProductPriceMasterView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = 850, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        try
        {
            window.Show(); Settle(window);
            var calendar = Assert.Single(view.GetVisualDescendants().OfType<CalendarDatePicker>());
            Assert.Equal(new DateTime(2026, 10, 31), calendar.SelectedDate);
            Assert.Equal(36, calendar.Bounds.Height);
            Assert.True(calendar.Bounds.Right <= ((Control)calendar.Parent!).Bounds.Width + 1);
            calendar.SelectedDate = new DateTime(2026, 11, 15); Settle(window);
            Assert.Equal(15, model.EffectiveDate!.Value.Day);
            Assert.Equal(TimeSpan.FromHours(5.5), model.EffectiveDate.Value.Offset);
            Assert.Equal(new TimeSpan(13, 40, 0), model.EffectiveDate.Value.TimeOfDay);
            AvaloniaHeadlessPlatform.ForceRenderTimerTick(); Settle(window);
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            var output = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "price-calendar");
            Directory.CreateDirectory(output);
            frame!.Save(Path.Combine(output, $"{width}-{dark}.png"), new Avalonia.Media.Imaging.PngBitmapEncoderOptions());
            model.EffectiveDate = null; Settle(window);
            Assert.Null(calendar.SelectedDate);
        }
        finally { window.Close(); }
    });
    private static void Settle(Window window) { Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
}
