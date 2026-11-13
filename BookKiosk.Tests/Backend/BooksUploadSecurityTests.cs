extern alias BackendApi;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ImageMagick;

namespace BookKiosk.Tests.Backend;

public class BooksUploadSecurityTests : IClassFixture<DisposableSqlQa>
{
    private readonly DisposableSqlQa sql;
    public BooksUploadSecurityTests(DisposableSqlQa sql) => this.sql=sql;

    private static byte[] Image(string format)
    {
        using var image = new MagickImage(MagickColors.Blue,4,4);
        return image.ToByteArray(format switch { "JPEG"=>MagickFormat.Jpeg, "PNG"=>MagickFormat.Png, _=>MagickFormat.WebP });
    }
    private static MultipartFormDataContent Upload(byte[] bytes, string name, string mime)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(file, "file", name);
        return form;
    }
    private static int FileCount(BooksApiTestHost host) => Directory.Exists(host.UploadRoot) ? Directory.GetFiles(host.UploadRoot).Length : 0;

    [Theory]
    [InlineData(".jpg","image/jpeg","JPEG")]
    [InlineData(".jpeg","image/jpeg","JPEG")]
    [InlineData(".png","image/png","PNG")]
    [InlineData(".webp","image/webp","WEBP")]
    public async Task Valid_pixels_are_published_under_uuid_name(string ext,string mime,string format)
    {
        using var host = new BooksApiTestHost();
        using var client = host.Client("Admin");
        using var form = Upload(Image(format), "../../cover"+ext, mime);
        var response = await client.PostAsync("/api/books/upload-image",form);
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var url = json.RootElement.GetProperty("data").GetString()!;
        Assert.Matches(@"^/uploads/books/[a-f0-9]{32}\.(jpg|jpeg|png|webp)$",url);
        Assert.Equal(1,FileCount(host));
        var saved=await File.ReadAllBytesAsync(Path.Combine(host.UploadRoot,Path.GetFileName(url)));
        Assert.Equal(format,new MagickImageInfo(saved).Format.ToString().ToUpperInvariant());
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task Rejected_payloads_leave_no_files_and_use_safe_wrapper()
    {
        using var host=new BooksApiTestHost();
        using var client=host.Client("Admin");
        var png=Image("PNG");
        var cases=new (byte[],string,string)[] {
            (Array.Empty<byte>(),"x.png","image/png"),
            (new byte[5*1024*1024+1],"x.png","image/png"),
            (Encoding.UTF8.GetBytes("<script>alert(1)</script>"),"x.jpg","image/jpeg"),
            (new byte[]{255,216,255,0,255,217},"x.jpg","image/jpeg"),
            (png[..16],"x.png","image/png"),
            (png,"x.jpg","image/jpeg"),
            (png,"x.png","image/jpeg"),
            (png,"x.svg","image/png"),
            (png.Concat(Encoding.UTF8.GetBytes("<script>payload</script>")).ToArray(),"x.png","image/png"),
            (Encoding.ASCII.GetBytes("RIFFxxxxWEBPjunk"),"x.webp","image/webp")
        };
        foreach(var (bytes,name,mime) in cases)
        {
            using var form=Upload(bytes,name,mime);
            var response=await client.PostAsync("/api/books/upload-image",form);
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("BAD_REQUEST",json.RootElement.GetProperty("code").GetString());
            Assert.Equal(0,FileCount(host));
        }
    }

    [Fact]
    public async Task Pixel_bombs_and_forged_reported_length_are_rejected_before_persistence()
    {
        using var host=new BooksApiTestHost();
        using var client=host.Client("Admin");
        using var oversizedPixels=new MagickImage(MagickColors.Blue,4001,4000);
        using var data=new MemoryStream();
        oversizedPixels.Write(data,MagickFormat.Png);
        using var form=Upload(data.ToArray(),"large.png","image/png");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/api/books/upload-image",form)).StatusCode);
        var forged=new Moq.Mock<Microsoft.AspNetCore.Http.IFormFile>();
        forged.SetupGet(f=>f.Length).Returns(1);
        forged.SetupGet(f=>f.FileName).Returns("cover.png");
        forged.SetupGet(f=>f.ContentType).Returns("image/png");
        forged.Setup(f=>f.OpenReadStream()).Returns(()=>new MemoryStream(new byte[5*1024*1024+1]));
        Assert.Null(await BackendApi::BookKiosk.API.Services.BookCoverImageValidator.NormalizeAsync(forged.Object,CancellationToken.None));
        Assert.Equal(0,FileCount(host));
    }

    [SqlQaFact]
    public async Task Write_rbac_matrix_blocks_before_sql_or_filesystem_mutation_and_admin_crud_works()
    {
        var book=await sql.AddBookAsync();
        await using var db=sql.Context();
        using var host=new BooksApiTestHost(db.Database.GetConnectionString());
        var payload=new { barcode="qa-"+Guid.NewGuid().ToString("N"),title="QA HTTP",author="QA",categoryId=book.CategoryId,
            costPrice=0,sellingPrice=10000,stockQuantity=10,isActive=true };
        var before=JsonSerializer.Serialize(await db.Books.AsNoTracking().OrderBy(b=>b.BookId).ToListAsync());
        foreach(var role in new string?[]{null,"Invalid","Staff","Manager"})
        {
            using var client=host.Client(role);
            var expected=role is null or "Invalid" ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            Assert.Equal(expected,(await client.PostAsJsonAsync("/api/books",payload)).StatusCode);
            Assert.Equal(expected,(await client.PutAsJsonAsync($"/api/books/{book.BookId}",payload)).StatusCode);
            Assert.Equal(expected,(await client.DeleteAsync($"/api/books/{book.BookId}")).StatusCode);
            using var form=Upload(Image("PNG"),"cover.png","image/png");
            Assert.Equal(expected,(await client.PostAsync("/api/books/upload-image",form)).StatusCode);
            Assert.Equal(before,JsonSerializer.Serialize(await db.Books.AsNoTracking().OrderBy(b=>b.BookId).ToListAsync()));
            Assert.Equal(0,FileCount(host));
        }
        using var admin=host.Client("Admin");
        var create=await admin.PostAsJsonAsync("/api/books",payload);
        Assert.Equal(HttpStatusCode.Created,create.StatusCode);
        using var created=JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id=created.RootElement.GetProperty("data").GetInt32();
        Assert.Equal(HttpStatusCode.OK,(await admin.PutAsJsonAsync($"/api/books/{id}",payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.DeleteAsync($"/api/books/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await admin.DeleteAsync("/api/books/2147483647")).StatusCode);
        foreach(var text in new[]{"{","null","{}"})
        {
            var invalid=await admin.PostAsync("/api/books",new StringContent(text,Encoding.UTF8,"application/json"));
            Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
            using var body=JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
            Assert.False(body.RootElement.GetProperty("success").GetBoolean());
        }
        var beforeConflict=JsonSerializer.Serialize(await db.Books.AsNoTracking().OrderBy(b=>b.BookId).ToListAsync());
        var conflict=await admin.PostAsJsonAsync($"/api/books/{book.BookId}/change-barcode",new { newBarcode=payload.barcode,reason="QA duplicate contract" });
        Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);
        using var conflictBody=JsonDocument.Parse(await conflict.Content.ReadAsStringAsync());
        Assert.Equal("CONFLICT",conflictBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(beforeConflict,JsonSerializer.Serialize(await db.Books.AsNoTracking().OrderBy(b=>b.BookId).ToListAsync()));
        using var anonymous=host.Client();
        Assert.Equal(HttpStatusCode.OK,(await anonymous.GetAsync("/api/books")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await anonymous.GetAsync($"/api/books/{book.BookId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await anonymous.GetAsync($"/api/books/barcode/{book.Barcode}")).StatusCode);
    }
}
