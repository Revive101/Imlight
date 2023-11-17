using Imlight.CoreLib.AntiAmbrose;
using Imlight.CoreLib.Login.Models;
using Imlight.CoreLib.WizardData.Implementations;
using System;
using System.Text;

namespace Imlight.CoreLib.Game.Commands;

internal class CommandAccountProtocol : CommandProtocol {
    internal override string Group { get; set; } = "account";

    [Command("create")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void CreateAccountCommand(string username, string password) {
        var newAccount = new Account(username, "", password);
        var createdSuccess = AccountCollection.CreateAccount(newAccount);

        var reply = createdSuccess ? "Account created successfully." : "Account creation failed.";
        InformSenderClient(reply);
    }

    [Command("delete")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void DeleteAccountCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");
            return;
        }

        var authorityReason = $"{Context.Account.Username} wants to delete account {username}.";
        if (!AuthorityRequester.RequestAuthority(account.AuthLevel, Context.Account, authorityReason)) {
            InformSenderClient("You cannot delete an account with a higher auth level than you.");
            return;
        }

        AccountCollection.DeleteAccount(username);
        InformSenderClient("Account deleted successfully.");
    }

    [Command("lock")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void LockAccountCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");
            return;
        }

        var authorityReason = $"{Context.Account.Username} wants to lock account {username}.";
        if (!AuthorityRequester.RequestAuthority(account.AuthLevel, Context.Account, authorityReason)) {
            InformSenderClient("You cannot lock an account with a higher auth level than you.");
            return;
        }

        var accountLockedSuccess = AccountCollection.LockAccount(username);

        var reply = accountLockedSuccess ? "Account locked successfully." : "Account lock failed.";
        InformSenderClient(reply);
    }

    [Command("unlock")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void UnlockAccountCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");
            return;
        }

        var accountUnlockedSuccess = AccountCollection.UnlockAccount(username);

        var reply = accountUnlockedSuccess ? "Account unlocked successfully." : "Account unlock failed.";
        InformSenderClient(reply);
    }

    [Command("password")]
    [AuthRequired(AuthLevel.Administrator)]
    private void ChangePasswordCommand(string username, string newPassword, string newPasswordConfirm) {
        if (newPassword != newPasswordConfirm) {
            InformSenderClient("Passwords do not match.");
            return;
        }

        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");
            return;
        }

        var passwordChangedSuccess = AccountCollection.ChangePassword(username, newPassword);

        var reply = passwordChangedSuccess ? "Password changed successfully." : "Password change failed.";
        InformSenderClient(reply);
    }

    [Command("authlevel")]
    [AuthRequired(AuthLevel.Administrator)]
    private void ChangeAuthLevelCommand(string username, string authLevel) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");
            return;
        }

        // Parse the authLevel as an integer.
        if (!int.TryParse(authLevel, out var authLevelInt)) {
            InformSenderClient("Invalid auth level.");
            return;
        }

        // Make sure the auth level is valid.
        if (!Enum.IsDefined(typeof(AuthLevel), authLevelInt)) {
            InformSenderClient("Invalid auth level.");
            return;
        }

        var authLevelChangeSuccess = AccountCollection.UpdateAuthLevel(username, (AuthLevel) authLevelInt);

        var reply = authLevelChangeSuccess ? "Account auth level changed successfully." : "Account auth level change failed.";
        InformSenderClient(reply);
    }

    [Command("info")]
    [AuthRequired(AuthLevel.HallMonitor)]
    private void GetAccountInfoCommand(string username) {
        var account = AccountCollection.GetAccount(username);
        if (account is null) {
            InformSenderClient("Account not found.");
            return;
        }

        // Craft the reply.
        var sb = new StringBuilder();
        sb.Append($"<center>{account.Username}</center>\n");
        sb.Append($"<center>Email: {account.Email}</center>\n");
        sb.Append($"<center>Auth Level: {account.AuthLevel}</center>\n");
        sb.Append($"<center>Creation Time: {account.CreationTime}</center>\n");
        sb.Append($"<center>Last Login Time: {account.LastLoginTime}</center>\n");
        sb.Append($"<center>Last Login Machine ID: {account.LastLoginMachineId}</center>\n");
        sb.Append($"<center>Last Login IP: {account.LastLoginIp}</center>\n");
        sb.Append($"<center>Is Locked: {account.IsLocked}</center>\n");

        sb.Append($"<center>Character IDs:");
        for (int i = 0; i < account.CharacterIds.Count; i++) {
            sb.Append($"{account.CharacterIds[i]}, ");
        }

        InformSenderClient(sb.ToString(), true);
    }
}
