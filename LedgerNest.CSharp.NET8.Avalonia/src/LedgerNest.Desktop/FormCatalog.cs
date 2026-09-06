namespace LedgerNest.Desktop;

public static class FormCatalog
{
    private static FormField Text(string label, string value = "", bool required = false) => new(label, value, required: required);
    private static FormField Number(string label, string value = "0") => new(label, value, "number");
    private static FormField Toggle(string label, bool value = false, string help = "") => new(label, value ? "true" : "false", "toggle") { Help = help };
    private static FormField Choice(string label, params string[] options) => new(label, options[0], "choice", options);
    public static FormField[] Customer() => [new("Name", required: true) { MaxLength = 50, Icon = "person" }, new("Business Name") { MaxLength = 100, Icon = "business" }, new("Phone", required: true) { MaxLength = 12, Icon = "phone" }, new("Email") { MaxLength = 100, Icon = "email" }, new("GST / VAT Number") { MaxLength = 50, Icon = "receipt_long" }, new("Address", kind: "multiline") { MaxLength = 500, Icon = "location_on" }];
    public static FormField[] Product() => [Choice("Type", "Product", "Service"), Text("Name", required: true), Text("Alias Name (for invoice PDF)"), new("Description", kind: "multiline"), Text("HSN/SAC"), Number("Sale Price"), Number("Purchase Price"), Number("Default Discount"), Number("Tax (%)"), Toggle("Price includes tax"), Number("Stock"), Toggle("Unlimited stock"), Choice("Unit", "None", "pcs", "kg", "g", "l", "m", "box", "Custom…"), Text("Custom unit"), Text("Storage Location"), Text("Container Number"), Text("Batch Number"), new("Expiry Date", kind: "date"), new("Manufacture Date", kind: "date"), Text("Supplier Name"), Text("SKU Code"), new("Notes", kind: "multiline")];
    public static FormField[] User() => [Text("Username", required: true), new("Password", kind: "password", required: true), Choice("Role", "User", "Admin")];
    public static FormField[] Payment() => [Number("Amount"), new("Date", DateTime.Today.ToString("yyyy-MM-dd"), "date"), Choice("Payment Method", "Cash", "UPI", "Bank Transfer", "Card", "Cheque", "Other"), Number("Tax covered"), new("Notes / Reference", kind: "multiline")];
    public static FormField[] CustomItem() => [Text("Name", required: true), new("Description", kind: "multiline"), Number("Quantity", "1"), Number("Price"), Number("Tax (%)"), Number("Discount"), Choice("Unit", "None", "pcs", "kg", "Custom…"), Text("Custom unit")];
    public static Dictionary<string, FormSection[]> Settings() => new()
    {
        ["Company Info"] = [
            new("LOGO", [new("Company logo", kind: "file"), Toggle("Show on PDF", true)]),
            new("COMPANY DETAILS", [Text("Company Name", required: true), new("Country", "India", "choice", LegacyChoices.Countries), Text("GSTIN"), Text("PAN"), Text("FSSAI Code"), Text("Phone"), Text("Email"), Text("Website"), new("Address", kind: "multiline")]),
            new("BUSINESS TYPE", [Choice("Business Type", "Products & Services", "Products", "Services")]),
            new("PAYMENT SETTINGS", [Toggle("Show QR code on invoice", true), Toggle("Show bank details on invoice", true)]),
            new("UPI ACCOUNTS", [Text("Label", "Primary"), Text("UPI ID"), Toggle("Default account", true)]),
            new("BANK ACCOUNTS", [Text("Label", "Primary"), Text("Bank Name"), Text("Account Number"), Text("IFSC Code"), Toggle("Default account", true)])],
        ["Invoice Settings"] = [
            new("General", [new("Currency", LegacyChoices.Currencies[0], "choice", LegacyChoices.Currencies), Text("Invoice Prefix"), Number("Starting Number", "1"), Toggle("Leading Zeros", true, "Pad invoice numbers with leading zeros"), Choice("Date Format", "dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd MMM yyyy"), Choice("Time Format", "12 hour", "24 hour"), Toggle("Show time in PDF"), Text("Quantity Column", "Qty"), new("Additional Information", kind: "multiline"), new("Thank You Note", kind: "multiline"), Toggle("Hide Invoice Number")]),
            new("Tax", [Number("Default Tax Rate (%)", "18"), Toggle("Tax Enabled", true), Choice("Tax Mode", "Global", "Per Item", "No Tax"), Toggle("Show GST fields", true), Choice("Default GST Title", "Invoice", "Tax Invoice", "Bill of Supply", "Invoice-cum-Bill of Supply", "Cash Bill", "Credit Note", "Debit Note", "Revised Invoice"), Toggle("Show Round Off", true)]),
            new("Items", [Toggle("Show Description", true), Toggle("Description on new line"), Toggle("Show Alias Name"), Toggle("Allow Fractional Quantity", true), Toggle("Show Product / Service Tag"), Toggle("Allow Duplicate Items"), Toggle("Show Previous Balance")]),
            new("Branding", [Choice("Logo Position", "Left", "Right"), Number("Logo Size", "80"), new("Signature Image", kind: "file"), Number("Signature Size", "80"), Choice("Signature Position", "Right", "Left"), new("Watermark Image", kind: "file"), new("Watermark Opacity", "15", "slider")]),
            new("Columns", [Toggle("Show Sl. No.", true), Toggle("Item Name", true), Toggle("HSN/SAC", true), Toggle("Show Quantity", true), Toggle("Price", true), Toggle("Tax", true), Toggle("Show Discount", true), Toggle("Total", true)]),
            new("Custom Fields", [Toggle("Enable Custom Fields"), Text("Field Name"), Choice("Field Type", "Text", "Number", "Date"), Toggle("Required"), Toggle("Show on PDF", true)]),
            new("Customer", [Toggle("Show Customer Business Name", true), Toggle("Show Customer Address", true), Toggle("Show Customer Phone", true), Toggle("Show Customer Email", true), Toggle("Show Customer GSTIN", true)])],
        ["PDF Settings"] = [
            new("PAGE SIZE", [Choice("Page Size", "A4", "A5", "A6", "Thermal 80mm", "Thermal 58mm"), Choice("Orientation", "Portrait", "Landscape")]),
            new("TEMPLATES", [Choice("Template", "Classic", "Modern", "Minimal", "Executive", "Compact", "Thermal", "Grid Classic")]),
            new("DISPLAY OPTIONS", [Toggle("Show Total Quantity", true), Choice("Item Layout", "Table", "Detailed"), Number("Company Name Size", "18")]),
            new("THEME COLOR", [Text("Theme Color", "#002E78")])],
        ["Product Details"] = [
            new("Product Fields", [Toggle("Name", true, "Always required"), Toggle("Price", true, "Always required"), Toggle("Stock", true), Toggle("Alias Name", true), Toggle("Tax Rate", true), Toggle("HSN/SAC", true), Toggle("Description", true), Toggle("Purchase Price", true), Toggle("Default Discount", true), Toggle("Unit", true), Toggle("Product / Service Type", true), Toggle("Advanced Information"), Toggle("Extra Cost")]),
            new("Advanced Information", [Toggle("Storage Location"), Toggle("Container Number"), Toggle("Batch Number"), Toggle("Expiry Date"), Toggle("Manufacture Date"), Toggle("Supplier Name"), Toggle("SKU Code"), Toggle("Notes")])],
        ["Accessibility"] = [new("Create Invoice Layout", [Choice("Layout", "New Layout", "Classic Layout")])]
    };
}
