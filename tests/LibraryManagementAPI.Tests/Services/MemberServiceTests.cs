using Data;
// لو خدمتك في namespace آخر عدّل السطر التالي:
using Data.Services;
// ولو عندك DTOs للتحديث الذاتي عدّل الاسم حسب مشروعك:
using Domain.DTOs;
using FluentAssertions;
using LibraryManagementAPI.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace LibraryManagementAPI.Tests.Services;

public class MemberServiceTests
{
    [Fact]
    public async Task Db_Should_Seed_And_Read_Members()
    {
        var ctx = DbContextFactory.Create();
        SeedData.Seed(ctx);

        var count = await ctx.Members.CountAsync();
        count.Should().BeGreaterThan(0);

        var ali = await ctx.Members.AsNoTracking().SingleAsync(m => m.Email == "m1@x.com");
        ali.Name.Should().Be("Ali Ahmad");
    }

    // مثال اختبار افتراضي لو عندك دالة تحديث ذاتي في MemberService:
    // public Task<bool> UpdateSelfAsync(int userId, MemberSelfUpdateDto dto)
    [Fact]
    public async Task UpdateSelfAsync_Should_Update_Name_And_Email_When_Available()
    {
        var ctx = DbContextFactory.Create();
        SeedData.Seed(ctx);

        var sut = new MemberService(ctx); // غيّر التوقيع إذا مختلف

        var dto = new MemberSelfUpdateDto
        {
            Name = "Ali A.",
            Email = "ali.new@x.com"
        };

        var ok = await sut.UpdateSelfAsync(2, dto); // userId=2 هو m1
        ok.Should().BeTrue();

        var updated = await ctx.Members.AsNoTracking().SingleAsync(x => x.UserId == 2);
        updated.Name.Should().Be("Ali A.");
        updated.Email.Should().Be("ali.new@x.com");
    }
}
