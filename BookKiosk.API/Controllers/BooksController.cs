using BookKiosk.Application.DTOs.Books;
using BookKiosk.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace BookKiosk.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BooksController : ControllerBase
{
    private readonly IBookService _bookService;

    public BooksController(IBookService bookService)
    {
        _bookService = bookService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllBooks()
    {
        var response = await _bookService.GetAllBooksAsync();
        return Ok(response);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBookById(int id)
    {
        var response = await _bookService.GetBookByIdAsync(id);
        if (!response.Success)
            return NotFound(response);
            
        return Ok(response);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetBookByBarcode(string barcode)
    {
        var response = await _bookService.GetBookByBarcodeAsync(barcode);
        if (!response.Success)
            return NotFound(response);
            
        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBook([FromBody] CreateBookDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _bookService.CreateBookAsync(dto);
        if (!response.Success)
            return BadRequest(response);

        return CreatedAtAction(nameof(GetBookById), new { id = response.Data }, response);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateBook(int id, [FromBody] UpdateBookDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _bookService.UpdateBookAsync(id, dto);
        if (!response.Success)
            return NotFound(response);

        return Ok(response);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteBook(int id)
    {
        var response = await _bookService.DeleteBookAsync(id);
        if (!response.Success)
            return NotFound(response);

        return Ok(response);
    }
}
