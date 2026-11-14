extern alias BackendApi;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ImageMagick;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;

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
        using var form = Upload(Image(format), "cover"+ext, mime);
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
            (png,"../../cover.png","image/png"),
            (png,@"..\cover.png","image/png"),
            (png,@"C:\cover.png","image/png"),
            (png,"cover..png","image/png"),
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
        var duplicate=await admin.PostAsJsonAsync("/api/books",payload);
        Assert.Equal(HttpStatusCode.BadRequest,duplicate.StatusCode);
        Assert.Equal("application/json",duplicate.Content.Headers.ContentType?.MediaType);
        using(var duplicateBody=JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync()))
            Assert.Equal("BAD_REQUEST",duplicateBody.RootElement.GetProperty("code").GetString());
        using var created=JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id=created.RootElement.GetProperty("data").GetInt32();
        Assert.Equal(HttpStatusCode.OK,(await admin.PutAsJsonAsync($"/api/books/{id}",payload)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await admin.DeleteAsync($"/api/books/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await admin.DeleteAsync("/api/books/2147483647")).StatusCode);
        foreach(var text in new[]{"{","null","{}"})
        {
            var invalid=await admin.PostAsync("/api/books",new StringContent(text,Encoding.UTF8,"application/json"));
            Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);
            await AssertProblemDetails(invalid);
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

    [Theory]
    [InlineData("GET","/api/books?page=invalid",null,"problem")]
    [InlineData("GET","/api/books?pageSize=invalid",null,"problem")]
    [InlineData("GET","/api/books?categoryId=invalid",null,"problem")]
    [InlineData("POST","/api/books","{","problem")]
    [InlineData("POST","/api/users","{","wrapper")]
    [InlineData("POST","/api/books/1/change-barcode","{","wrapper")]
    [InlineData("POST","/api/orders/counter/checkout","{","problem")]
    public async Task Baseline_seven_case_validation_contract_is_preserved(string method,string path,string? body,string expected)
    {
        using var host=new BooksApiTestHost();
        using var client=host.Client(method=="GET" ? null : "Admin");
        var response=method=="GET" ? await client.GetAsync(path) :
            await client.PostAsync(path,new StringContent(body!,Encoding.UTF8,"application/json"));
        if(expected=="problem") await AssertProblemDetails(response);
        else
        {
            Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
            Assert.Equal("application/json",response.Content.Headers.ContentType?.MediaType);
            using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(json.RootElement.GetProperty("success").GetBoolean());
            Assert.Equal("BAD_REQUEST",json.RootElement.GetProperty("code").GetString());
            Assert.False(json.RootElement.TryGetProperty("errors",out _));
        }
    }

    [Theory]
    [InlineData("POST","null")]
    [InlineData("PUT","null")]
    [InlineData("PUT","{")]
    public async Task Null_and_malformed_write_bodies_remain_framework_problem_details(string method,string body)
    {
        using var host=new BooksApiTestHost();
        using var client=host.Client("Admin");
        using var request=new HttpRequestMessage(new HttpMethod(method), method=="PUT" ? "/api/books/1" : "/api/books")
        { Content=new StringContent(body,Encoding.UTF8,"application/json") };
        await AssertProblemDetails(await client.SendAsync(request));
    }

    private static async Task AssertProblemDetails(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        Assert.Equal("application/problem+json",response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8",response.Content.Headers.ContentType?.CharSet);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach(var key in new[]{"type","title","status","errors"}) Assert.True(json.RootElement.TryGetProperty(key,out _));
        Assert.Equal(400,json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.Object,json.RootElement.GetProperty("errors").ValueKind);
        Assert.False(json.RootElement.TryGetProperty("success",out _));
    }

    [SqlQaFact]
    public async Task Kestrel_rejects_oversized_multipart_before_persistence_and_keeps_valid_uploads_working()
    {
        await using var db=sql.Context();
        var before=JsonSerializer.Serialize(await db.Books.AsNoTracking().OrderBy(b=>b.BookId).ToListAsync());
        using var host=new BooksApiTestHost(db.Database.GetConnectionString());
        host.UseKestrel(0);
        using var client=host.Client("Admin");
        client.BaseAddress=new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using(var form=Upload(new byte[5*1024*1024+128*1024],"large.png","image/png"))
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/api/books/upload-image") { Content=form };
            request.Headers.ExpectContinue=true;
            var rejected=await client.SendAsync(request);
            await AssertProblemDetails(rejected);
            var text=await rejected.Content.ReadAsStringAsync();
            Assert.DoesNotContain("StackTrace",text,StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("ImageMagick",text,StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(0,FileCount(host));
        Assert.Equal(before,JsonSerializer.Serialize(await db.Books.AsNoTracking().OrderBy(b=>b.BookId).ToListAsync()));
        using var valid=Upload(Image("PNG"),"cover.png","image/png");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/api/books/upload-image",valid)).StatusCode);
        Assert.Equal(1,FileCount(host));
        Assert.InRange(ResourceLimits.Memory,1UL,256UL*1024*1024);
        Assert.InRange(ResourceLimits.MaxMemoryRequest,1UL,128UL*1024*1024);
        Assert.InRange(ResourceLimits.MaxProfileSize,1UL,1024UL*1024);
        Assert.Equal(0UL,ResourceLimits.Disk);
        Assert.InRange(ResourceLimits.Thread,1UL,2UL);
    }

    [Fact]
    public async Task Two_slow_multipart_requests_bound_admission_and_excess_is_safe_429()
    {
        using var host=new BooksApiTestHost();
        host.UseKestrel(0);
        using var client=host.Client("Admin");
        client.BaseAddress=new Uri(host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
        using var firstForm=Upload(Image("PNG"),"first.png","image/png");
        using var secondForm=Upload(Image("PNG"),"second.png","image/png");
        using var first=new GatedMultipart(await firstForm.ReadAsByteArrayAsync());
        using var second=new GatedMultipart(await secondForm.ReadAsByteArrayAsync());
        first.Headers.ContentType=firstForm.Headers.ContentType;
        second.Headers.ContentType=secondForm.Headers.ContentType;
        var admission=new AdmissionObserver();
        using var subscription=host.Services.GetRequiredService<System.Diagnostics.DiagnosticListener>()
            .Subscribe(admission, name=>name=="Microsoft.AspNetCore.Mvc.BeforeAction");
        var a=client.PostAsync("/api/books/upload-image",first);
        var b=client.PostAsync("/api/books/upload-image",second);
        try
        {
            await Task.WhenAll(first.Started.Task,second.Started.Task).WaitAsync(TimeSpan.FromSeconds(15));
            await admission.BothAdmitted.Task.WaitAsync(TimeSpan.FromSeconds(15));
            using var probe=new MultipartFormDataContent();
            using var rejected=await client.PostAsync("/api/books/upload-image",probe);
            Assert.NotNull(rejected);
            Assert.Equal(HttpStatusCode.TooManyRequests,rejected!.StatusCode);
            Assert.Equal("1",rejected.Headers.RetryAfter?.Delta?.TotalSeconds.ToString());
            using var json=JsonDocument.Parse(await rejected.Content.ReadAsStringAsync());
            Assert.Equal("TOO_MANY_REQUESTS",json.RootElement.GetProperty("code").GetString());
            Assert.Equal(0,FileCount(host));
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/books")).StatusCode);
        }
        finally
        {
            first.Release.TrySetResult(); second.Release.TrySetResult();
            await Task.WhenAll(a,b).WaitAsync(TimeSpan.FromSeconds(30));
        }
        Assert.Equal(HttpStatusCode.OK,(await a).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await b).StatusCode);
        Assert.Equal(2,FileCount(host));
    }

    // Observe the existing MVC diagnostic event after the limiter, before form binding.
    private sealed class AdmissionObserver : IObserver<KeyValuePair<string,object?>>
    {
        private int count;
        public TaskCompletionSource BothAdmitted { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void OnNext(KeyValuePair<string,object?> value)
        {
            if(value.Key=="Microsoft.AspNetCore.Mvc.BeforeAction" && Interlocked.Increment(ref count)==2)
                BothAdmitted.TrySetResult();
        }
        public void OnCompleted() { }
        public void OnError(Exception error) => BothAdmitted.TrySetException(error);
    }

    // A tiny, bounded test body pauses after the first boundary bytes, before form binding completes.
    private sealed class GatedMultipart(byte[] bytes) : HttpContent
    {
        public TaskCompletionSource Started { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override bool TryComputeLength(out long length) { length=bytes.Length; return true; }
        protected override async Task SerializeToStreamAsync(Stream stream,TransportContext? context)
        {
            await stream.WriteAsync(bytes.AsMemory(0,16));
            await stream.FlushAsync();
            Started.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await stream.WriteAsync(bytes.AsMemory(16));
        }
    }


    [Fact]
    public async Task Valid_webp_immediately_below_five_MiB_is_decoded_and_reencoded()
    {
        var original=Image("WEBP");
        var bytes=new byte[5*1024*1024-2]; // RIFF containers are word-aligned.
        original.CopyTo(bytes,0);
        "JUNK"u8.CopyTo(bytes.AsSpan(original.Length));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(original.Length+4,4),(uint)(bytes.Length-original.Length-8));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4,4),(uint)(bytes.Length-8));
        using(var decoded=new MagickImage(bytes)) Assert.Equal(4U,decoded.Width);
        using var host=new BooksApiTestHost();
        using var client=host.Client("Admin");
        using var form=Upload(bytes,"boundary.webp","image/webp");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/api/books/upload-image",form)).StatusCode);
        Assert.Equal(1,FileCount(host));
        Assert.True(new FileInfo(Directory.GetFiles(host.UploadRoot).Single()).Length<bytes.Length);
    }

    [Fact]
    public async Task Pre_cancelled_upload_and_file_path_obstruction_leave_no_cover_and_allow_recovery()
    {
        using var host=new BooksApiTestHost();
        using var client=host.Client("Admin");
        var bytes=Image("PNG");
        var file=new Microsoft.AspNetCore.Http.FormFile(new MemoryStream(bytes),0,bytes.Length,"file","cover.png") { Headers=new Microsoft.AspNetCore.Http.HeaderDictionary(), ContentType="image/png" };
        var controller=new BackendApi::BookKiosk.API.Controllers.BooksController(
            host.Services.GetRequiredService<BookKiosk.Application.Interfaces.Services.IBookService>(),
            host.Services.GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        controller.ControllerContext=new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext=new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        controller.HttpContext.RequestAborted=cancelled.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>controller.UploadImage(file));
        Assert.Equal(0,FileCount(host));
        var obstruction=Path.GetDirectoryName(host.UploadRoot)!;
        await File.WriteAllTextAsync(obstruction,"QA obstruction");
        try
        {
            using var blocked=Upload(bytes,"cover.png","image/png");
            var response=await client.PostAsync("/api/books/upload-image",blocked);
            Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
            var body=await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(obstruction,body);
            using var json=JsonDocument.Parse(body);
            Assert.Equal("INTERNAL_ERROR",json.RootElement.GetProperty("code").GetString());
            Assert.Equal(0,FileCount(host));
        }
        finally { File.Delete(obstruction); }
        using var recovered=Upload(bytes,"cover.png","image/png");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/api/books/upload-image",recovered)).StatusCode);
        Assert.Equal(1,FileCount(host));
    }


    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    public async Task Kiosk_checkout_keeps_its_automatic_problem_details(string body)
    {
        using var host=new BooksApiTestHost();
        using var client=host.Client();
        var config=host.Services.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        client.DefaultRequestHeaders.Add("X-API-KEY",config["ApiSettings:ApiKey"]);
        await AssertProblemDetails(await client.PostAsync("/api/orders/kiosk/checkout",new StringContent(body,Encoding.UTF8,"application/json")));
    }

    [Fact]
    public async Task Animated_webp_is_still_normalized_to_a_static_first_frame()
    {
        using var frames=new MagickImageCollection();
        frames.Add(new MagickImage(MagickColors.Blue,4,4));
        frames.Add(new MagickImage(MagickColors.Red,4,4));
        foreach(var frame in frames) frame.AnimationDelay=10;
        using var host=new BooksApiTestHost();
        using var client=host.Client("Admin");
        using var form=Upload(frames.ToByteArray(MagickFormat.WebP),"animation.webp","image/webp");
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsync("/api/books/upload-image",form)).StatusCode);
        using var stored=new MagickImageCollection(Directory.GetFiles(host.UploadRoot).Single());
        Assert.Single(stored);
    }

}
