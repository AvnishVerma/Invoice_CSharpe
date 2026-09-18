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
    private AppUser? sessionAccount;

    public long SessionVersion { get; private set; }

    // Performs the can continue workspace operation action for this screen or workflow.
    public bool CanContinueWorkspaceOperation(long version) => ValidateSession() && CanAccessWorkspace && SessionVersion == version;

    public bool CanAccessWorkspace => dbFactory == null || (CurrentUsername != null && !RequiresPasswordChange);
    public string? CurrentUsername { get; private set; }
    public string CurrentRole { get; private set; } = "";
    public bool RequiresPasswordChange { get; private set; }

    // Performs the set session action for this screen or workflow.
    private void SetSession(string? username, string role, bool requiresPasswordChange)
    {
        SessionVersion++;
        if (username == null) sessionAccount = null;
        CurrentUsername = username;
        CurrentRole = role;
        RequiresPasswordChange = requiresPasswordChange;
        OnPropertyChanged(nameof(CurrentUsername));
        OnPropertyChanged(nameof(CurrentRole));
        OnPropertyChanged(nameof(RequiresPasswordChange));
        OnPropertyChanged(nameof(CanAccessWorkspace));
    }

    // Performs the sign out action for this screen or workflow.
    public void SignOut()
    {
        SetSession(null, "", false);
        Status = "Signed out.";
    }

    // Performs the sign in action for this screen or workflow.
    public bool SignIn(string username, string password)
    {
        var user = AuthenticateUser(username, password);
        if (user == null)
        {
            if (CurrentUsername != null || sessionAccount != null) SetSession(null, "", false);
            return false;
        }
        sessionAccount = user;
        SetSession(user.Username, user.Role, !user.PasswordChanged);
        return true;
    }

    // Performs the validate session action for this screen or workflow.
    public bool ValidateSession()
    {
        if (dbFactory == null) return true;
        if (sessionAccount == null) return false;
        try
        {
            using var db = dbFactory.CreateDbContext();
            var current = db.Users.AsNoTracking().SingleOrDefault(u => u.Id == sessionAccount.Id);
            if (current != null && current.Username == sessionAccount.Username && current.Role == sessionAccount.Role
                && current.PasswordHash == sessionAccount.PasswordHash && current.Salt == sessionAccount.Salt
                && current.PasswordChanged == sessionAccount.PasswordChanged) return true;
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException)
        {
            SetSession(null, "", false);
            Status = "Unable to verify your session. Log in again.";
            return false;
        }
        SetSession(null, "", false);
        Status = "Your account changed. Log in again.";
        return false;
    }

    // Performs the change current password action for this screen or workflow.
    public bool ChangeCurrentPassword(FormField[] fields)
    {
        if (!ValidateSession() || CurrentUsername == null)
        {
            Status = "Log in before changing your password.";
            return false;
        }
        if (!ChangePassword(CurrentUsername, fields)) return false;
        RequiresPasswordChange = false;
        OnPropertyChanged(nameof(RequiresPasswordChange));
        OnPropertyChanged(nameof(CanAccessWorkspace));
        return true;
    }

    // Performs the verify user action for this screen or workflow.
    public bool VerifyUser(string username, string password) => AuthenticateUser(username, password) != null;

    // Performs the authenticate user action for this screen or workflow.
    private AppUser? AuthenticateUser(string username, string password)
    {
        if (dbFactory == null)
        {
            Status = "User storage is not available.";
            return null;
        }

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var user = db.Users.AsEnumerable().FirstOrDefault(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user == null || !PasswordCredentials.Verify(password, user.Salt, user.PasswordHash, out var needsUpgrade))
        {
            Status = "Invalid username or password.";
            return null;
        }

        if (needsUpgrade)
        {
            user.Salt = PasswordCredentials.CreateSalt();
            user.PasswordHash = HashPassword(password, user.Salt);
            db.SaveChanges();
        }
        Status = $"Logged in as {user.Username}.";
        return user;
    }

    // Performs the change password action for this screen or workflow.
    public bool ChangePassword(string username, FormField[] fields)
    {
        if (dbFactory == null)
        {
            Status = "User storage is not available.";
            return false;
        }
        if (!fields.Select(f => f.Validate()).ToArray().All(v => v)) return false;
        if (fields[1].Value.Length < 8)
        {
            fields[1].Error = "Password must be at least 8 characters.";
            return false;
        }
        if (fields[1].Value != fields[2].Value)
        {
            fields[2].Error = "Passwords do not match.";
            return false;
        }

        using var db = dbFactory.CreateDbContext();
        db.EnsureCurrentSchema();
        var user = db.Users.AsEnumerable().FirstOrDefault(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user == null || !PasswordCredentials.Verify(fields[0].Value, user.Salt, user.PasswordHash, out _))
        {
            fields[0].Error = "Current password is incorrect.";
            return false;
        }

        user.Salt = PasswordCredentials.CreateSalt();
        user.PasswordHash = HashPassword(fields[1].Value, user.Salt);
        user.PasswordChanged = true;
        db.SaveChanges();
        if (sessionAccount?.Id == user.Id) sessionAccount = user;
        Status = "Password changed.";
        return true;
    }

}
