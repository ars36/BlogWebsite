﻿using AspNetCoreHero.ToastNotification.Abstractions;
using BlogWebsite.Data;
using BlogWebsite.Models;
using BlogWebsite.Utilites;
using BlogWebsite.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogWebsite.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize]
    public class PostController : Controller
    {
        private readonly ApplicationDbContext _context;
        public INotyfService _notification { get; }
        private IWebHostEnvironment _webHostEnvironment;
        private readonly UserManager<ApplicationUser> _userManager;
        public PostController(ApplicationDbContext context,
                              INotyfService notyfService,
                              IWebHostEnvironment webHostEnvironment,
                              UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _userManager = userManager;
            _notification = notyfService;
        }


		[HttpGet]
		public async Task<IActionResult> Index()
		{
			var loggedInUser = await _userManager.GetUserAsync(User);
			var loggedInUserRole = await _userManager.GetRolesAsync(loggedInUser!);
            var listOfPosts= new List<Post>();
            if (loggedInUserRole[0] == WebsiteRole.WebisteAdmin)
            {
                listOfPosts = await _context.posts!
                    .Include(x => x.ApplicationUsers)
                    .OrderByDescending(x => x.CreatedDate)
                    .ToListAsync();
            }
            else
            {
                listOfPosts = await _context.posts!
                    .Include(x => x.ApplicationUsers)
                    .OrderByDescending(x => x.CreatedDate)
                    .Where(x => x.ApplicationUsers!.Id == loggedInUser!.Id)
                    .ToListAsync();
            }
            var listOfPostVM = listOfPosts.Select(x => new PostVM()
			{
				Id = x.Id,
				Title = x.Title,
				CreateDate = x.CreatedDate,
				ThumbnailUrl = x.ThumbnailUrl,
				AuthorName = (x.ApplicationUsers != null) ? x.ApplicationUsers.FirstName + " " + x.ApplicationUsers.LastName : "Unknown Author"
			}).ToList();
			return View(listOfPostVM);
		}

		[HttpGet]
        public IActionResult CreatePost()
        {
            return View(new CreatPostVM());
        }

        [HttpPost]
        public async Task<IActionResult> CreatePost(CreatPostVM vm)
        {
            if (!ModelState.IsValid) { return View(vm); }

            var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);

            var post = new Post();
            post.Title = vm.Title;
            post.Description = vm.Description;
            post.ApplicationUserId = loggedInUser!.Id;

            if (post.Title != null)
            {
                string slug = vm.Title!.Trim();
                slug = slug.Replace(" ", "-");
                post.Slug = slug + "-" + Guid.NewGuid();
            }

            if (vm.Thumbnail != null)
            {
                post.ThumbnailUrl = UploadImage(vm.Thumbnail);
            }

            await _context.posts!.AddAsync(post);
            await _context.SaveChangesAsync();
            _notification.Success("Post Created Successfully!");
            return RedirectToAction("Index");
        }


        [HttpPost]
        public async Task<IActionResult> DeletePost(int id)
        {
            var post = await _context.posts!.FirstOrDefaultAsync(x => x.Id == id);

            var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
            var loggedInUserRole = await _userManager.GetRolesAsync(loggedInUser!);

            if (loggedInUserRole[0] == WebsiteRole.WebisteAdmin || loggedInUser?.Id == post?.ApplicationUserId)
            {
                _context.posts!.Remove(post!);
                await _context.SaveChangesAsync();
                _notification.Success("Post Deleted Successfully!");
                return RedirectToAction("Index", "Post", new { area = "Admin" });
            }
            return View();
        }


		[HttpGet]
		public async Task<IActionResult> EditPost(int id)
		{
			var post = await _context.posts!.FirstOrDefaultAsync(x => x.Id == id);
			if (post == null)
			{
				_notification.Error("Post not found!");
				return View();
			}

			var loggedInUser = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == User.Identity!.Name);
			var loggedInUserRole = await _userManager.GetRolesAsync(loggedInUser!);
			if (loggedInUserRole[0] != WebsiteRole.WebisteAdmin && loggedInUser!.Id != post.ApplicationUserId)
			{
				_notification.Error("You are not Authorized!");
				return RedirectToAction("Index");
			}

			var vm = new CreatPostVM()
			{
				Id = post.Id,
				Title = post.Title,
				Description = post.Description,
				ThumbnailUrl = post.ThumbnailUrl
			};


			return View(vm);
		}


		[HttpPost]
		public async Task<IActionResult> EditPost(CreatPostVM vm)
		{
			if (!ModelState.IsValid) { return View(vm); }
			var post = await _context.posts!.FirstOrDefaultAsync(x => x.Id == vm.Id);
			if (post == null)
			{
				_notification.Error("Post not found!");
				return View();
			}



			post.Title = vm.Title;
			post.Description = vm.Description;
			if (vm.Thumbnail != null)
			{
				post.ThumbnailUrl = UploadImage(vm.Thumbnail!);
			}

			await _context.SaveChangesAsync();
			_notification.Success("Post Updated Successfully!");
			return RedirectToAction("Index", "Post", new { area = "Admin" });

		}

		public string UploadImage(IFormFile file)
        {
            string uniqueFileName = "";
            var folderPath = Path.Combine(_webHostEnvironment.WebRootPath, "thumbnails");
            uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            var filePath = Path.Combine(folderPath, uniqueFileName);
            using (FileStream fileStream = System.IO.File.Create(filePath))
            {
                file.CopyTo(fileStream);
            }
            return uniqueFileName;
        }
    }
}
