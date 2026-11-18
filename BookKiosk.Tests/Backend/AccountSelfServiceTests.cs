using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using BookKiosk.Domain.Entities;
using BookKiosk.Domain.Enums;
using BookKiosk.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BookKiosk.Tests.Backend;

public sealed class AccountSelfServiceTests(DisposableSqlQa sql) : IClassFixture<DisposableSqlQa>
{
    [SqlQaFact]
    public async Task Real_jwt_sql_own_profile_password_change_and_security_contract()
    {
        await using var context=sql.Context();
        using var host=new BooksApiTestHost(context.Database.GetConnectionString());
        using var anonymous=host.Client();
        foreach(var route in new[]{"/api/account/me","/api/account/change-password"})
        {
            using var response=route.EndsWith("me") ? await anonymous.GetAsync(route) : await anonymous.PostAsJsonAsync(route,Change());
            Assert.Equal(HttpStatusCode.Unauthorized,response.StatusCode);
        }
        using(var invalid=host.Client("Invalid"))
            Assert.Equal(HttpStatusCode.Unauthorized,(await invalid.GetAsync("/api/account/me")).StatusCode);
        foreach(var actor in new string?[]{null,"bad","0","-1","2147483647"})
        {
            using var malformed=host.Client("Staff",actor);
            Assert.Equal(HttpStatusCode.Unauthorized,(await malformed.GetAsync("/api/account/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await malformed.PostAsJsonAsync("/api/account/change-password",Change())).StatusCode);
        }
        foreach(var role in new[]{UserRole.Admin,UserRole.Manager,UserRole.Staff})
        {
            var user=new User { Username="qa_self_"+role,FullName="QA self",Role=role,IsActive=true,PasswordHash=BCrypt.Net.BCrypt.HashPassword("OriginalPass1",4) };
            context.Users.Add(user); await context.SaveChangesAsync();
            using var client=host.Client();
            using(var login=await client.PostAsJsonAsync("/api/auth/login",new{username=user.Username,password="OriginalPass1"}))
            {
                Assert.Equal(HttpStatusCode.OK,login.StatusCode);
                using var json=JsonDocument.Parse(await login.Content.ReadAsStringAsync());
                client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",json.RootElement.GetProperty("data").GetProperty("accessToken").GetString());
            }
            using(var response=await client.GetAsync("/api/account/me?userId=999"))
            {
                Assert.Equal(HttpStatusCode.OK,response.StatusCode);
                using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var data=json.RootElement.GetProperty("data");
                Assert.Equal(user.UserId,data.GetProperty("userId").GetInt32());
                Assert.Equal(role.ToString(),data.GetProperty("role").GetString());
                Assert.Equal(new[]{"fullName","isActive","role","userId","username"}, data.EnumerateObject().Select(p=>p.Name).Order().ToArray());
                Assert.True(response.Headers.CacheControl?.NoStore);
            }
            foreach(var body in new object[]{ Change("wrong"),Change("OriginalPass1","weak"),Change("OriginalPass1",new string('a',73)+"A1"),
                Change("OriginalPass1",new string('é',36)+"A1"),Change("OriginalPass1","OriginalPass1"),
                new{currentPassword="OriginalPass1",newPassword="ChangedPass2",userId=999,role="Admin",username="hijacked"},
                new{newPassword="ChangedPass2"} })
            {
                using var response=await client.PostAsJsonAsync("/api/account/change-password",body);
                Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
                var text=await response.Content.ReadAsStringAsync();
                Assert.DoesNotContain("OriginalPass1",text); Assert.DoesNotContain("ChangedPass2",text);
                Assert.Equal(user.PasswordHash,await context.Users.AsNoTracking().Where(u=>u.UserId==user.UserId).Select(u=>u.PasswordHash).SingleAsync());
            }
            using(var success=await client.PostAsJsonAsync("/api/account/change-password",Change()))
            {
                Assert.Equal(HttpStatusCode.OK,success.StatusCode);
                using var json=JsonDocument.Parse(await success.Content.ReadAsStringAsync());
                Assert.Equal(JsonValueKind.Null,json.RootElement.GetProperty("data").ValueKind);
            }
            // Existing token stays valid by documented policy; password mutation does not claim revocation.
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/account/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync("/api/auth/login",new{username=user.Username,password="OriginalPass1"})).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await anonymous.PostAsJsonAsync("/api/auth/login",new{username=user.Username,password="ChangedPass2"})).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/account/change-password",Change())).StatusCode);
            var saved=await context.Users.AsNoTracking().SingleAsync(u=>u.UserId==user.UserId);
            Assert.Equal(user.Username,saved.Username);Assert.Equal(user.Role,saved.Role);Assert.Equal(user.FullName,saved.FullName);
            Assert.True(BCrypt.Net.BCrypt.Verify("ChangedPass2",saved.PasswordHash));
            await context.Users.Where(u=>u.UserId==user.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.IsActive,false));
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/account/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/account/change-password",Change("ChangedPass2","ThirdPass3"))).StatusCode);
        }
    }

    [SqlQaFact]
    public async Task Concurrent_sql_updates_only_one_wins_and_inactive_account_cannot_change()
    {
        await using var setup=sql.Context();
        var user=new User {Username="qa_cas",FullName="QA",Role=UserRole.Staff,IsActive=true,PasswordHash=BCrypt.Net.BCrypt.HashPassword("OriginalPass1",4)};
        setup.Users.Add(user);await setup.SaveChangesAsync();
        await using var first=sql.Context();await using var second=sql.Context();
        var next1=BCrypt.Net.BCrypt.HashPassword("ChangedPass2",4);
        var next2=BCrypt.Net.BCrypt.HashPassword("ChangedPass3",4);
        var results=await Task.WhenAll(new AccountRepository(first).TryChangePasswordAsync(user.UserId,user.PasswordHash,next1,default),
            new AccountRepository(second).TryChangePasswordAsync(user.UserId,user.PasswordHash,next2,default));
        Assert.Single(results,r=>r);Assert.Single(results,r=>!r);
        var actual=await setup.Users.AsNoTracking().SingleAsync(u=>u.UserId==user.UserId);
        Assert.Equal(results[0]?next1:next2,actual.PasswordHash);
        await setup.Users.Where(u=>u.UserId==user.UserId).ExecuteUpdateAsync(s=>s.SetProperty(u=>u.IsActive,false));
        Assert.False(await new AccountRepository(first).TryChangePasswordAsync(user.UserId,actual.PasswordHash,user.PasswordHash,default));
    }
    private static object Change(string currentPassword="OriginalPass1",string newPassword="ChangedPass2") => new {currentPassword,newPassword};
}
