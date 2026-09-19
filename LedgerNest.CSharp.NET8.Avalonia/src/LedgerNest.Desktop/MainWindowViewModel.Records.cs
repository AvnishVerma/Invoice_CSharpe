using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LedgerNest.Application;
using LedgerNest.Domain;
using LedgerNest.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace LedgerNest.Desktop;

public partial class MainWindowViewModel
{
    // Performs the save record action for this screen or workflow.
    public bool SaveRecord(string kind, FormField[] fields, UiRecord? original = null)
    {
        if (!fields.Select(f => f.Validate()).ToArray().All(v => v)) return false;
        var records = kind == "Customer" ? Customers : kind == "Product" ? Products : Users;
        var databaseValues = fields.ToDictionary(f => f.Label, f => f.Kind == "toggle" ? f.IsChecked.ToString() : f.Kind == "password" ? f.Value : f.Value.Trim());
        var values = fields.Where(f => f.Kind != "password").ToDictionary(f => f.Label, f => f.Kind == "toggle" ? f.IsChecked.ToString() : f.Value.Trim());
        if (original != null && !records.Contains(original)) { Status = "Record no longer exists in this view."; return false; }
        if (kind == "User")
        {
            if (values.GetValueOrDefault("Role") is not ("Admin" or "User"))
            { Status = "Select a valid user role."; return false; }
            if (dbFactory == null && Users.Any(u => u != original && u.Name.Equals(values["Username"], StringComparison.OrdinalIgnoreCase)))
            { Status = "Username is already in use."; return false; }
            if (dbFactory == null && original?["Role"] == "Admin" && values["Role"] != "Admin" && Users.Count(u => u["Role"] == "Admin") <= 1)
            { Status = "The last administrator cannot be demoted."; return false; }
        }
        var sourceId = SaveRecordToDatabase(kind, databaseValues, original?.SourceId ?? 0);
        if (sourceId < 0) return false;
        var record = new UiRecord { SourceId = sourceId, Values = values };
        if (original != null) records[records.IndexOf(original)] = record; else records.Add(record);
        if (kind == "User" && original != null && CurrentUsername == original.Name)
        {
            SetSession(null, "", false);
        }
        Status = $"{kind} saved.";
        return true;
    }
    // Performs the delete record action for this screen or workflow.
    public bool DeleteRecord(string kind, UiRecord record)
    {
        var records = kind switch { "Customer" => Customers, "Product" => Products, "User" => Users, _ => null };
        if (records == null || !records.Contains(record)) return false;
        if (kind == "User" && record["Role"] == "Admin")
        { Status = "Administrator users cannot be deleted."; return false; }
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            using var transaction = db.Database.BeginTransaction();
            if (kind == "Customer")
            {
                var customer = db.Customers.Find(record.SourceId);
                if (customer == null) return false;
                foreach (var invoice in db.Invoices.Where(i => i.CustomerId == customer.Id))
                {
                    if (string.IsNullOrEmpty(invoice.CustomerName)) invoice.CustomerName = customer.Name;
                    invoice.CustomerId = null;
                }
                db.Customers.Remove(customer);
            }
            else if (kind == "Product")
            {
                var product = db.Products.Find(record.SourceId);
                if (product == null) return false;
                foreach (var item in db.InvoiceItems.Where(i => i.ProductId == product.Id)) item.ProductId = null;
                db.Products.Remove(product);
            }
            else
            {
                var user = db.Users.Find(record.SourceId);
                if (user == null) return false;
                db.Users.Remove(user);
            }
            db.SaveChanges();
            transaction.Commit();
        }
        records.Remove(record);
        DeletedRecords.Remove(record.Id);
        if (kind == "User" && CurrentUsername == record.Name)
        {
            SetSession(null, "", false);
        }
        Status = $"{kind} deleted.";
        return true;
    }

    // Performs the set document trash action for this screen or workflow.
    public bool SetDocumentTrash(UiRecord record, bool trashed)
    {
        if (!Invoices.Contains(record)) return false;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            var invoice = db.Invoices.Find(record.SourceId);
            if (invoice == null) { Status = "Document no longer exists."; return false; }
            invoice.DeletedAt = trashed ? DateTime.UtcNow : null;
            db.SaveChanges();
        }
        if (trashed) DeletedRecords.Add(record.Id); else DeletedRecords.Remove(record.Id);
        Status = trashed ? "Document moved to trash." : "Document restored.";
        return true;
    }

    // Performs the delete document permanently action for this screen or workflow.
    public bool DeleteDocumentPermanently(UiRecord record)
    {
        if (!Invoices.Contains(record) || !DeletedRecords.Contains(record.Id)) return false;
        if (dbFactory != null)
        {
            using var db = dbFactory.CreateDbContext();
            db.EnsureCurrentSchema();
            using var transaction = db.Database.BeginTransaction();
            var invoice = db.Invoices.Find(record.SourceId);
            if (invoice == null || invoice.DeletedAt == null) return false;
            db.Payments.RemoveRange(db.Payments.Where(p => p.InvoiceId == record.SourceId));
            db.InvoiceItems.RemoveRange(db.InvoiceItems.Where(i => i.InvoiceId == record.SourceId));
            db.Invoices.Remove(invoice);
            db.SaveChanges();
            transaction.Commit();
        }
        foreach (var payment in Payments.Where(p => p["InvoiceId"] == record.SourceId.ToString()).ToArray()) Payments.Remove(payment);
        DeletedRecords.Remove(record.Id);
        Invoices.Remove(record);
        Status = "Document permanently deleted.";
        return true;
    }

}
