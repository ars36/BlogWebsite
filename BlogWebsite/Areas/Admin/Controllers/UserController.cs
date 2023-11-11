﻿using AspNetCoreHero.ToastNotification.Abstractions;
using BlogWebsite.Models;
using BlogWebsite.Utilites;
using BlogWebsite.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EmailService;

namespace BlogWebsite.Areas.Admin.Controllers
{
	[Area("Admin")]
	public class UserController : Controller
	{
		private readonly UserManager<ApplicationUser> _userManager;
		private readonly SignInManager<ApplicationUser> _signInManager;
		private readonly INotyfService _notification;
		private readonly IEmailSender _emailSender;
		private readonly EmailConfiguration _emailConfig;

		public UserController(UserManager<ApplicationUser> userManager,
							  SignInManager<ApplicationUser> signInManager,
							  INotyfService notyfService, IEmailSender emailSender,
							  EmailConfiguration emailConfig)
		{
			_userManager = userManager;
			_signInManager = signInManager;
			_notification = notyfService;
			_emailSender = emailSender;
			_emailConfig = emailConfig;
		}

		[Authorize(Roles = "Admin")]
		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var users = await _userManager.Users.ToListAsync();
			var vm = users.Select(x => new UserVM()
			{
				Id = x.Id,
				FirstName = x.FirstName,
				LastName = x.LastName,
				UserName = x.UserName,
				Email = x.Email
			}).ToList();
			foreach (var user in vm)
			{
				var singleUser = await _userManager.FindByIdAsync(user.Id);
				var role = await _userManager.GetRolesAsync(singleUser);
				user.Role = role.FirstOrDefault();
			}

			return View(vm);
		}


		[HttpGet("ForgotPassword")]
		public IActionResult ForgotPassword()
		{
			return View(new ForgotPasswordVM());
		}

		[HttpPost("ForgotPassword")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ForgotPassword(ForgotPasswordVM vm)
		{
			if (!ModelState.IsValid)
				return View(vm);
			var user = await _userManager.FindByEmailAsync(vm.Email);
			var checkUserByEmail = await _userManager.FindByEmailAsync(vm.Email);	
			if (user == null)
			{
				return RedirectToAction(nameof(ForgotPasswordConfirmationError));
			}
			else
			{
				var token = await _userManager.GeneratePasswordResetTokenAsync(user);

				var callback = Url.Action("ResetPassword", "User", new { token, email = user.Email }, Request.Scheme);
				var message = new Message(_emailConfig, new string[] { user.Email }, "Reset password link", callback!, null!);
				await _emailSender.SendEmailAsync(message);
				return RedirectToAction(nameof(ForgotPasswordConfirmation));
			}
			
		}

		[HttpGet("ForgotPasswordConfirmation")]
		public IActionResult ForgotPasswordConfirmation()
		{
			return View();
		}
		[HttpGet("ForgotPasswordConfirmationError")]
		public IActionResult ForgotPasswordConfirmationError()
		{
			return View();
		}

		//[Authorize(Roles = "Admin")]
		//[HttpPost("Delete")]
		//public async Task<IActionResult> DeleteUser(string id)
		//{
		//	var user = await _userManager.FindByIdAsync(id);

		//	if (user == null)
		//	{
		//		return NotFound();
		//	}

		//	var result = await _userManager.DeleteAsync(user);

		//	if (result.Succeeded)
		//	{
		//		_notification.Success("User Deleted Successfully!");
		//		return RedirectToAction("Index", "User", new {area=("Admin")});
		//	}
		//	else
		//	{
		//		// Xử lý lỗi trong result.Errors
		//		foreach (var error in result.Errors)
		//		{
		//			ModelState.AddModelError("", error.Description);
		//		}
		//		return RedirectToAction("Index", "User", new { area = ("Admin") });
		//	}
		//}


		//Register
		[HttpGet("Register")]
		public IActionResult Register()
		{
			return View(new RegisterVM());
		}

		[HttpPost("Register")]
		public async Task<IActionResult> Register(RegisterVM vm)
		{
			if (!ModelState.IsValid)
			{
				return View(vm);
			}

			var checkUserByEmail = await _userManager.FindByEmailAsync(vm.Email);
			if (checkUserByEmail != null)
			{
				_notification.Error("This Email Already Exist!");
				return View(vm);
			}
			var checkUserByUsername = await _userManager.FindByNameAsync(vm.UserName);
			if (checkUserByUsername != null)
			{
				_notification.Error("This UserName Already Exist!");
				return View(vm);
			}

			var applicationUser = new ApplicationUser()
			{
				FirstName = vm.FirstName,
				LastName = vm.LastName,	
				Email = vm.Email,
				UserName = vm.UserName
			};

			var result = await _userManager.CreateAsync(applicationUser, vm.Password);

			if (result.Succeeded)
			{
				await _userManager.AddToRoleAsync(applicationUser, WebsiteRole.WebisteAuthor);
				_notification.Success("User Created Successfully!");
				return RedirectToAction("Login", "User", new { area = "Admin" });
			}
			return View(vm);

		}

		[HttpGet("ResetPassword")]
		public IActionResult ResetPassword(string token, string email)
		{
			var model = new ResetPasswordVM { Token = token, Email = email };
			return View(model);
		}

		[HttpPost("ResetPassword")]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> ResetPassword(ResetPasswordVM vm)
		{
			if (!ModelState.IsValid)
				return View(vm);

			var user = await _userManager.FindByEmailAsync(vm.Email);
			if (user == null)
				RedirectToAction(nameof(ResetPasswordConfirmation));

			var resetPassResult = await _userManager.ResetPasswordAsync(user!, vm.Token, vm.Password);
			if (!resetPassResult.Succeeded)
			{
				foreach (var error in resetPassResult.Errors)
				{
					ModelState.TryAddModelError(error.Code, error.Description);
				}

				return View();
			}

			return RedirectToAction(nameof(ResetPasswordConfirmation));
		}

		[HttpGet("ResetPasswordConfirmation")]
		public IActionResult ResetPasswordConfirmation()
		{
			return View();
		}

		[HttpGet("Login")]
		public IActionResult Login()
		{
			if (!HttpContext.User.Identity!.IsAuthenticated)
			{
				return View(new LoginVM());
			}
			return RedirectToAction("Index", "Home", new { area = "Default" });
		}

		[HttpPost("Login")]
		public async Task<IActionResult> Login(LoginVM vm)
		{
			if (!ModelState.IsValid)
			{
				return View(vm);
			}
			var existingUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == vm.Username);
			if (existingUser == null)
			{
				_notification.Error("Username does not exist");
				return View(vm);
			}
			var verifyPassword = await _userManager.CheckPasswordAsync(existingUser, vm.Password);
			if (!verifyPassword)
			{
				_notification.Error("Password does not match");
				return View(vm);
			}
			await _signInManager.PasswordSignInAsync(vm.Username, vm.Password, vm.RememberMe, true);
			_notification.Success("Logged In Successfully!");
			return RedirectToAction("Index", "Home", new { area = "Default" });
		}

		[HttpPost]
		[Authorize]
		public IActionResult Logout()
		{
			_signInManager.SignOutAsync();
			_notification.Success("You logged out successfully!");
			return RedirectToAction("Index", "Home", new { area = "Default" });
		}
	}
}
