using Avalonia.Controls;

namespace LedgerNest.Desktop.Views;

public sealed partial class RecordDialogFooterView : UserControl
{
    // Performs the record dialog footer view initialization action for this screen or workflow.
    public RecordDialogFooterView()
    {
        InitializeComponent();
    }

    // Performs the record dialog footer content assignment action for this screen or workflow.
    public RecordDialogFooterView(Control useDefault, Control addAnother, Control cancel, Control save)
    {
        InitializeComponent();
        UseDefaultHost.Content = useDefault;
        AddAnotherHost.Content = addAnother;
        CancelHost.Content = cancel;
        SaveHost.Content = save;
    }
}
