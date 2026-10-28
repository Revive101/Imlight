/*
 * Imlight
 * Copyright (C) 2025 Revive101
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * ========================================================================
 * Authentication controller for the WebUI and API
 * ========================================================================
 * This class handles authentication-related endpoints for the API,
 * Including login, registration, password changes and retrieving the current user's profile.
 *
 * Created by: Phill
 * Version: KALI 1.0
 * Last Updated: 29.10.2026
*/


using Imlight.Api.Auth;
using Imlight.Common;
using Imlight.CoreLib.WizardData;
using Imlight.CoreLib.WizardData.Collections;
using Imlight.CoreLib.WizardData.Models.Player;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Imlight.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase {

    private static readonly uint s_minPasswordLength = 6;
    private static readonly uint s_maxUsernameLength = 32;

    private static readonly bool s_allowRegistration =
        ConfigurationManager.Settings["API.AllowRegistration"].AsBool(false);

    public sealed record LoginRequest(string Username, string Password);
    public sealed record RegisterRequest(string Username, string Password);
    public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

    /// <summary>
    /// Authenticates an account with username and password and issues a 12-hour JWT for the WebUI.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public IActionResult Login([FromBody] LoginRequest request) {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password)) {
            return BadRequest(new { error = "Username and plaintextPassword are required." });
        }

        var account = AccountCollection.GetAccountForCharacterList(request.Username);
        if (account is null || !VerifyPassword(request.Password, account.PasswordHash)) {
            return Unauthorized(new { error = "Invalid username or plaintextPassword." });
        }

        if(account.IsLocked || (account.InfractionHistory?.IsCurrentlyBanned ?? false)) {
            return Unauthorized(new { error = "Account is locked." });
        }

        var token = JwtTokenService.GenerateToken(account);
        return Ok(new {
            token,
            account= MapAccountSummary(account)
        });
    }

    /// <summary>
    /// Creates a new player account if self-registration is enabled in Imlight.ini and logs them right in.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public IActionResult Register([FromBody] RegisterRequest request) {
        if(!s_allowRegistration) {
            return StatusCode(403, new { error = "Account registration is currently disabled." });
        }

        var username = request.Username?.Trim() ?? string.Empty;
        var password = request.Password?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username) || username.Length > s_maxUsernameLength) {
            return BadRequest(new { error = "Invalid username." });
        }

        if (password.Length < s_minPasswordLength) {
            return BadRequest(new { error = "Password must be at least 6 characters long." });
        }

        var account = DatabaseUtilities.CreateEmbeddedDatabaseAccount(username, "", password);
        if (account is null) {
            return StatusCode(500, new { error = "Failed to create account. Account already exists." });
        }

        var token = JwtTokenService.GenerateToken(account);
        return Ok(new {
            token,
            account = MapAccountSummary(account)
        });
    }

    /// <summary>
    /// Returns the current caller's account profile and active permission scopes.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult GetMe() {
        var scopes = User.FindAll("scope").Select(c => c.Value).ToList();
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!ulong.TryParse(idClaim, out var accountId) || accountId == 0) {
            // Master token or synthetic identity
            return Ok(new {
                accountId = "0",
                username = User.Identity?.Name ?? "ApiToken",
                authLevel = User.FindFirst("auth_level")?.Value ?? "None",
                crowns = 0,
                scopes,
                allowRegistration = s_allowRegistration
            });
        }

        var account = AccountCollection.GetAccountForCharacterList(accountId);
        if (account is null) {
            return NotFound(new { error = "Account not found." });
        }

        return Ok(MapAccountSummary(account, scopes));
    }

    /// <summary>
    /// Updates the logged-in player's password after verifying their current password.
    /// </summary>
    [HttpPost("password")]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequest request) {
        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword)) {
            return BadRequest(new { error = "Current and new passwords are required." });
        }

        if (request.NewPassword.Length < s_minPasswordLength) {
            return BadRequest(new { error = "New password must be at least 6 characters." });
        }

        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!ulong.TryParse(idClaim, out var accountId) || accountId == 0) {
            return BadRequest(new { error = "Cannot change password for a non-account token." });
        }

        var account = AccountCollection.GetAccountForCharacterList(accountId);
        if (account is null || !VerifyPassword(request.CurrentPassword, account.PasswordHash)) {
            return Unauthorized(new { error = "Current password is incorrect." });
        }

        if (!AccountCollection.ChangePassword(account.Username, request.NewPassword)) {
            return StatusCode(500, new { error = "Failed to update password." });
        }

        return Ok(new { message = "Password updated successfully." });
    }


    private static bool VerifyPassword(string plaintextPassword, string storedHash) {
        if (string.IsNullOrEmpty(storedHash)) {
            return false;
        }

        var computedHash = DatabaseUtilities.CreateHashedPassword(plaintextPassword);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHash),
            Encoding.UTF8.GetBytes(storedHash));
    }

    private static object MapAccountSummary(
            Account account,
            List<string>? scopes = null
        ) => new {
            accountId = account.AccountId.ToString(),
            username = account.Username,
            email = account.Email,
            authLevel = account.AuthLevel.ToString(),
            chatMode = account.ChatMode.ToString(),
            crowns = account.Crowns,
            characterCount = account.CharacterIds.Count,
            scopes = scopes ?? [..ApiScopes.GetScopesForAuthLevel(account.AuthLevel)],
            allowRegistration = s_allowRegistration,
        };
}
