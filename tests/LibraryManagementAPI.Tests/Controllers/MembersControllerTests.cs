// Controller & Service namespaces (عدّل حسب مشروعك)
using ApiProject.Controllers;
using Data.Services; // أو Data.Services
using Domain.DTOs;
using FluentAssertions;
using LibraryManagementAPI.Tests.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LibraryManagementAPI.Tests.Controllers;

public class MembersControllerTests
{
    [Fact]
    public async Task GetById_Should_Return_NotFound_When_Missing()
    {
        var ctx = DbContextFactory.Create();
        SeedData.Seed(ctx);

        var service = new MemberService(ctx);
        var logger = new LoggerFactory().CreateLogger<MembersController>();
        var ctrl = new MembersController(service, logger);

        var result = await ctrl.GetById(999, default);
        result.Should().BeOfType<NotFoundObjectResult>();

        var notFound = (NotFoundObjectResult)result;
        notFound.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetAll_Should_Return_Ok()
    {
        var ctx = DbContextFactory.Create();
        SeedData.Seed(ctx);

        var service = new MemberService(ctx);
        var logger = new LoggerFactory().CreateLogger<MembersController>();
        var ctrl = new MembersController(service, logger);

        var qp = new MemberQueryParams { Page = 1, PageSize = 10 }; // عدّل لو عندك اسم مختلف
        var action = await ctrl.GetAll(qp, default);

        action.Should().BeOfType<OkObjectResult>();
    }
}
