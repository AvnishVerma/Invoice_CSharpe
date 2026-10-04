using Avalonia;
using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class PdfTemplatePreviewView : UserControl
{
    public static readonly StyledProperty<string> TemplateNameProperty = AvaloniaProperty.Register<PdfTemplatePreviewView, string>(nameof(TemplateName), "Classic");
    public static readonly StyledProperty<string> AccentProperty = AvaloniaProperty.Register<PdfTemplatePreviewView, string>(nameof(Accent), "#000000");
    public string TemplateName { get => GetValue(TemplateNameProperty); set => SetValue(TemplateNameProperty, value); }
    public static readonly DirectProperty<PdfTemplatePreviewView, string> TemplateKeyProperty = AvaloniaProperty.RegisterDirect<PdfTemplatePreviewView, string>(nameof(TemplateKey), view => view.TemplateKey);
    public string TemplateKey => TemplateName.Replace(" ", "");
    public string Accent { get => GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
    public PdfTemplatePreviewView() => InitializeComponent();
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TemplateNameProperty)
            RaisePropertyChanged(TemplateKeyProperty, (change.OldValue as string ?? "Classic").Replace(" ", ""), TemplateKey);
    }
}
