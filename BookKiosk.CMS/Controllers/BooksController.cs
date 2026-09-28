using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.CMS.Controllers;

public class BooksController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Create()
    {
        return View();
    }

    public IActionResult Edit(int id)
    {
        ViewBag.BookId = id;
        return View();
    }
}
