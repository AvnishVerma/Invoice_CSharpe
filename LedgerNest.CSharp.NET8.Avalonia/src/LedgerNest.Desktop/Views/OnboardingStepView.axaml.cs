using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class OnboardingStepView : UserControl
{
    public OnboardingStepView()
    {
        InitializeComponent();
    }

    public OnboardingStepView(string title, Control steps, Control body)
    {
        InitializeComponent();
        TitleText.Text = title;
        StepHost.Content = steps;
        BodyHost.Content = body;
    }
}
