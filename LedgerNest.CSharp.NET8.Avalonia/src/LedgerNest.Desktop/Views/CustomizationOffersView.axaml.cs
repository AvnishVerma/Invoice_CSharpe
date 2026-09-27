using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

/// <summary>Displays the available tailored LedgerNest customization offerings.</summary>
public sealed partial class CustomizationOffersView : UserControl
{
    public CustomizationOffersView()
    {
        InitializeComponent();
    }

    public CustomizationOffersView(string brandName)
        : this()
    {
        DataContext = new CustomizationOffersModel(brandName);
    }
}

public sealed class CustomizationOffersModel
{
    public string Heading { get; }
    public IReadOnlyList<CustomizationOffer> Offers { get; } =
    [
        new("Custom PDF Template", "An invoice design tailored to your business and branding."),
        new("Custom Fields", "Capture the additional details your business needs."),
        new("White Label", "Your brand, logo and identity throughout the application."),
        new("Industry Build", "A tailored workflow for your industry.")
    ];

    public CustomizationOffersModel(string brandName) => Heading = $"Customize {brandName}";
}

public sealed record CustomizationOffer(string Title, string Description);
