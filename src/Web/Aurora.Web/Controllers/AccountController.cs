using Aurora.Modules.Audit.Application;
using Aurora.Modules.Audit.Contracts;
using Aurora.Modules.Audit.Domain;
using Aurora.Modules.Identity.Domain;
using Aurora.Web.Infrastructure;
using Aurora.Web.Models.Account;
using Aurora.Web.Validation;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Aurora.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditRecorder _audit;
    private readonly IAuditRepository _auditRepository;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        AuditRecorder audit,
        IAuditRepository auditRepository,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _audit = audit;
        _auditRepository = auditRepository;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.Authentication)]
    public async Task<IActionResult> Login(LoginViewModel model, [FromServices] IValidator<LoginViewModel> validator, CancellationToken ct)
    {
        await validator.ValidateIntoModelStateAsync(model, ModelState, ct);
        if (!ModelState.IsValid) return View(model);

        var candidate = await _userManager.FindByEmailAsync(model.Email);
        if (candidate is { IsActive: false })
        {
            _logger.LogWarning("Sign-in refused for deactivated account {Email}.", model.Email);
            await RecordSignInAsync(candidate, model.Email, AuditOutcome.Denied, AuditActions.SignInFailed);
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is not null)
            {
                user.LastLoginAtUtc = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
            _logger.LogInformation("User {UserId} signed in.", user?.Id);
            await RecordSignInAsync(user, model.Email, AuditOutcome.Success, AuditActions.SignInSucceeded);
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            await RecordSignInAsync(candidate, model.Email, AuditOutcome.Denied, AuditActions.SignInFailed);
            ModelState.AddModelError(string.Empty, "This account is locked out. Try again later.");
            return View(model);
        }

        _logger.LogWarning("Failed sign-in attempt for {Email}.", model.Email);
        await RecordSignInAsync(candidate, model.Email, AuditOutcome.Failure, AuditActions.SignInFailed);
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    /// <summary>
    /// Records a sign-in attempt. The email is the actor name because the attempt may come from an
    /// unknown account. No password or other secret is written. The organization is set only when the
    /// account is known.
    /// </summary>
    private async Task RecordSignInAsync(ApplicationUser? user, string email, AuditOutcome outcome, string action)
    {
        _audit.Record(user?.OrganizationId, user?.Id, email, action, nameof(ApplicationUser), user?.Id.ToString(), outcome);
        await _auditRepository.SaveChangesAsync();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "CommandCenter");
    }
}
